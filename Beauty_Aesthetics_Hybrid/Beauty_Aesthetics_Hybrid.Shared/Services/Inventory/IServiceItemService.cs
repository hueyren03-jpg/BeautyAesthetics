using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IServiceItemService
{
    Task<ApiCallResult<IReadOnlyList<ServiceViewModel.ServiceItem>>> LoadServiceItemsAsync(
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<ServiceViewModel.ServiceItem>> LoadServiceAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<ServiceEditorDetails>> LoadServiceEditorDetailsAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateServiceAsync(
        ServiceViewModel.ServiceItem service,
        string branchId = "hq",
        CancellationToken cancellationToken = default,
        string branchGroupId = "",
        ServiceEditorDetails? editorDetails = null,
        IReadOnlyCollection<ServiceBranchSelection>? visibleBranches = null);

    Task<ApiCallResult<bool>> DeleteServiceAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default);
    Task<ApiCallResult<bool>> UpdateServiceAsync(
        ServiceViewModel.ServiceItem service,
        string branchId = "hq",
        CancellationToken cancellationToken = default,
        string branchGroupId = "",
        ServiceEditorDetails? editorDetails = null,
        IReadOnlyCollection<ServiceBranchSelection>? visibleBranches = null);
}

public sealed record ServiceEditorDetails(
    decimal PointBalance = 0m,
    decimal RedeemPoint = 0m,
    string BillOfMaterial = "");



public sealed record ServiceBranchSelection(
    string BranchId,
    string GroupId);
