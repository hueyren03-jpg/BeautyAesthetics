using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IProductInventoryService
{
    Task<ApiCallResult<IReadOnlyList<InventoryViewModel.InventoryItem>>> LoadProductsAsync(
        string branchId = "HQ",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateProductAsync(
        InventoryViewModel.InventoryItem product,
        string branchId = "HQ",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdateProductAsync(
        InventoryViewModel.InventoryItem product,
        string branchId = "HQ",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteProductAsync(
        InventoryViewModel.InventoryItem product,
        CancellationToken cancellationToken = default);
}
