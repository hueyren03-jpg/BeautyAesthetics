using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IStockGinService
{
    Task<ApiCallResult<IReadOnlyList<StockGinViewModel>>> LoadGinsAsync(
        string branchId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<StockGinViewModel>> LoadGinAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateGinAsync(
        StockGinViewModel gin,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdateGinAsync(
        StockGinViewModel gin,
        CancellationToken cancellationToken = default);
}
