using System.Net;
using System.Globalization;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class StockGrnService : IStockGrnService
{
    private readonly StockGrnAC stockGrnAC;

    public StockGrnService(StockGrnAC stockGrnAC)
    {
        this.stockGrnAC = stockGrnAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<StockGrnViewModel>>> LoadGrnsAsync(
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        var request = new StockGrnProxyRequestDTO
        {
            BranchID = NormalizeBranchId(branchId),
            StartDate = DateTime.Today.AddYears(-2),
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
            PageNumber = 1,
            PageSize = 200
        };

        var result = await stockGrnAC.LoadProxyAsync(request, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<StockGrnViewModel>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load GRN records.");
        }

        var grns = result.Value
            .Where(document => !document.IsVoid)
            .Select(ToViewModel)
            .OrderByDescending(grn => grn.Date)
            .ThenBy(grn => grn.DisplayCode)
            .ToList();

        return ApiCallResult<IReadOnlyList<StockGrnViewModel>>.Ok(result.StatusCode, grns);
    }

    public async Task<ApiCallResult<bool>> CreateGrnAsync(
        StockGrnViewModel grn,
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        var templateResult = await stockGrnAC.LoadRecordAsync(string.Empty, cancellationToken);
        if (!templateResult.Success || templateResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                templateResult.StatusCode,
                templateResult.ErrorMessage ?? "Unable to prepare a new GRN.");
        }

        Apply(templateResult.Value.Document, grn, branchId);
        ApplyLines(templateResult.Value, grn, branchId, isNewDocument: true);
        templateResult.Value.Document.SaveAction = "Added";
        templateResult.Value.Document.IsDirty = true;

        var result = await stockGrnAC.CreateRecordAsync(templateResult.Value, cancellationToken);
        return ToBoolean(result, "Unable to create GRN.");
    }

    public async Task<ApiCallResult<StockGrnViewModel>> LoadGrnAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<StockGrnViewModel>.Failure(HttpStatusCode.BadRequest, "GRN document ID is missing.");
        }

        var result = await stockGrnAC.LoadRecordAsync(documentId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<StockGrnViewModel>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load the GRN.");
        }

        return ApiCallResult<StockGrnViewModel>.Ok(result.StatusCode, ToViewModel(result.Value));
    }

    public async Task<ApiCallResult<bool>> UpdateGrnAsync(
        StockGrnViewModel grn,
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(grn.DocumentId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "GRN document ID is missing.");
        }

        var loadResult = await stockGrnAC.LoadRecordAsync(grn.DocumentId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load GRN before updating.");
        }

        Apply(loadResult.Value.Document, grn, branchId);
        ApplyLines(loadResult.Value, grn, branchId, isNewDocument: false);
        loadResult.Value.Document.DocumentID = grn.DocumentId;
        loadResult.Value.Document.SaveAction = "Changed";
        loadResult.Value.Document.IsDirty = true;

        var result = await stockGrnAC.UpdateRecordAsync(loadResult.Value, cancellationToken);
        return ToBoolean(result, "Unable to update GRN.");
    }

    public async Task<ApiCallResult<bool>> DeleteGrnAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "GRN document ID is missing.");
        }

        var result = await stockGrnAC.DeleteAsync(documentId, cancellationToken);
        return ToBoolean(result, "Unable to delete GRN.");
    }

    private static void Apply(StockGrnDocumentDTO document, StockGrnViewModel grn, string branchId)
    {
        var normalizedBranch = NormalizeBranchId(string.IsNullOrWhiteSpace(grn.BranchId) ? branchId : grn.BranchId);
        var exchangeRate = grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate;
        var subtotal = Math.Max(0, grn.ReceivedQuantity * grn.UnitPrice);
        var taxAmount = Math.Max(0, grn.TaxAmount);
        var totalBeforeTax = grn.IsTaxInclusive ? Math.Max(0, subtotal - taxAmount) : subtotal;
        var totalAfterTax = grn.Amount > 0
            ? grn.Amount
            : (grn.IsTaxInclusive ? subtotal : subtotal + taxAmount) + grn.RoundingAmount;

        document.DocumentTypeID = 51;
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
        document.PostingDate = grn.PostingDate == default ? document.FinancialDate : grn.PostingDate;
        document.IsPostingDateDifferent = document.PostingDate.Date != document.FinancialDate.Date;
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
        StockGrnEnvelopeDTO envelope,
        StockGrnViewModel grn,
        string branchId,
        bool isNewDocument)
    {
        var normalizedBranch = NormalizeBranchId(string.IsNullOrWhiteSpace(grn.BranchId) ? branchId : grn.BranchId);
        var quantity = grn.ReceivedQuantity > 0 ? grn.ReceivedQuantity : 1;
        var unitPrice = grn.UnitPrice >= 0 ? grn.UnitPrice : 0;
        var subtotal = Math.Max(0, quantity * unitPrice);
        var taxAmount = Math.Max(0, grn.TaxAmount);
        var taxableAmount = grn.IsTaxInclusive ? Math.Max(0, subtotal - taxAmount) : subtotal;
        var amount = grn.Amount > 0
            ? grn.Amount
            : (grn.IsTaxInclusive ? subtotal : subtotal + taxAmount) + grn.RoundingAmount;
        var lineSaveAction = isNewDocument ? "Added" : "Changed";

        if (envelope.DocumentLines.Count > 0)
        {
            var firstLine = envelope.DocumentLines[0];
            RemoveLegacyLineFields(firstLine);
            firstLine["documentID"] = envelope.Document.DocumentID;
            firstLine["ownerDocumentTypeID"] = envelope.Document.DocumentTypeID;
            firstLine["lineOrder"] = 1;
            SetLine(firstLine, "description", grn.ItemName);
            SetLine(firstLine, "itemName", grn.ItemName);
            SetLine(firstLine, "lineItemID", grn.InventoryId);
            SetLine(firstLine, "inventoryItemAccountID", grn.InventoryId);
            SetLine(firstLine, "lineItemDisplayCode", grn.ItemSku);
            SetLine(firstLine, "skuName", grn.ItemSku);
            SetLine(firstLine, "unitOfMeasurementID", grn.Uom);
            SetLine(firstLine, "orderDocumentLineID", grn.OrderDocumentLineId);
            SetLine(firstLine, "sourceDocumentLineID", grn.SourceDocumentLineId);
            firstLine["quantity"] = quantity;
            firstLine["adjustedQuantity"] = quantity;
            firstLine["unitPrice"] = unitPrice;
            firstLine["subTotal"] = subtotal;
            firstLine["amount"] = amount;
            firstLine["taxableAmount"] = taxableAmount;
            firstLine["subTotalBeforeGST"] = taxableAmount;
            firstLine["taxAmount"] = taxAmount;
            firstLine["taxPercentage"] = Math.Max(0, grn.TaxPercentage);
            firstLine["isTaxInclusive"] = grn.IsTaxInclusive;
            firstLine["isPurchaseTax"] = true;
            SetLine(firstLine, "taxCodeID", grn.TaxCodeId);
            SetLine(firstLine, "gstTypeID", grn.GstTypeId);
            SetLine(firstLine, "batchNo", grn.BatchNumber);
            SetLine(firstLine, "serialNo", grn.SerialNumber);
            firstLine["branchID"] = normalizedBranch;
            firstLine["editBranchID"] = normalizedBranch;
            firstLine["financialDate"] = envelope.Document.FinancialDate;
            firstLine["documentDisplayCode"] = envelope.Document.DisplayCode;
            firstLine["inventoryTypeID"] = grn.InventoryTypeId;
            firstLine["currencyID"] = grn.TransactionCurrencyId;
            firstLine["exchangeRate"] = grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate;
            firstLine["convertedAmount"] = Math.Round(amount * (grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate), 2);
            firstLine["saveAction"] = lineSaveAction;
            firstLine["isDirty"] = true;
        }
        else
        {
            var lineId = Guid.NewGuid().ToString();
            var line = new JsonObject
            {
                ["isLoading"] = false,
                ["documentID"] = envelope.Document.DocumentID,
                ["documentLineID"] = lineId,
                ["ownerDocumentTypeID"] = envelope.Document.DocumentTypeID,
                ["lineOrder"] = 1,
                ["description"] = grn.ItemName,
                ["itemName"] = grn.ItemName,
                ["lineItemID"] = grn.InventoryId,
                ["inventoryItemAccountID"] = grn.InventoryId,
                ["lineItemDisplayCode"] = grn.ItemSku,
                ["skuName"] = grn.ItemSku,
                ["unitOfMeasurementID"] = grn.Uom,
                ["orderDocumentLineID"] = grn.OrderDocumentLineId,
                ["sourceDocumentLineID"] = grn.SourceDocumentLineId,
                ["quantity"] = quantity,
                ["adjustedQuantity"] = quantity,
                ["unitPrice"] = unitPrice,
                ["subTotal"] = subtotal,
                ["amount"] = amount,
                ["taxableAmount"] = taxableAmount,
                ["subTotalBeforeGST"] = taxableAmount,
                ["taxAmount"] = taxAmount,
                ["taxPercentage"] = Math.Max(0, grn.TaxPercentage),
                ["isTaxInclusive"] = grn.IsTaxInclusive,
                ["isPurchaseTax"] = true,
                ["taxCodeID"] = grn.TaxCodeId,
                ["gstTypeID"] = grn.GstTypeId,
                ["batchNo"] = grn.BatchNumber,
                ["serialNo"] = grn.SerialNumber,
                ["branchID"] = normalizedBranch,
                ["editBranchID"] = normalizedBranch,
                ["financialDate"] = envelope.Document.FinancialDate,
                ["documentDisplayCode"] = envelope.Document.DisplayCode,
                ["inventoryTypeID"] = grn.InventoryTypeId,
                ["currencyID"] = grn.TransactionCurrencyId,
                ["exchangeRate"] = grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate,
                ["convertedAmount"] = Math.Round(amount * (grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate), 2),
                ["saveAction"] = "Added",
                ["isDirty"] = true
            };
            envelope.DocumentLines.Add(line);
        }
    }

    private static StockGrnViewModel ToViewModel(StockGrnDocumentDTO document)
    {
        return new StockGrnViewModel
        {
            DocumentId = document.DocumentID ?? string.Empty,
            BranchId = document.BranchID ?? document.EditBranchID ?? string.Empty,
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
            PostingDate = document.PostingDate == default ? document.FinancialDate : document.PostingDate,
            IsPostingDateDifferent = document.IsPostingDateDifferent,
            Remarks = document.Remarks ?? string.Empty,
            Amount = document.TotalAfterTax != 0 ? document.TotalAfterTax : document.LocalTotalAfterTax,
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
    }

    private static StockGrnViewModel ToViewModel(StockGrnEnvelopeDTO envelope)
    {
        var viewModel = ToViewModel(envelope.Document);
        var line = envelope.DocumentLines.FirstOrDefault();
        if (line is null)
        {
            return viewModel;
        }

        viewModel.ItemName = ReadString(line, "itemName", "description", "Description");
        viewModel.ItemSku = ReadString(line, "lineItemDisplayCode", "skuName", "sku", "SKU", "inventoryCode", "InventoryCode");
        viewModel.InventoryId = ReadString(line, "lineItemID", "inventoryItemAccountID", "inventoryID", "InventoryID");
        viewModel.Uom = ReadString(line, "unitOfMeasurementID", "uom", "UOM", "uom1", "UOM1");
        viewModel.OrderDocumentLineId = ReadString(line, "orderDocumentLineID");
        viewModel.SourceDocumentLineId = ReadString(line, "sourceDocumentLineID");
        viewModel.InventoryTypeId = (int)ReadDecimal(line, "inventoryTypeID");
        viewModel.ReceivedQuantity = ReadDecimal(line, "quantity", "Quantity");
        viewModel.OrderedQuantity = ReadDecimal(line, "orderedQuantity", "OrderedQuantity");
        if (viewModel.OrderedQuantity <= 0)
        {
            viewModel.OrderedQuantity = viewModel.ReceivedQuantity;
        }
        viewModel.UnitPrice = ReadDecimal(line, "unitPrice", "UnitPrice");
        viewModel.TaxCodeId = ReadString(line, "taxCodeID");
        viewModel.GstTypeId = ReadString(line, "gstTypeID");
        viewModel.TaxPercentage = ReadDecimal(line, "taxPercentage");
        if (viewModel.TaxAmount == 0)
        {
            viewModel.TaxAmount = ReadDecimal(line, "taxAmount");
        }
        viewModel.IsTaxInclusive = ReadBool(line, "isTaxInclusive");
        viewModel.BatchNumber = ReadString(line, "batchNo");
        viewModel.SerialNumber = ReadString(line, "serialNo");
        return viewModel;
    }

    private static void SetLine(JsonObject line, string name, string value)
    {
        line[name] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void RemoveLegacyLineFields(JsonObject line)
    {
        foreach (var name in new[]
        {
            "inventoryID", "InventoryID", "sku", "SKU", "uom", "UOM",
            "Quantity", "UnitPrice", "SubTotal", "BranchID", "SaveAction", "IsDirty"
        })
        {
            line.Remove(name);
        }
    }

    private static string ReadString(JsonObject line, params string[] names)
    {
        foreach (var name in names)
        {
            if (line.TryGetPropertyValue(name, out var node) && node is not null)
            {
                var value = node.ToString().Trim('"');
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return string.Empty;
    }

    private static decimal ReadDecimal(JsonObject line, params string[] names)
    {
        var text = ReadString(line, names);
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var current) ? current : 0;
    }

    private static bool ReadBool(JsonObject line, params string[] names)
    {
        var text = ReadString(line, names);
        return bool.TryParse(text, out var value) && value;
    }

    private static ApiCallResult<bool> ToBoolean(ApiCallResult<string> result, string fallback)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallback);
    }

    private static string NormalizeBranchId(string branchId) =>
        string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
