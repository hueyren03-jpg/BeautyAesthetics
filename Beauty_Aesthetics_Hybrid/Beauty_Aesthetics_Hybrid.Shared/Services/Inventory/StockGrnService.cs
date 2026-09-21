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
        // Same history request used by SenangRetails: branch scoped, recent date
        // range, one LoadProxy call, 500-row page.
        var result = await stockGrnAC.LoadProxyAsync(new StockGrnProxyRequestDTO
        {
            BranchID = string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim(),
            StartDate = DateTime.Today.AddYears(-2),
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
            PageNumber = 1,
            PageSize = 200
        }, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<StockGrnViewModel>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load GRN records.");
        }

        var grns = result.Value
            .Where(document => !document.IsVoid)
            .DistinctBy(
                document => First(
                    document.DocumentID,
                    document.DisplayCode,
                    $"{document.NumericCode}|{document.FinancialDate:O}"),
                StringComparer.OrdinalIgnoreCase)
            .Select(ToViewModel)
            .OrderByDescending(grn => grn.Date)
            .ThenByDescending(grn => grn.DisplayCode)
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
        var sourceLines = ResolveLines(grn);
        var subtotal = sourceLines.Sum(line => Math.Max(0, line.Quantity) * Math.Max(0, line.UnitCost));
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
        var sourceLines = ResolveLines(grn);
        var existingLines = envelope.DocumentLines.ToList();
        var existingById = existingLines
            .Where(line => !string.IsNullOrWhiteSpace(ReadString(line, "documentLineID")))
            .DistinctBy(line => ReadString(line, "documentLineID"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(line => ReadString(line, "documentLineID"), StringComparer.OrdinalIgnoreCase);
        var blankTemplate = isNewDocument ? existingLines.FirstOrDefault() : null;
        var updatedLines = new List<JsonObject>();

        for (var index = 0; index < sourceLines.Count; index++)
        {
            var source = sourceLines[index];
            JsonObject? existingLine = null;
            var hasExistingLine = !string.IsNullOrWhiteSpace(source.DocumentLineId) &&
                                  existingById.TryGetValue(source.DocumentLineId, out existingLine);
            var line = hasExistingLine
                ? existingLine!
                : blankTemplate?.DeepClone().AsObject() ?? new JsonObject();

            RemoveLegacyLineFields(line);
            var lineId = hasExistingLine ? source.DocumentLineId : Guid.NewGuid().ToString();
            var quantity = Math.Max(0, source.Quantity);
            var unitCost = Math.Max(0, source.UnitCost);
            var lineAmount = quantity * unitCost;

            line["isLoading"] = false;
            line["documentID"] = envelope.Document.DocumentID;
            line["documentLineID"] = lineId;
            line["ownerDocumentTypeID"] = envelope.Document.DocumentTypeID;
            line["lineOrder"] = index + 1;
            SetLine(line, "description", source.ProductName);
            SetLine(line, "itemName", source.ProductName);
            SetLine(line, "lineItemID", source.InventoryId);
            SetLine(line, "inventoryItemAccountID", source.InventoryId);
            SetLine(line, "lineItemDisplayCode", source.Sku);
            SetLine(line, "skuName", source.Sku);
            SetLine(line, "unitOfMeasurementID", source.UnitOfMeasurementId);
            line["quantity"] = quantity;
            line["adjustedQuantity"] = quantity;
            line["unitPrice"] = unitCost;
            line["cost"] = unitCost;
            line["subTotal"] = lineAmount;
            line["subTotalBeforeGST"] = lineAmount;
            line["amount"] = lineAmount;
            line["taxableAmount"] = lineAmount;
            line["taxAmount"] = 0;
            line["inventoryTypeID"] = source.InventoryTypeId <= 0 ? 1 : source.InventoryTypeId;
            line["branchID"] = normalizedBranch;
            line["editBranchID"] = normalizedBranch;
            line["financialDate"] = envelope.Document.FinancialDate;
            line["documentDisplayCode"] = envelope.Document.DisplayCode;
            line["currencyID"] = string.IsNullOrWhiteSpace(grn.TransactionCurrencyId) ? "MYR" : grn.TransactionCurrencyId;
            line["exchangeRate"] = grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate;
            line["convertedAmount"] = Math.Round(lineAmount * (grn.ExchangeRate <= 0 ? 1 : grn.ExchangeRate), 2);
            line["saveAction"] = isNewDocument || !hasExistingLine ? "Added" : "Changed";
            line["isDirty"] = true;
            updatedLines.Add(line);
        }

        foreach (var removedLine in existingLines.Except(updatedLines))
        {
            if (isNewDocument && ReferenceEquals(removedLine, blankTemplate))
            {
                continue;
            }

            removedLine["saveAction"] = "Deleted";
            removedLine["isDirty"] = true;
            updatedLines.Add(removedLine);
        }

        envelope.DocumentLines = updatedLines;
    }

    private static IReadOnlyList<StockGrnLineViewModel> ResolveLines(StockGrnViewModel grn)
    {
        if (grn.Lines.Count > 0)
        {
            return grn.Lines;
        }

        if (string.IsNullOrWhiteSpace(grn.InventoryId) && string.IsNullOrWhiteSpace(grn.ItemSku))
        {
            return Array.Empty<StockGrnLineViewModel>();
        }

        return new[]
        {
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
        };
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
        viewModel.Lines = envelope.DocumentLines
            .Where(line => !string.Equals(ReadString(line, "saveAction"), "Deleted", StringComparison.OrdinalIgnoreCase))
            .Select(line =>
            {
                var cost = ReadDecimal(line, "cost");
                if (cost <= 0)
                {
                    cost = ReadDecimal(line, "unitPrice", "UnitPrice");
                }

                return new StockGrnLineViewModel
                {
                    DocumentLineId = ReadString(line, "documentLineID"),
                    InventoryId = ReadString(line, "lineItemID", "inventoryItemAccountID", "inventoryID", "InventoryID"),
                    ProductName = ReadString(line, "itemName", "description", "Description"),
                    Sku = ReadString(line, "lineItemDisplayCode", "skuName", "sku", "SKU", "inventoryCode", "InventoryCode"),
                    Quantity = ReadDecimal(line, "quantity", "Quantity"),
                    UnitOfMeasurementId = ReadString(line, "unitOfMeasurementID", "uom", "UOM", "uom1", "UOM1"),
                    InventoryTypeId = Math.Max(1, (int)ReadDecimal(line, "inventoryTypeID")),
                    UnitCost = cost
                };
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId) || !string.IsNullOrWhiteSpace(line.Sku))
            .ToList();

        var firstLine = viewModel.Lines.FirstOrDefault();
        if (firstLine is not null)
        {
            viewModel.ItemName = firstLine.ProductName;
            viewModel.ItemSku = firstLine.Sku;
            viewModel.InventoryId = firstLine.InventoryId;
            viewModel.Uom = firstLine.UnitOfMeasurementId;
            viewModel.InventoryTypeId = firstLine.InventoryTypeId;
            viewModel.ReceivedQuantity = firstLine.Quantity;
            viewModel.OrderedQuantity = firstLine.Quantity;
            viewModel.UnitPrice = firstLine.UnitCost;
        }

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
