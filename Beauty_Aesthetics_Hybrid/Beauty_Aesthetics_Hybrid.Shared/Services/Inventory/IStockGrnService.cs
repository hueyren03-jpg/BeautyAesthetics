using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IStockGrnService
{
    Task<ApiCallResult<IReadOnlyList<StockGrnViewModel>>> LoadGrnsAsync(
        string branchId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<StockGrnViewModel>> LoadGrnAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateGrnAsync(
        StockGrnViewModel grn,
        string branchId = "HQ",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdateGrnAsync(
        StockGrnViewModel grn,
        string branchId = "HQ",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteGrnAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}
