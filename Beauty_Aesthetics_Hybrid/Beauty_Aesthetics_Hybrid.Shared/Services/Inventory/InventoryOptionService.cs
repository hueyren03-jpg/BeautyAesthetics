using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class InventoryOptionService : IInventoryOptionService
{
    private const int BrandSupportingTableTypeId = 34;
    private const int CategorySupportingTableTypeId = 55;
    private const string DefaultBranchId = "HQ";

    private readonly SupportingTableAC supportingTableAC;
    private readonly AppFeedbackService feedback;

    public InventoryOptionService(SupportingTableAC supportingTableAC, AppFeedbackService feedback)
    {
        this.supportingTableAC = supportingTableAC;
        this.feedback = feedback;
    }

    public Task<ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>> GetBrandsAsync(
        CancellationToken cancellationToken = default) =>
        LoadOptionsAsync(BrandSupportingTableTypeId, "brands", cancellationToken);

    public Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateBrandAsync(
        string brandName,
        bool active,
        CancellationToken cancellationToken = default) =>
        CreateOptionAsync(
            brandName,
            null,
            active,
            BrandSupportingTableTypeId,
            "brand",
            cancellationToken);

    public Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateBrandAsync(
        string brandId,
        string brandName,
        bool active,
        CancellationToken cancellationToken = default) =>
        UpdateOptionAsync(
            brandId,
            brandName,
            null,
            active,
            BrandSupportingTableTypeId,
            "brand",
            cancellationToken);

    public Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteBrandAsync(
        string brandId,
        CancellationToken cancellationToken = default) =>
        DeleteOptionAsync(brandId, "brand", cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        LoadOptionsAsync(CategorySupportingTableTypeId, "categories", cancellationToken);

    public Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateCategoryAsync(
        string categoryName,
        string externalCode,
        bool active,
        CancellationToken cancellationToken = default) =>
        CreateOptionAsync(
            categoryName,
            externalCode,
            active,
            CategorySupportingTableTypeId,
            "category",
            cancellationToken);

    public Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteCategoryAsync(
        string categoryId,
        CancellationToken cancellationToken = default) =>
        DeleteOptionAsync(categoryId, "category", cancellationToken);

    public Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateCategoryAsync(
        string categoryId,
        string categoryName,
        string externalCode,
        bool active,
        CancellationToken cancellationToken = default) =>
        UpdateOptionAsync(
            categoryId,
            categoryName,
            externalCode,
            active,
            CategorySupportingTableTypeId,
            "category",
            cancellationToken);

    private async Task<ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>> LoadOptionsAsync(
        int supportingTableTypeId,
        string optionLabel,
        CancellationToken cancellationToken)
    {
        var result = await supportingTableAC.LoadListByTypeAsync(supportingTableTypeId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? $"Unable to load {optionLabel}.");
        }

        IReadOnlyList<SupportingTableListItemDTO> options = result.Value
            .Where(option => !string.IsNullOrWhiteSpace(option.SupportingTableName))
            .OrderBy(option => option.SupportingTableName ?? string.Empty)
            .ToList();

        return ApiCallResult<IReadOnlyList<SupportingTableListItemDTO>>.Ok(result.StatusCode, options);
    }

    private async Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateOptionAsync(
        string optionName,
        string? textField,
        bool active,
        int supportingTableTypeId,
        string optionLabel,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var payload = new SupportingTableDM
        {
            SupportingTableName = optionName.Trim(),
            SupportingTableTypeID = supportingTableTypeId,
            Active = active,
            BranchID = DefaultBranchId,
            TextField = string.IsNullOrWhiteSpace(textField) ? null : textField.Trim(),
            CreatedDateTime = now,
            ModifiedDateTime = now,
            SaveAction = "Added",
            IsDirty = true,
            StrSelectedBrandID = string.Empty,
            LstExchangeRate = new()
        };

        var result = await supportingTableAC.CreateAsync(payload, cancellationToken);
        if (result.Success)
        {
            feedback.Success(
                $"{ToTitle(optionLabel)} created successfully.",
                $"{ToTitle(optionLabel)} created");
        }
        else
        {
            feedback.Error(
                result.ErrorMessage ?? $"Unable to create {optionLabel}.",
                $"{ToTitle(optionLabel)} not created");
        }

        return result;
    }

    private async Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateOptionAsync(
        string optionId,
        string optionName,
        string? textField,
        bool active,
        int supportingTableTypeId,
        string optionLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(optionId))
        {
            return InvalidId(optionLabel, "updated");
        }

        var loadResult = await supportingTableAC.LoadRecordAsync(optionId.Trim(), cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<SupportingTableSaveResultDTO>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? $"Unable to load the {optionLabel} before updating.");
        }

        var payload = loadResult.Value;
        payload.SupportingTableID = optionId.Trim();
        payload.SupportingTableName = optionName.Trim();
        payload.SupportingTableTypeID = supportingTableTypeId;
        payload.Active = active;
        payload.BranchID = string.IsNullOrWhiteSpace(payload.BranchID) ? DefaultBranchId : payload.BranchID;
        payload.TextField = string.IsNullOrWhiteSpace(textField) ? null : textField.Trim();
        payload.ModifiedDateTime = DateTime.Now;
        payload.SaveAction = "Changed";
        payload.IsDirty = true;
        payload.StrSelectedBrandID ??= string.Empty;
        payload.LstExchangeRate ??= new();

        var result = await supportingTableAC.UpdateAsync(payload, cancellationToken);
        if (result.Success)
        {
            feedback.Success(
                $"{ToTitle(optionLabel)} updated successfully.",
                $"{ToTitle(optionLabel)} updated");
        }
        else
        {
            feedback.Error(
                result.ErrorMessage ?? $"Unable to update {optionLabel}.",
                $"{ToTitle(optionLabel)} not updated");
        }

        return result;
    }

    private async Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteOptionAsync(
        string optionId,
        string optionLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(optionId))
        {
            return InvalidId(optionLabel, "deleted");
        }

        var result = await supportingTableAC.DeleteAsync(optionId.Trim(), cancellationToken);
        if (result.Success)
        {
            feedback.Success(
                $"{ToTitle(optionLabel)} deleted successfully.",
                $"{ToTitle(optionLabel)} deleted");
        }
        else
        {
            feedback.Error(
                result.ErrorMessage ?? $"Unable to delete {optionLabel}.",
                $"{ToTitle(optionLabel)} not deleted");
        }

        return result;
    }

    private ApiCallResult<SupportingTableSaveResultDTO> InvalidId(string optionLabel, string action)
    {
        var message = $"The selected {optionLabel} has no record ID.";
        feedback.Warning(message, $"{ToTitle(optionLabel)} not {action}");
        return ApiCallResult<SupportingTableSaveResultDTO>.Failure(
            System.Net.HttpStatusCode.BadRequest,
            message);
    }

    private static string ToTitle(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
