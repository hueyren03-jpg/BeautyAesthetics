using System.Collections.ObjectModel;
using System.Net;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.Enum;
using EBI.UC;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class StockGinService : IStockGinService
{
    private readonly StockGinAC stockGinAC;
    private readonly AppFeedbackService feedback;

    public StockGinService(StockGinAC stockGinAC, AppFeedbackService feedback)
    {
        this.stockGinAC = stockGinAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<StockGinViewModel>>> LoadGinsAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return ApiCallResult<IReadOnlyList<StockGinViewModel>>.Ok(
                HttpStatusCode.OK,
                Array.Empty<StockGinViewModel>());
        }

        var result = await stockGinAC.LoadProxyAsync(new StockGinProxyRequestDTO
        {
            BranchID = branchId.Trim(),
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
            PageNumber = 1,
            PageSize = 500
        }, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<StockGinViewModel>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load GIN records.");
        }

        var rows = result.Value
            .Where(record => !record.IsVoid)
            .OrderByDescending(record => record.FinancialDate)
            .Select(ToViewModel)
            .ToList();

        return ApiCallResult<IReadOnlyList<StockGinViewModel>>.Ok(result.StatusCode, rows);
    }

    public async Task<ApiCallResult<StockGinViewModel>> LoadGinAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<StockGinViewModel>.Failure(
                HttpStatusCode.BadRequest,
                "GIN document ID is missing.");
        }

        var result = await stockGinAC.LoadRecordAsync(documentId.Trim(), cancellationToken);
        if (!result.Success || result.Value?.mobjDoc_Stock_GIN is null)
        {
            return ApiCallResult<StockGinViewModel>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "The GIN record was not found.");
        }

        return ApiCallResult<StockGinViewModel>.Ok(result.StatusCode, ToViewModel(result.Value));
    }

    public async Task<ApiCallResult<bool>> CreateGinAsync(
        StockGinViewModel gin,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(gin);
        if (validationError is not null)
        {
            feedback.Warning(validationError, "Check Stock Out");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        // EBIUC now supplies the correct GIN header and document-line container.
        // Leave document and line IDs empty so the API can generate them.
        var envelope = new Doc_Stock_GIN();
        ApplyDocument(envelope.mobjDoc_Stock_GIN, gin);
        ApplyLines(envelope, gin, isNewDocument: true);
        envelope.mobjDoc_Stock_GIN.SaveAction = EntityState.Added;
        envelope.mobjDoc_Stock_GIN.IsDirty = true;

        var result = ToBoolean(
            await stockGinAC.CreateRecordAsync(envelope, cancellationToken),
            "Unable to create GIN.");

        if (result.Success)
        {
            feedback.Success("Stock Out completed and GIN created successfully.", "Stock Out completed");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to create GIN.", "Stock Out failed");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> UpdateGinAsync(
        StockGinViewModel gin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gin.DocumentId))
        {
            const string message = "GIN document ID is missing.";
            feedback.Warning(message, "GIN not updated");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, message);
        }

        var validationError = Validate(gin);
        if (validationError is not null)
        {
            feedback.Warning(validationError, "Check GIN changes");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var loadResult = await stockGinAC.LoadRecordAsync(gin.DocumentId.Trim(), cancellationToken);
        if (!loadResult.Success || loadResult.Value?.mobjDoc_Stock_GIN is null)
        {
            var message = loadResult.ErrorMessage ?? "Unable to load the GIN before updating.";
            feedback.Error(message, "GIN not updated");
            return ApiCallResult<bool>.Failure(loadResult.StatusCode, message);
        }

        var envelope = loadResult.Value;
        ApplyDocument(envelope.mobjDoc_Stock_GIN, gin);
        ApplyLines(envelope, gin, isNewDocument: false);
        envelope.mobjDoc_Stock_GIN.DocumentID = gin.DocumentId.Trim();
        envelope.mobjDoc_Stock_GIN.SaveAction = EntityState.Changed;
        envelope.mobjDoc_Stock_GIN.IsDirty = true;

        var result = ToBoolean(
            await stockGinAC.UpdateRecordAsync(envelope, cancellationToken),
            "Unable to update GIN.");

        if (result.Success)
        {
            feedback.Success("GIN updated successfully.", "GIN updated");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to update GIN.", "GIN not updated");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> DeleteGinAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            const string message = "GIN document ID is missing.";
            feedback.Warning(message, "GIN not deleted");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, message);
        }

        var result = ToBoolean(
            await stockGinAC.DeleteAsync(documentId.Trim(), cancellationToken),
            "Unable to delete GIN.");

        if (result.Success)
        {
            feedback.Success("GIN deleted successfully.", "GIN deleted");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to delete GIN.", "GIN not deleted");
        }

        return result;
    }

    private static void ApplyDocument(Doc_Stock_GINDM document, StockGinViewModel gin)
    {
        var branchId = NormalizeBranchId(gin.BranchId);
        var groupId = NormalizeGroupId(gin.GroupId, branchId);
        var date = gin.Date == default ? DateTime.Today : gin.Date;
        var total = gin.Lines.Sum(line => Math.Max(0, line.Quantity) * Math.Max(0, line.UnitCost));

        document.DocumentTypeID = (int)EnumDocumentType.GIN;
        document.FriendlyDocumentName = "GIN";
        document.BranchID = branchId;
        document.EditBranchID = branchId;
        document.GroupID = groupId;
        document.FinancialDate = date;
        document.PostingDate = date;
        document.IsPostingDateDifferent = false;
        document.DisplayCode = NullIfWhiteSpace(gin.DisplayCode);
        document.StockActivityType = NullIfWhiteSpace(gin.IssueType);
        document.AccountID = NullIfWhiteSpace(gin.AccountId);
        document.AccountName = NullIfWhiteSpace(gin.AccountName);
        document.OrderBranchID = NullIfWhiteSpace(gin.OrderBranchId);
        document.ReferenceNumber = NullIfWhiteSpace(gin.ReferenceNumber);
        document.Remarks = NullIfWhiteSpace(gin.Remarks);
        document.ExchangeRate = document.ExchangeRate <= 0 ? 1 : document.ExchangeRate;
        document.TotalBeforeTax = total;
        document.TaxableAmount = total;
        document.TaxAmount = 0;
        document.RoundingAmount = 0;
        document.TotalAfterTax = total;
        document.LocalTotalBeforeTax = total;
        document.LocalTaxableAmount = total;
        document.LocalTaxAmount = 0;
        document.LocalRoundingAmount = 0;
        document.LocalTotalAfterTax = total;
    }

    private static void ApplyLines(Doc_Stock_GIN envelope, StockGinViewModel gin, bool isNewDocument)
    {
        var document = envelope.mobjDoc_Stock_GIN ??
            throw new InvalidOperationException("GIN document header is missing.");
        var existingLines = envelope.lstDocumentLine?.ToList() ?? new List<DocumentLineTableDM>();
        var existingById = existingLines
            .Where(line => !string.IsNullOrWhiteSpace(line.DocumentLineID))
            .DistinctBy(line => line.DocumentLineID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(line => line.DocumentLineID, StringComparer.OrdinalIgnoreCase);
        var updatedLines = new List<DocumentLineTableDM>();
        var branchId = NormalizeBranchId(gin.BranchId);
        var groupId = NormalizeGroupId(gin.GroupId, branchId);
        var date = gin.Date == default ? DateTime.Today : gin.Date;

        for (var index = 0; index < gin.Lines.Count; index++)
        {
            var source = gin.Lines[index];
            DocumentLineTableDM? existingLine = null;
            var hasExistingLine = !string.IsNullOrWhiteSpace(source.DocumentLineId) &&
                                  existingById.TryGetValue(source.DocumentLineId, out existingLine);
            var line = hasExistingLine ? existingLine! : new DocumentLineTableDM();
            var quantity = Math.Max(0, source.Quantity);
            var unitCost = Math.Max(0, source.UnitCost);
            var amount = quantity * unitCost;

            line.DocumentLineID = hasExistingLine ? source.DocumentLineId : string.Empty;
            line.DocumentID = document.DocumentID ?? string.Empty;
            line.OwnerDocumentTypeID = (int)EnumDocumentType.GIN;
            line.LineOrder = index + 1;
            line.LineItemID = source.InventoryId;
            line.InventoryItemAccountID = source.InventoryId;
            line.LineItemDisplayCode = source.Sku;
            line.SKUName = source.Sku;
            line.Description = source.ProductName;
            line.ItemName = source.ProductName;
            line.Quantity = quantity;
            line.AdjustedQuantity = quantity;
            line.UnitOfMeasurementID = source.UnitOfMeasurementId;
            line.InventoryTypeID = source.InventoryTypeId <= 0 ? 1 : source.InventoryTypeId;
            line.UnitPrice = unitCost;
            line.Cost = unitCost;
            line.SubTotal = amount;
            line.SubTotalBeforeGST = amount;
            line.Amount = amount;
            line.TaxableAmount = amount;
            line.SKUQuantity = 1;
            line.BranchID = branchId;
            line.EditBranchID = branchId;
            line.GroupID = groupId;
            line.FinancialDate = date;
            line.DocumentDisplayCode = document.DisplayCode;
            line.SaveAction = isNewDocument || !hasExistingLine
                ? EntityState.Added
                : EntityState.Changed;
            line.IsDirty = true;
            updatedLines.Add(line);
        }

        foreach (var removedLine in existingLines.Except(updatedLines))
        {
            removedLine.SaveAction = EntityState.Deleted;
            removedLine.IsDirty = true;
            updatedLines.Add(removedLine);
        }

        envelope.lstDocumentLine = new ObservableCollection<DocumentLineTableDM>(updatedLines);
    }

    private static StockGinViewModel ToViewModel(Doc_Stock_GINDM document) => new()
    {
        DocumentId = document.DocumentID ?? string.Empty,
        DisplayCode = First(document.DisplayCode, document.DocumentID),
        BranchId = First(document.BranchID, document.EditBranchID),
        GroupId = document.GroupID ?? string.Empty,
        Date = document.FinancialDate == default ? DateTime.Today : document.FinancialDate,
        IssueType = document.StockActivityType ?? string.Empty,
        AccountId = document.AccountID ?? string.Empty,
        AccountName = document.AccountName ?? string.Empty,
        OrderBranchId = document.OrderBranchID ?? string.Empty,
        ReferenceNumber = document.ReferenceNumber ?? string.Empty,
        Remarks = document.Remarks ?? string.Empty,
        CreatedByDocumentTypeId = document.CreatedByDocumentTypeID,
        CreatedByDocumentTypeName = document.CreatedByDocumentTypeName ?? string.Empty,
        CreatedByDocumentId = document.CreatedByDocumentID ?? string.Empty,
        CreatedByDocumentDisplayCode = document.CreatedByDocumentDisplayCode ?? string.Empty,
        IsLocked = document.IsLocked,
        IsVoid = document.IsVoid,
        TotalAmount = document.TotalAfterTax != 0 ? document.TotalAfterTax : document.LocalTotalAfterTax
    };

    private static StockGinViewModel ToViewModel(Doc_Stock_GIN envelope)
    {
        var gin = envelope.mobjDoc_Stock_GIN is null
            ? new StockGinViewModel()
            : ToViewModel(envelope.mobjDoc_Stock_GIN);

        gin.Lines = (envelope.lstDocumentLine ?? new ObservableCollection<DocumentLineTableDM>())
            .Where(line => line.SaveAction != EntityState.Deleted)
            .Select(line => new StockGinLineViewModel
            {
                DocumentLineId = line.DocumentLineID ?? string.Empty,
                InventoryId = First(line.LineItemID, line.InventoryItemAccountID),
                ProductName = First(line.ItemName, line.Description),
                Sku = First(line.LineItemDisplayCode, line.SKUName),
                Quantity = line.Quantity != 0 ? line.Quantity : line.AdjustedQuantity,
                UnitOfMeasurementId = line.UnitOfMeasurementID ?? string.Empty,
                InventoryTypeId = Math.Max(1, line.InventoryTypeID),
                UnitCost = FirstPositive(line.Cost, line.UnitPrice)
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId) ||
                           !string.IsNullOrWhiteSpace(line.Sku))
            .ToList();

        return gin;
    }

    private static string? Validate(StockGinViewModel gin)
    {
        if (string.IsNullOrWhiteSpace(gin.BranchId)) return "Issuing branch is required.";
        if (string.IsNullOrWhiteSpace(gin.IssueType)) return "Select an issue purpose.";
        if (string.Equals(gin.IssueType, "Supplier Return", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(gin.AccountId)) return "Select the supplier receiving the returned stock.";
        if (gin.Lines.Count == 0) return "Select at least one product.";
        if (gin.Lines.Any(line => string.IsNullOrWhiteSpace(line.InventoryId)))
            return "A selected product is missing its inventory ID.";
        if (gin.Lines.Any(line => line.Quantity <= 0)) return "Issue quantity must be greater than zero.";
        return null;
    }

    private static decimal FirstPositive(params decimal[] values) =>
        values.FirstOrDefault(value => value > 0);

    private static ApiCallResult<bool> ToBoolean(ApiCallResult<string> result, string fallback) =>
        result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallback);

    private static string NormalizeBranchId(string? branchId) =>
        string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();

    private static string NormalizeGroupId(string? groupId, string branchId) =>
        string.IsNullOrWhiteSpace(groupId) ? branchId : groupId.Trim();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
