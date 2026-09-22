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

public sealed class StockGrnService : IStockGrnService
{
    private readonly StockGrnAC stockGrnAC;
    private readonly AppFeedbackService feedback;

    public StockGrnService(StockGrnAC stockGrnAC, AppFeedbackService feedback)
    {
        this.stockGrnAC = stockGrnAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<StockGrnViewModel>>> LoadGrnsAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return ApiCallResult<IReadOnlyList<StockGrnViewModel>>.Ok(
                HttpStatusCode.OK,
                Array.Empty<StockGrnViewModel>());
        }

        var result = await stockGrnAC.LoadProxyAsync(new StockGrnProxyRequestDTO
        {
            BranchID = branchId.Trim(),
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
            PageNumber = 1,
            PageSize = 500
        }, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<StockGrnViewModel>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load GRN records.");
        }

        var rows = result.Value
            .Where(record => !record.IsVoid)
            .OrderByDescending(record => record.FinancialDate)
            .Select(ToViewModel)
            .ToList();

        return ApiCallResult<IReadOnlyList<StockGrnViewModel>>.Ok(result.StatusCode, rows);
    }

    public async Task<ApiCallResult<StockGrnViewModel>> LoadGrnAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<StockGrnViewModel>.Failure(
                HttpStatusCode.BadRequest,
                "GRN document ID is missing.");
        }

        var result = await stockGrnAC.LoadRecordAsync(documentId.Trim(), cancellationToken);
        if (!result.Success || result.Value?.mobjDoc_Stock_GRN is null)
        {
            return ApiCallResult<StockGrnViewModel>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load the GRN.");
        }

        return ApiCallResult<StockGrnViewModel>.Ok(result.StatusCode, ToViewModel(result.Value));
    }

    public async Task<ApiCallResult<bool>> CreateGrnAsync(
        StockGrnViewModel grn,
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(grn);
        if (validationError is not null)
        {
            feedback.Warning(validationError, "Check Stock In");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var envelope = new Doc_Stock_GRN();
        ApplyDocument(envelope.mobjDoc_Stock_GRN, grn, branchId);
        ApplyLines(envelope, grn, branchId, isNewDocument: true);
        envelope.mobjDoc_Stock_GRN.SaveAction = EntityState.Added;
        envelope.mobjDoc_Stock_GRN.IsDirty = true;

        var result = ToBoolean(
            await stockGrnAC.CreateRecordAsync(envelope, cancellationToken),
            "Unable to create GRN.");

        if (result.Success)
        {
            feedback.Success("Stock In completed and GRN created successfully.", "Stock In completed");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to create GRN.", "Stock In failed");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> UpdateGrnAsync(
        StockGrnViewModel grn,
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(grn.DocumentId))
        {
            const string message = "GRN document ID is missing.";
            feedback.Warning(message, "GRN not updated");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, message);
        }

        var validationError = Validate(grn);
        if (validationError is not null)
        {
            feedback.Warning(validationError, "Check GRN changes");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var loadResult = await stockGrnAC.LoadRecordAsync(grn.DocumentId.Trim(), cancellationToken);
        if (!loadResult.Success || loadResult.Value?.mobjDoc_Stock_GRN is null)
        {
            var message = loadResult.ErrorMessage ?? "Unable to load GRN before updating.";
            feedback.Error(message, "GRN not updated");
            return ApiCallResult<bool>.Failure(loadResult.StatusCode, message);
        }

        var envelope = loadResult.Value;
        ApplyDocument(envelope.mobjDoc_Stock_GRN, grn, branchId);
        ApplyLines(envelope, grn, branchId, isNewDocument: false);
        envelope.mobjDoc_Stock_GRN.DocumentID = grn.DocumentId.Trim();
        envelope.mobjDoc_Stock_GRN.SaveAction = EntityState.Changed;
        envelope.mobjDoc_Stock_GRN.IsDirty = true;

        var result = ToBoolean(
            await stockGrnAC.UpdateRecordAsync(envelope, cancellationToken),
            "Unable to update GRN.");

        if (result.Success)
        {
            feedback.Success("GRN updated successfully.", "GRN updated");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to update GRN.", "GRN not updated");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> DeleteGrnAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            const string message = "GRN document ID is missing.";
            feedback.Warning(message, "GRN not deleted");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, message);
        }

        var result = ToBoolean(
            await stockGrnAC.DeleteAsync(documentId.Trim(), cancellationToken),
            "Unable to delete GRN.");

        if (result.Success)
        {
            feedback.Success("GRN deleted successfully.", "GRN deleted");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to delete GRN.", "GRN not deleted");
        }

        return result;
    }

    private static void ApplyDocument(
        Doc_Stock_GRNDM document,
        StockGrnViewModel grn,
        string branchId)
    {
        var normalizedBranch = NormalizeBranchId(
            string.IsNullOrWhiteSpace(grn.BranchId) ? branchId : grn.BranchId);
        var exchangeRate = grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate;
        var sourceLines = ResolveLines(grn);
        var subtotal = sourceLines.Sum(line =>
            Math.Max(0, line.Quantity) * Math.Max(0, line.UnitCost));
        var taxAmount = Math.Max(0, grn.TaxAmount);
        var totalBeforeTax = grn.IsTaxInclusive
            ? Math.Max(0, subtotal - taxAmount)
            : subtotal;
        var totalAfterTax = grn.Amount > 0
            ? grn.Amount
            : (grn.IsTaxInclusive ? subtotal : subtotal + taxAmount) + grn.RoundingAmount;

        document.DocumentTypeID = (int)EnumDocumentType.GRN;
        document.FriendlyDocumentName = "GRN";
        document.BranchID = normalizedBranch;
        document.EditBranchID = normalizedBranch;
        document.FinancialDate = grn.Date == default ? DateTime.Today : grn.Date;
        document.DisplayCode = NullIfWhiteSpace(grn.DisplayCode);
        document.AccountID = NullIfWhiteSpace(grn.AccountId);
        document.AccountName = NullIfWhiteSpace(grn.AccountName);
        document.PODocumentID = NullIfWhiteSpace(grn.PODocumentId);
        document.PODisplayCode = NullIfWhiteSpace(grn.PODisplayCode);
        document.OrderBranchID = NullIfWhiteSpace(grn.OrderBranchId);
        document.PaymentTermID = NullIfWhiteSpace(grn.PaymentTermId);
        document.PaymentTermName = NullIfWhiteSpace(grn.PaymentTermName);
        document.ReferenceNumber = NullIfWhiteSpace(grn.ReferenceNumber);
        document.Remarks = NullIfWhiteSpace(grn.Remarks);
        document.TransactionCurrencyID = NullIfWhiteSpace(grn.TransactionCurrencyId);
        document.LocalCurrencyID = NullIfWhiteSpace(grn.LocalCurrencyId);
        document.ExchangeRate = exchangeRate;
        document.TaxTypeID = NullIfWhiteSpace(grn.TaxTypeId);
        document.StockActivityType = NullIfWhiteSpace(grn.StockActivityType);
        document.PostingDate = grn.PostingDate == default
            ? document.FinancialDate
            : grn.PostingDate;
        document.IsPostingDateDifferent =
            document.PostingDate.Date != document.FinancialDate.Date;

        if (grn.PODate.HasValue)
        {
            document.POFinancialDate = grn.PODate.Value;
        }

        document.TotalBeforeTax = totalBeforeTax;
        document.TaxableAmount = totalBeforeTax;
        document.TaxAmount = taxAmount;
        document.RoundingAmount = grn.RoundingAmount;
        document.TotalAfterTax = totalAfterTax;
        document.LocalTotalBeforeTax = Math.Round(totalBeforeTax * exchangeRate, 2);
        document.LocalTaxableAmount = document.LocalTotalBeforeTax;
        document.LocalTaxAmount = Math.Round(taxAmount * exchangeRate, 2);
        document.LocalRoundingAmount = Math.Round(grn.RoundingAmount * exchangeRate, 2);
        document.LocalTotalAfterTax = Math.Round(totalAfterTax * exchangeRate, 2);
    }

    private static void ApplyLines(
        Doc_Stock_GRN envelope,
        StockGrnViewModel grn,
        string branchId,
        bool isNewDocument)
    {
        var document = envelope.mobjDoc_Stock_GRN ??
            throw new InvalidOperationException("GRN document header is missing.");
        var normalizedBranch = NormalizeBranchId(
            string.IsNullOrWhiteSpace(grn.BranchId) ? branchId : grn.BranchId);
        var sourceLines = ResolveLines(grn);
        var existingLines = envelope.lstDocumentLine?.ToList() ?? new List<DocumentLineTableDM>();
        var existingById = existingLines
            .Where(line => !string.IsNullOrWhiteSpace(line.DocumentLineID))
            .DistinctBy(line => line.DocumentLineID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(line => line.DocumentLineID, StringComparer.OrdinalIgnoreCase);
        var updatedLines = new List<DocumentLineTableDM>();
        var exchangeRate = grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate;

        for (var index = 0; index < sourceLines.Count; index++)
        {
            var source = sourceLines[index];
            DocumentLineTableDM? existingLine = null;
            var hasExistingLine =
                !string.IsNullOrWhiteSpace(source.DocumentLineId) &&
                existingById.TryGetValue(source.DocumentLineId, out existingLine);

            var line = hasExistingLine ? existingLine! : new DocumentLineTableDM();
            var quantity = Math.Max(0, source.Quantity);
            var unitCost = Math.Max(0, source.UnitCost);
            var lineAmount = quantity * unitCost;

            line.DocumentLineID = hasExistingLine ? source.DocumentLineId : string.Empty;
            line.DocumentID = document.DocumentID ?? string.Empty;
            line.OwnerDocumentTypeID = (int)EnumDocumentType.GRN;
            line.LineOrder = index + 1;
            line.Description = source.ProductName;
            line.ItemName = source.ProductName;
            line.LineItemID = source.InventoryId;
            line.InventoryItemAccountID = source.InventoryId;
            line.LineItemDisplayCode = source.Sku;
            line.SKUName = source.Sku;
            line.UnitOfMeasurementID = source.UnitOfMeasurementId;
            line.Quantity = quantity;
            line.AdjustedQuantity = quantity;
            line.UnitPrice = unitCost;
            line.Cost = unitCost;
            line.SubTotal = lineAmount;
            line.SubTotalBeforeGST = lineAmount;
            line.Amount = lineAmount;
            line.TaxableAmount = lineAmount;
            line.TaxAmount = 0;
            line.InventoryTypeID = source.InventoryTypeId <= 0 ? 1 : source.InventoryTypeId;
            line.SKUQuantity = 1;
            line.BranchID = normalizedBranch;
            line.EditBranchID = normalizedBranch;
            line.FinancialDate = document.FinancialDate;
            line.DocumentDisplayCode = document.DisplayCode;
            line.CurrencyID = string.IsNullOrWhiteSpace(grn.TransactionCurrencyId)
                ? "MYR"
                : grn.TransactionCurrencyId;
            line.ExchangeRate = exchangeRate;
            line.ConvertedAmount = Math.Round(lineAmount * exchangeRate, 2);
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

        envelope.lstDocumentLine =
            new ObservableCollection<DocumentLineTableDM>(updatedLines);
    }

    private static IReadOnlyList<StockGrnLineViewModel> ResolveLines(StockGrnViewModel grn)
    {
        if (grn.Lines.Count > 0)
        {
            return grn.Lines;
        }

        if (string.IsNullOrWhiteSpace(grn.InventoryId) &&
            string.IsNullOrWhiteSpace(grn.ItemSku))
        {
            return Array.Empty<StockGrnLineViewModel>();
        }

        return
        [
            new StockGrnLineViewModel
            {
                InventoryId = grn.InventoryId,
                ProductName = grn.ItemName,
                Sku = grn.ItemSku,
                Quantity = grn.ReceivedQuantity,
                UnitOfMeasurementId = grn.Uom,
                InventoryTypeId = grn.InventoryTypeId,
                UnitCost = grn.UnitPrice
            }
        ];
    }

    private static StockGrnViewModel ToViewModel(Doc_Stock_GRNDM document) => new()
    {
        DocumentId = document.DocumentID ?? string.Empty,
        BranchId = First(document.BranchID, document.EditBranchID),
        Date = document.FinancialDate == default ? DateTime.Today : document.FinancialDate,
        DisplayCode = First(document.DisplayCode, document.DocumentID),
        AccountId = document.AccountID ?? string.Empty,
        AccountName = document.AccountName ?? string.Empty,
        PODisplayCode = document.PODisplayCode ?? string.Empty,
        PODocumentId = document.PODocumentID ?? string.Empty,
        OrderBranchId = document.OrderBranchID ?? string.Empty,
        PaymentTermId = document.PaymentTermID ?? string.Empty,
        PaymentTermName = document.PaymentTermName ?? string.Empty,
        CreatedByDocumentId = document.CreatedByDocumentID ?? string.Empty,
        CreatedByDocumentDisplayCode = document.CreatedByDocumentDisplayCode ?? string.Empty,
        VerifyStatus = document.VerifyStatus ?? string.Empty,
        ReferenceNumber = document.ReferenceNumber ?? string.Empty,
        PODate = document.POFinancialDate == default ? null : document.POFinancialDate,
        PostingDate = document.PostingDate == default
            ? document.FinancialDate
            : document.PostingDate,
        IsPostingDateDifferent = document.IsPostingDateDifferent,
        Remarks = document.Remarks ?? string.Empty,
        Amount = document.TotalAfterTax != 0
            ? document.TotalAfterTax
            : document.LocalTotalAfterTax,
        TransactionCurrencyId = document.TransactionCurrencyID ?? string.Empty,
        LocalCurrencyId = document.LocalCurrencyID ?? string.Empty,
        ExchangeRate = document.ExchangeRate <= 0 ? 1 : document.ExchangeRate,
        TaxTypeId = document.TaxTypeID ?? string.Empty,
        TaxAmount = document.TaxAmount,
        RoundingAmount = document.RoundingAmount,
        StockActivityType = document.StockActivityType ?? string.Empty,
        IsLocked = document.IsLocked,
        IsVoid = document.IsVoid
    };

    private static StockGrnViewModel ToViewModel(Doc_Stock_GRN envelope)
    {
        var grn = envelope.mobjDoc_Stock_GRN is null
            ? new StockGrnViewModel()
            : ToViewModel(envelope.mobjDoc_Stock_GRN);

        grn.Lines = (envelope.lstDocumentLine ?? new ObservableCollection<DocumentLineTableDM>())
            .Where(line => line.SaveAction != EntityState.Deleted)
            .Select(line => new StockGrnLineViewModel
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
            .Where(line =>
                !string.IsNullOrWhiteSpace(line.ProductName) ||
                !string.IsNullOrWhiteSpace(line.InventoryId) ||
                !string.IsNullOrWhiteSpace(line.Sku) ||
                line.Quantity != 0 ||
                line.UnitCost != 0)
            .ToList();

        var firstLine = grn.Lines.FirstOrDefault();
        if (firstLine is not null)
        {
            grn.ItemName = firstLine.ProductName;
            grn.ItemSku = firstLine.Sku;
            grn.InventoryId = firstLine.InventoryId;
            grn.Uom = firstLine.UnitOfMeasurementId;
            grn.InventoryTypeId = firstLine.InventoryTypeId;
            grn.ReceivedQuantity = firstLine.Quantity;
            grn.OrderedQuantity = firstLine.Quantity;
            grn.UnitPrice = firstLine.UnitCost;
        }

        return grn;
    }

    private static string? Validate(StockGrnViewModel grn)
    {
        if (string.IsNullOrWhiteSpace(grn.BranchId))
            return "Receiving branch is required.";

        var lines = ResolveLines(grn);
        if (lines.Count == 0)
            return "Select at least one product.";

        if (lines.Any(line => string.IsNullOrWhiteSpace(line.InventoryId)))
            return "A selected product is missing its inventory ID.";

        if (lines.Any(line => line.Quantity <= 0))
            return "Received quantity must be greater than zero.";

        return null;
    }

    private static decimal FirstPositive(params decimal[] values) =>
        values.FirstOrDefault(value => value > 0);

    private static ApiCallResult<bool> ToBoolean(ApiCallResult<string> result, string fallback) =>
        result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? fallback);

    private static string NormalizeBranchId(string? branchId) =>
        string.IsNullOrWhiteSpace(branchId)
            ? "HQ"
            : branchId.Trim().ToUpperInvariant();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
