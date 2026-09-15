using Beauty_Aesthetics_WebPos.Components.Services.Inventory;

namespace Beauty_Aesthetics_WebPos.Components.Services.Branches;

public interface IBranchSessionService
{
    IReadOnlyList<BranchLookupItem> AvailableBranches { get; }
    BranchLookupItem? CurrentBranch { get; }
    bool RequiresSelection { get; }
    string? ErrorMessage { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> SelectAsync(string branchId);
    Task ClearAsync();
}
