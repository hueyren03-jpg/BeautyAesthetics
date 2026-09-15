using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IServiceItemService
{
    Task<ApiCallResult<IReadOnlyList<ServiceViewModel.ServiceItem>>> LoadServiceItemsAsync(
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateServiceAsync(
        ServiceViewModel.ServiceItem service,
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteServiceAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default);
    Task<ApiCallResult<bool>> UpdateServiceAsync(
        ServiceViewModel.ServiceItem service,
        string branchId = "hq",
        CancellationToken cancellationToken = default);
}
