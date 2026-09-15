using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IInventoryPendingAcceptService
{
    Task<ApiCallResult<IReadOnlyList<PendingStockReceiptViewModel>>> LoadPendingAsync(
        string branchId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<IReadOnlyList<PendingStockReceiptLineViewModel>>> LoadDetailsAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<string>> AcceptAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}
