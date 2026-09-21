using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IStockTransferService
{
    Task<ApiCallResult<IReadOnlyList<StockTransferViewModel>>> LoadTransfersAsync(
        string branchId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<StockTransferViewModel>> LoadTransferAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateTransferAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdateTransferAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteTransferAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}
