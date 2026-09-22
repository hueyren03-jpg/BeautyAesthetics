using System.Text.Json;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;

namespace Beauty_Aesthetics_WebPos.Components.Services.Branches;

public sealed class BranchSessionService(
    IBranchLookupService branchLookupService,
    IBranchSessionStore branchStore,
    AppState appState) : IBranchSessionService
{
    private readonly SemaphoreSlim initializeLock = new(1, 1);

    public IReadOnlyList<BranchLookupItem> AvailableBranches => appState.AvailableBranches;
    public BranchLookupItem? CurrentBranch => appState.CurrentBranch;
    public bool RequiresSelection => CurrentBranch is null;
    public string? ErrorMessage { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentBranch is not null)
        {
            return;
        }

        await initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (CurrentBranch is not null)
            {
                return;
            }

            ErrorMessage = null;

            IReadOnlyList<BranchLookupItem> branches;
            if (AvailableBranches.Count > 0)
            {
                // Another page/service may already have populated the branch list.
                // We still need to restore/select the working branch instead of
                // returning with CurrentBranch == null.
                branches = AvailableBranches;
            }
            else
            {
                var result = await branchLookupService.LoadBranchesAsync(cancellationToken);
                if (!result.Success || result.Value is null)
                {
                    ErrorMessage = result.ErrorMessage ?? "Unable to load branches.";
                    appState.SetAvailableBranches([]);
                    return;
                }

                var allowedIds = GetAllowedBranchIds(appState.CurrentUserJson);
                branches = allowedIds.Count == 0
                    ? result.Value
                    : result.Value.Where(branch => allowedIds.Contains(branch.Id)).ToList();

                appState.SetAvailableBranches(branches);
            }

            if (branches.Count == 0)
            {
                ErrorMessage = "No branch is available for this account.";
                return;
            }

            var savedBranchId = await branchStore.GetBranchIdAsync();
            var branchToSelect = FindBranch(branches, savedBranchId);

            if (branchToSelect is null && branches.Count == 1)
            {
                branchToSelect = branches[0];
            }

            if (branchToSelect is not null)
            {
                appState.SelectBranch(branchToSelect);
                await branchStore.SaveBranchIdAsync(branchToSelect.Id);
            }
        }
        finally
        {
            initializeLock.Release();
        }
    }

    public async Task<bool> SelectAsync(string branchId)
    {
        var branch = FindBranch(AvailableBranches, branchId);
        if (branch is null)
        {
            return false;
        }

        // Persist first so a clean reload can always restore the selected branch.
        await branchStore.SaveBranchIdAsync(branch.Id);

        try
        {
            appState.SelectBranch(branch);
        }
        catch
        {
            // A stale live component must not break branch switching. The caller
            // reloads the current route after selection, rebuilding branch-scoped UI.
        }

        return true;
    }

    public async Task ClearAsync()
    {
        appState.ClearBranchSession();
        await branchStore.ClearAsync();
    }

    private static BranchLookupItem? FindBranch(IEnumerable<BranchLookupItem> branches, string? branchId)
    {
        return string.IsNullOrWhiteSpace(branchId)
            ? null
            : branches.FirstOrDefault(branch =>
                string.Equals(branch.Id, branchId, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> GetAllowedBranchIds(string? currentUserJson)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(currentUserJson))
        {
            return ids;
        }

        try
        {
            using var document = JsonDocument.Parse(currentUserJson);
            FindBranchProperties(document.RootElement, ids);
        }
        catch (JsonException)
        {
        }

        return ids;
    }

    private static void FindBranchProperties(JsonElement element, HashSet<string> ids)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var name = Normalize(property.Name);
                if (name is "branchid" or "branchids" or "availablebranches" or "branches")
                {
                    CollectBranchIds(property.Value, ids);
                }
                else
                {
                    FindBranchProperties(property.Value, ids);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                FindBranchProperties(item, ids);
            }
        }
    }

    private static void CollectBranchIds(JsonElement element, HashSet<string> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                foreach (var value in (element.GetString() ?? string.Empty)
                             .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    ids.Add(value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectBranchIds(item, ids);
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (Normalize(property.Name) is "id" or "branchid" or "displaycode")
                    {
                        CollectBranchIds(property.Value, ids);
                    }
                }
                break;
        }
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
