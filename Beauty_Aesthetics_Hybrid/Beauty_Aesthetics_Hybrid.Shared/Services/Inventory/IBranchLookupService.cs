using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IBranchLookupService
{
    Task<ApiCallResult<IReadOnlyList<BranchLookupItem>>> LoadBranchesAsync(
        CancellationToken cancellationToken = default);
}

public sealed record BranchLookupItem(string Id, string Name)
{
    public string DisplayName => string.Equals(Id, Name, StringComparison.OrdinalIgnoreCase)
        ? Id
        : $"{Name} ({Id})";
}