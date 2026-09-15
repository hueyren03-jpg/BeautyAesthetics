namespace Beauty_Aesthetics_WebPos.Components.Services.Branches;

public interface IBranchSessionStore
{
    Task SaveBranchIdAsync(string branchId);
    Task<string?> GetBranchIdAsync();
    Task ClearAsync();
}
