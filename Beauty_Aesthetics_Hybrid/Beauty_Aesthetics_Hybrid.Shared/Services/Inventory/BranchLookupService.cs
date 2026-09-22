using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class BranchLookupService : IBranchLookupService
{
    private readonly BranchAC branchAC;

    public BranchLookupService(BranchAC branchAC)
    {
        this.branchAC = branchAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<BranchLookupItem>>> LoadBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await branchAC.LoadBranchesAsync(cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<BranchLookupItem>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load branches.");
        }

        var branches = result.Value
            .Where(IsActive)
            .Select(item => new BranchLookupItem(
                First(item.BranchID, item.DisplayCode, item.MasterAccountID).Trim().ToUpperInvariant(),
                First(item.Branch, item.BranchName, item.AccountName, item.DisplayCode, item.BranchID).Trim(),
                First(item.BranchGroupID, item.GroupID, item.BranchID).Trim()))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Name)
            .ToList();

        return ApiCallResult<IReadOnlyList<BranchLookupItem>>.Ok(result.StatusCode, branches);
    }

    private static bool IsActive(BranchLookupDTO item) =>
        item.Active is not false &&
        !string.Equals(item.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase);

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
