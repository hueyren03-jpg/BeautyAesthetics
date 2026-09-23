using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IInventoryOptionService
{
    Task<ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>> GetBrandsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateBrandAsync(
        string brandName,
        bool active,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateBrandAsync(
        string brandId,
        string brandName,
        bool active,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteBrandAsync(
        string brandId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateCategoryAsync(
        string categoryName,
        string externalCode,
        bool active,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateCategoryAsync(
        string categoryId,
        string categoryName,
        string externalCode,
        bool active,
        CancellationToken cancellationToken = default);
}
