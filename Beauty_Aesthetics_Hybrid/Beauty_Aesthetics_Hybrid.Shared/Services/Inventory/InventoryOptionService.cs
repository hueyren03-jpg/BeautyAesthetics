using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class InventoryOptionService : IInventoryOptionService
{
    private const int CategorySupportingTableTypeId = 4;
    private const string DefaultBranchId = "HQ";

    private readonly SupportingTableAC supportingTableAC;
    private readonly AppFeedbackService feedback;

    public InventoryOptionService(SupportingTableAC supportingTableAC, AppFeedbackService feedback)
    {
        this.supportingTableAC = supportingTableAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<SupportingTableDM>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await supportingTableAC.LoadListByTypeAsync(CategorySupportingTableTypeId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<SupportingTableDM>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load categories.");
        }

        IReadOnlyList<SupportingTableDM> categories = result.Value
            .Where(c => c.Active)
            .OrderBy(c => c.SupportingTableName ?? string.Empty)
            .ToList();

        return ApiCallResult<IReadOnlyList<SupportingTableDM>>.Ok(result.StatusCode, categories);
    }

    public async Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateCategoryAsync(
        string categoryName,
        string externalCode,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var payload = new SupportingTableDM
        {
            SupportingTableName = categoryName.Trim(),
            SupportingTableTypeID = CategorySupportingTableTypeId,
            Active = true,
            BranchID = DefaultBranchId,
            TextField = string.IsNullOrWhiteSpace(externalCode) ? null : externalCode.Trim(),
            CreatedDateTime = now,
            ModifiedDateTime = now,
            SaveAction = 1,
            IsDirty = true,
            StrSelectedBrandID = string.Empty,
            LstExchangeRate = new()
        };

        var result = await supportingTableAC.CreateAsync(payload, cancellationToken);
        if (result.Success)
        {
            feedback.Success("Category created successfully.", "Category created");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to create category.", "Category not created");
        }

        return result;
    }

    public async Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            const string message = "The selected category has no record ID.";
            feedback.Warning(message, "Category not deleted");
            return ApiCallResult<SupportingTableSaveResultDTO>.Failure(
                System.Net.HttpStatusCode.BadRequest,
                message);
        }

        var result = await supportingTableAC.DeleteAsync(categoryId.Trim(), cancellationToken);
        if (result.Success)
        {
            feedback.Success("Category deleted successfully.", "Category deleted");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to delete category.", "Category not deleted");
        }

        return result;
    }
    public async Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateCategoryAsync(
        string categoryId,
        string categoryName,
        string externalCode,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var payload = new SupportingTableDM
        {
            SupportingTableID = categoryId.Trim(),
            SupportingTableName = categoryName.Trim(),
            SupportingTableTypeID = CategorySupportingTableTypeId,
            Active = true,
            BranchID = DefaultBranchId,
            TextField = string.IsNullOrWhiteSpace(externalCode) ? null : externalCode.Trim(),
            ModifiedDateTime = now,
            SaveAction = 2,
            IsDirty = true,
            StrSelectedBrandID = string.Empty,
            LstExchangeRate = new()
        };

        var result = await supportingTableAC.UpdateAsync(payload, cancellationToken);
        if (result.Success)
        {
            feedback.Success("Category updated successfully.", "Category updated");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to update category.", "Category not updated");
        }

        return result;
    }
}
