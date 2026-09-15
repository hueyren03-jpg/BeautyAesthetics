using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IPackageService
{
    Task<ApiCallResult<IReadOnlyList<InventoryPackageSummary>>> LoadPackagesAsync(
        IReadOnlyCollection<ServiceViewModel.ServiceItem> services,
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreatePackageAsync(
        InventoryPackageEdit package,
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdatePackageAsync(
        InventoryPackageEdit package,
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeletePackageAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryPackageSummary(
    string MasterAccountId,
    string Name,
    string Sku,
    int ServicesCount,
    int TotalDuration,
    decimal Price,
    IReadOnlyList<string> ServiceIds,
    string Status = "Active",
    string BranchId = "",
    string InventoryTypeName = "",
    string SalesDescription = "",
    DateTime? AvailableDateFrom = null,
    DateTime? AvailableDateTo = null,
    TimeSpan? AvailableTimeFrom = null,
    TimeSpan? AvailableTimeTo = null,
    string EInvoiceClassificationCode = "",
    string ImagePath = "",
    string ImageFileName = "");

public sealed record InventoryPackageEdit(
    string? MasterAccountId,
    string Name,
    string Sku,
    decimal Price,
    IReadOnlyCollection<ServiceViewModel.ServiceItem> Services);


