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
        CancellationToken cancellationToken = default,
        string branchGroupId = "");

    Task<ApiCallResult<bool>> UpdatePackageAsync(
        InventoryPackageEdit package,
        string branchId = "hq",
        CancellationToken cancellationToken = default,
        string branchGroupId = "");

    Task<ApiCallResult<bool>> DeletePackageAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryPackageLineSummary(
    string ServiceId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    bool IsDeferred,
    int InventoryTypeId = 3,
    string? AutoId = null);

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
    string ImageFileName = "",
    string Section = "",
    bool IsActive = true,
    string Barcode = "",
    string UnitOfMeasure = "unit",
    int ValidityDays = 0,
    int MemberExpiryDays = 0,
    string TriggeredMemberTypeId = "",
    decimal MemberMainAccountCredit = 0m,
    IReadOnlyList<InventoryPackageLineSummary>? Lines = null,
    decimal Points = 0m,
    string Policy = "",
    string TermCondition1 = "",
    string TermCondition2 = "",
    string TermCondition3 = "");

public sealed record InventoryPackageLineEdit(
    string InventoryId,
    string Description,
    int InventoryTypeId,
    decimal Quantity,
    decimal UnitPrice,
    bool IsDeferred);

public sealed record InventoryPackageEdit(
    string? MasterAccountId,
    string Name,
    string Sku,
    decimal Price,
    IReadOnlyCollection<ServiceViewModel.ServiceItem> Services,
    string Section = "",
    bool IsActive = true,
    string Barcode = "",
    string UnitOfMeasure = "unit",
    int ValidityDays = 0,
    int MemberExpiryDays = 0,
    string TriggeredMemberTypeId = "",
    decimal MemberMainAccountCredit = 0m,
    IReadOnlyCollection<InventoryPackageLineEdit>? Lines = null,
    string ImagePath = "",
    string ImageFileName = "",
    decimal Points = 0m,
    string Policy = "",
    string TermCondition1 = "",
    string TermCondition2 = "",
    string TermCondition3 = "");


