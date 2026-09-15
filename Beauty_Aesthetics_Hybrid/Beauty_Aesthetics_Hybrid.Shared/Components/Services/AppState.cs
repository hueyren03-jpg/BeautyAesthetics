using Beauty_Aesthetics_WebPos.Components.Services.Inventory;

namespace Beauty_Aesthetics_WebPos.Components.Services;

public sealed class AppState
{
    public string? CurrentUserJson { get; private set; }
    public string? UserEmail { get; private set; }
    public DateTimeOffset? SignedInAt { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentUserJson);
    public IReadOnlyList<BranchLookupItem> AvailableBranches { get; private set; } = [];
    public BranchLookupItem? CurrentBranch { get; private set; }
    public string? SelectedBranchID => CurrentBranch?.Id;
    public event Action? AuthenticationChanged;
    public event Action? BranchChanged;

    public void SetAuthenticatedUser(string currentUserJson, string? userEmail = null)
    {
        CurrentUserJson = currentUserJson;

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            UserEmail = userEmail;
        }

        SignedInAt = DateTimeOffset.UtcNow;
        AuthenticationChanged?.Invoke();
    }

    public void ClearAuthentication()
    {
        CurrentUserJson = null;
        UserEmail = null;
        SignedInAt = null;
        ClearBranchSession();
        AuthenticationChanged?.Invoke();
    }

    public void SetAvailableBranches(IReadOnlyList<BranchLookupItem> branches)
    {
        AvailableBranches = branches;
        if (CurrentBranch is not null && !branches.Any(branch =>
                string.Equals(branch.Id, CurrentBranch.Id, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentBranch = null;
        }

        BranchChanged?.Invoke();
    }

    public void SelectBranch(BranchLookupItem branch)
    {
        CurrentBranch = branch;
        BranchChanged?.Invoke();
    }

    public void ClearBranchSession()
    {
        AvailableBranches = [];
        CurrentBranch = null;
        BranchChanged?.Invoke();
    }
}
