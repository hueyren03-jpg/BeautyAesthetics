using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IInventoryOptionService
{
    Task<ApiCallResult<IReadOnlyList<SupportingTableDM>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateCategoryAsync(
        string categoryName,
        string externalCode,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default);
    Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateCategoryAsync(
        string categoryId,
        string categoryName,
        string externalCode,
        CancellationToken cancellationToken = default);
}
