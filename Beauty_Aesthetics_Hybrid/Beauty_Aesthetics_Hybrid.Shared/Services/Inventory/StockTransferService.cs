using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class StockTransferService : IStockTransferService
{
    private readonly StockTransferAC stockTransferAC;

    public StockTransferService(StockTransferAC stockTransferAC)
    {
        this.stockTransferAC = stockTransferAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<StockTransferViewModel>>> LoadTransfersAsync(
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 200;
        const int maximumPages = 100;
        var documents = new List<StockTransferDocumentDTO>();
        string? previousPageSignature = null;
        var lastStatusCode = HttpStatusCode.OK;

        for (var pageNumber = 1; pageNumber <= maximumPages; pageNumber++)
        {
            var request = new StockTransferProxyRequestDTO
            {
                BranchID = NormalizeBranchId(branchId),
                StartDate = new DateTime(1900, 1, 1),
                EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await stockTransferAC.LoadProxyAsync(request, cancellationToken);
            lastStatusCode = result.StatusCode;
            if (!result.Success || result.Value is null)
            {
                return ApiCallResult<IReadOnlyList<StockTransferViewModel>>.Failure(
                    result.StatusCode,
                    result.ErrorMessage ?? "Unable to load stock transfers.");
            }

            var page = result.Value;
            var signature = string.Join('|', page.Select(TransferKey));
            if (pageNumber > 1 && string.Equals(signature, previousPageSignature, StringComparison.Ordinal))
            {
                break;
            }

            documents.AddRange(page);
            previousPageSignature = signature;
            if (page.Count < pageSize)
            {
                break;
            }
        }

        var transfers = documents
            .DistinctBy(document => TransferKey(document), StringComparer.OrdinalIgnoreCase)
            .Where(document => !document.IsVoid)
            .Select(ToViewModel)
            .OrderByDescending(transfer => transfer.Date)
            .ThenBy(transfer => transfer.DisplayCode)
            .ToList();

        return ApiCallResult<IReadOnlyList<StockTransferViewModel>>.Ok(lastStatusCode, transfers);
    }

    public async Task<ApiCallResult<StockTransferViewModel>> LoadTransferAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<StockTransferViewModel>.Failure(
                HttpStatusCode.BadRequest,
                "Stock transfer document ID is missing.");
        }

        var result = await stockTransferAC.LoadRecordAsync(documentId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<StockTransferViewModel>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load stock transfer details.");
        }

        var transfer = ToViewModel(result.Value.Document);
        transfer.Lines = result.Value.DocumentLines.Select(ToLineViewModel).ToList();
        return ApiCallResult<StockTransferViewModel>.Ok(result.StatusCode, transfer);
    }

    public async Task<ApiCallResult<bool>> CreateTransferAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(transfer);
        if (validationError is not null)
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var templateResult = await stockTransferAC.LoadRecordAsync(string.Empty, cancellationToken);
        if (!templateResult.Success || templateResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                templateResult.StatusCode,
                templateResult.ErrorMessage ?? "Unable to prepare a new stock transfer.");
        }

        HashSet<string>? existingTransferIds = null;
        if (string.IsNullOrWhiteSpace(templateResult.Value.Document.DocumentID))
        {
            var beforeCreate = await LoadTransfersAsync(transfer.FromBranchId, cancellationToken);
            if (beforeCreate.Success && beforeCreate.Value is not null)
            {
                existingTransferIds = beforeCreate.Value
                    .Where(item => !string.IsNullOrWhiteSpace(item.DocumentId))
                    .Select(item => item.DocumentId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        ApplyDocument(templateResult.Value.Document, transfer);
        ApplyLines(templateResult.Value, transfer, isNew: true);
        templateResult.Value.Document.SaveAction = "Added";
        templateResult.Value.Document.IsDirty = true;

        var createResult = await stockTransferAC.CreateRecordAsync(templateResult.Value, cancellationToken);
        if (!createResult.Success)
        {
            return ApiCallResult<bool>.Failure(
                createResult.StatusCode,
                createResult.ErrorMessage ?? "Unable to create stock transfer.");
        }

        transfer.DocumentId = First(
            templateResult.Value.Document.DocumentID,
            LooksLikeDocumentId(createResult.Value) ? createResult.Value : null,
            transfer.DocumentId);
        transfer.DocumentTypeId = templateResult.Value.Document.DocumentTypeID;
        transfer.DisplayCode = First(
            templateResult.Value.Document.DisplayCode,
            templateResult.Value.Document.ReferenceNumber,
            transfer.DisplayCode,
            transfer.DocumentId);

        if (!string.IsNullOrWhiteSpace(transfer.DocumentId))
        {
            var createdRecord = await stockTransferAC.LoadRecordAsync(transfer.DocumentId, cancellationToken);
            if (createdRecord.Success && createdRecord.Value is not null)
            {
                ApplyCreatedIdentity(transfer, createdRecord.Value.Document);
                transfer.Lines = createdRecord.Value.DocumentLines.Count > 0
                    ? createdRecord.Value.DocumentLines.Select(ToLineViewModel).ToList()
                    : transfer.Lines;
            }
            else if (string.IsNullOrWhiteSpace(templateResult.Value.Document.DocumentID))
            {
                transfer.DocumentId = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(transfer.DocumentId) && existingTransferIds is not null)
        {
            var resolvedTransfer = await ResolveCreatedTransferAsync(
                transfer,
                existingTransferIds,
                cancellationToken);

            if (resolvedTransfer is not null)
            {
                transfer.DocumentId = resolvedTransfer.DocumentId;
                transfer.DocumentTypeId = resolvedTransfer.DocumentTypeId;
                transfer.DisplayCode = resolvedTransfer.DisplayCode;
                transfer.Lines = resolvedTransfer.Lines;
            }
        }

        if (string.IsNullOrWhiteSpace(transfer.DocumentId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.Conflict,
                "Stock Transfer was created, but its document ID could not be resolved safely.");
        }

        transfer.Status = "In Transit";
        return ApiCallResult<bool>.Ok(createResult.StatusCode, true);
    }

    public async Task<ApiCallResult<bool>> UpdateTransferAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transfer.DocumentId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "Stock transfer document ID is missing.");
        }

        var validationError = Validate(transfer);
        if (validationError is not null)
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var loadResult = await stockTransferAC.LoadRecordAsync(transfer.DocumentId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load stock transfer before updating.");
        }

        ApplyDocument(loadResult.Value.Document, transfer);
        ApplyLines(loadResult.Value, transfer, isNew: false);
        loadResult.Value.Document.DocumentID = transfer.DocumentId;
        loadResult.Value.Document.SaveAction = "Changed";
        loadResult.Value.Document.IsDirty = true;

        return ToBoolean(
            await stockTransferAC.UpdateRecordAsync(loadResult.Value, cancellationToken),
            "Unable to update stock transfer.");
    }

    public async Task<ApiCallResult<bool>> DeleteTransferAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "Stock transfer document ID is missing.");
        }

        return ToBoolean(
            await stockTransferAC.DeleteAsync(documentId, cancellationToken),
            "Unable to delete stock transfer.");
    }

    private async Task<StockTransferViewModel?> ResolveCreatedTransferAsync(
        StockTransferViewModel requestedTransfer,
        IReadOnlySet<string> existingTransferIds,
        CancellationToken cancellationToken)
    {
        var afterCreate = await LoadTransfersAsync(requestedTransfer.FromBranchId, cancellationToken);
        if (!afterCreate.Success || afterCreate.Value is null)
        {
            return null;
        }

        var candidates = afterCreate.Value
            .Where(candidate =>
                !string.IsNullOrWhiteSpace(candidate.DocumentId) &&
                !existingTransferIds.Contains(candidate.DocumentId) &&
                SameKey(candidate.FromBranchId, requestedTransfer.FromBranchId) &&
                SameKey(candidate.ToBranchId, requestedTransfer.ToBranchId) &&
                candidate.Date.Date == requestedTransfer.Date.Date &&
                SameText(candidate.Remarks, requestedTransfer.Remarks))
            .ToList();

        var verifiedCandidates = new List<StockTransferViewModel>();
        foreach (var candidate in candidates)
        {
            var detailResult = await LoadTransferAsync(candidate.DocumentId, cancellationToken);
            if (!detailResult.Success || detailResult.Value is null)
            {
                continue;
            }

            var detail = detailResult.Value;
            if (SameKey(detail.FromBranchId, requestedTransfer.FromBranchId) &&
                SameKey(detail.ToBranchId, requestedTransfer.ToBranchId) &&
                detail.Date.Date == requestedTransfer.Date.Date &&
                SameText(detail.Remarks, requestedTransfer.Remarks) &&
                TransferLinesMatch(detail.Lines, requestedTransfer.Lines))
            {
                verifiedCandidates.Add(detail);
            }
        }

        return verifiedCandidates.Count == 1 ? verifiedCandidates[0] : null;
    }

    private static void ApplyDocument(StockTransferDocumentDTO document, StockTransferViewModel transfer)
    {
        var fromBranch = NormalizeBranchId(transfer.FromBranchId);
        var toBranch = NormalizeBranchId(transfer.ToBranchId);

        document.FriendlyDocumentName = string.IsNullOrWhiteSpace(document.FriendlyDocumentName)
            ? "Stock Transfer"
            : document.FriendlyDocumentName;
        document.BranchID = fromBranch;
        document.EditBranchID = fromBranch;
        document.OrderBranchID = toBranch;
        document.FromBranchID = fromBranch;
        document.ToBranchID = toBranch;
        document.FromBranch = First(transfer.FromBranchName, transfer.FromBranchId);
        document.ToBranch = First(transfer.ToBranchName, transfer.ToBranchId);
        document.FinancialDate = transfer.Date == default ? DateTime.Today : transfer.Date;
        document.DisplayCode = NullIfWhiteSpace(transfer.DisplayCode);
        document.ReferenceNumber = NullIfWhiteSpace(transfer.DisplayCode);
        document.Remarks = NullIfWhiteSpace(transfer.Remarks);
        document.ExchangeRate = document.ExchangeRate <= 0 ? 1 : document.ExchangeRate;

        var total = transfer.Lines.Sum(line => Math.Max(0, line.Quantity) * Math.Max(0, line.UnitCost));
        document.TotalBeforeTax = total;
        document.TaxableAmount = total;
        document.TotalAfterTax = total;
        document.LocalTotalBeforeTax = total;
        document.LocalTaxableAmount = total;
        document.LocalTotalAfterTax = total;
    }

    private static void ApplyLines(StockTransferEnvelopeDTO envelope, StockTransferViewModel transfer, bool isNew)
    {
        var existingById = envelope.DocumentLines
            .Where(line => !string.IsNullOrWhiteSpace(ReadString(line, "documentLineID")))
            .DistinctBy(line => ReadString(line, "documentLineID"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(line => ReadString(line, "documentLineID")!, StringComparer.OrdinalIgnoreCase);
        var updatedLines = new List<JsonObject>();

        for (var index = 0; index < transfer.Lines.Count; index++)
        {
            var source = transfer.Lines[index];
            JsonObject? existingLine = null;
            var hasExistingLine = !string.IsNullOrWhiteSpace(source.DocumentLineId) &&
                                  existingById.TryGetValue(source.DocumentLineId, out existingLine);
            var line = hasExistingLine ? existingLine! : new JsonObject();
            var documentLineId = hasExistingLine ? source.DocumentLineId : Guid.NewGuid().ToString();
            var saveAction = isNew || !hasExistingLine ? "Added" : "Changed";

            Set(line, "documentLineID", documentLineId);
            Set(line, "documentID", envelope.Document.DocumentID);
            Set(line, "ownerDocumentTypeID", envelope.Document.DocumentTypeID);
            Set(line, "lineOrder", index + 1);
            Set(line, "lineItemID", source.InventoryId);
            Set(line, "inventoryItemAccountID", source.InventoryId);
            Set(line, "lineItemDisplayCode", source.Sku);
            Set(line, "skuName", source.Sku);
            Set(line, "description", source.ProductName);
            Set(line, "itemName", source.ProductName);
            var lineAmount = Math.Max(0, source.Quantity) * Math.Max(0, source.UnitCost);
            Set(line, "quantity", source.Quantity);
            Set(line, "adjustedQuantity", source.Quantity);
            Set(line, "unitPrice", Math.Max(0, source.UnitCost));
            Set(line, "cost", Math.Max(0, source.UnitCost));
            Set(line, "subTotal", lineAmount);
            Set(line, "subTotalBeforeGST", lineAmount);
            Set(line, "amount", lineAmount);
            Set(line, "taxableAmount", lineAmount);
            Set(line, "unitOfMeasurementID", source.UnitOfMeasurementId);
            Set(line, "inventoryTypeID", 1);
            Set(line, "branchID", NormalizeBranchId(transfer.FromBranchId));
            Set(line, "editBranchID", NormalizeBranchId(transfer.FromBranchId));
            Set(line, "financialDate", transfer.Date == default ? DateTime.Today : transfer.Date);
            Set(line, "saveAction", saveAction);
            Set(line, "isDirty", true);
            updatedLines.Add(line);
        }

        foreach (var removedLine in envelope.DocumentLines.Except(updatedLines))
        {
            Set(removedLine, "saveAction", "Deleted");
            Set(removedLine, "isDirty", true);
            updatedLines.Add(removedLine);
        }

        envelope.DocumentLines = updatedLines;
    }

    private static void ApplyCreatedIdentity(StockTransferViewModel transfer, StockTransferDocumentDTO document)
    {
        transfer.DocumentId = First(document.DocumentID, transfer.DocumentId);
        transfer.DocumentTypeId = document.DocumentTypeID != 0 ? document.DocumentTypeID : transfer.DocumentTypeId;
        transfer.DisplayCode = First(document.DisplayCode, document.ReferenceNumber, transfer.DisplayCode, transfer.DocumentId);
        transfer.FromBranchId = First(document.FromBranchID, document.BranchID, document.EditBranchID, transfer.FromBranchId);
        transfer.ToBranchId = First(document.ToBranchID, document.OrderBranchID, transfer.ToBranchId);
        transfer.Remarks = document.Remarks ?? transfer.Remarks;
    }

    private static StockTransferViewModel ToViewModel(StockTransferDocumentDTO document) => new()
    {
        DocumentId = document.DocumentID ?? string.Empty,
        DocumentTypeId = document.DocumentTypeID,
        DisplayCode = First(document.DisplayCode, document.ReferenceNumber, document.DocumentID),
        Date = document.FinancialDate == default ? DateTime.Today : document.FinancialDate,
        BranchId = First(document.BranchID, document.EditBranchID, document.FromBranchID),
        FromBranchId = First(document.FromBranchID, document.BranchID, document.EditBranchID),
        FromBranchName = First(document.FromBranch, document.FromBranchID, document.BranchID),
        ToBranchId = First(document.ToBranchID, document.OrderBranchID),
        ToBranchName = First(document.ToBranch, document.ToBranchID, document.OrderBranchID),
        Remarks = document.Remarks ?? string.Empty,
        TotalAmount = document.TotalAfterTax != 0 ? document.TotalAfterTax : document.LocalTotalAfterTax,
        Status = ResolveTransferStatus(document)
    };

    private static string ResolveTransferStatus(StockTransferDocumentDTO document)
    {
        if (document.IsVoid)
        {
            return "Cancelled";
        }

        if (document.ExtensionData is not null)
        {
            foreach (var statusName in new[]
                     {
                         "status", "documentStatus", "transferStatus", "stockTransferStatus",
                         "verifyStatus", "acceptStatus", "receiptStatus"
                     })
            {
                var status = ReadExtensionString(document.ExtensionData, statusName);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var normalized = NormalizeTransferStatus(status);
                    if (normalized is not null)
                    {
                        return normalized;
                    }
                }
            }

            foreach (var receivedName in new[] { "isReceived", "isAccepted", "isCompleted", "accepted" })
            {
                if (ReadExtensionBoolean(document.ExtensionData, receivedName))
                {
                    return "Completed";
                }
            }
        }

        return "In Transit";
    }

    private static string? NormalizeTransferStatus(string status)
    {
        var value = status.Trim();
        if (value.Contains("receive", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("accept", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("closed", StringComparison.OrdinalIgnoreCase))
        {
            return "Completed";
        }

        if (value.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("void", StringComparison.OrdinalIgnoreCase))
        {
            return "Cancelled";
        }

        if (value.Contains("draft", StringComparison.OrdinalIgnoreCase))
        {
            return "Draft";
        }

        if (value.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("transit", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("dispatch", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("posted", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("open", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("new", StringComparison.OrdinalIgnoreCase))
        {
            return "In Transit";
        }

        return null;
    }

    private static string ReadExtensionString(
        IReadOnlyDictionary<string, JsonElement> extensionData,
        string propertyName)
    {
        var property = extensionData.FirstOrDefault(item =>
            string.Equals(item.Key, propertyName, StringComparison.OrdinalIgnoreCase));

        return property.Value.ValueKind switch
        {
            JsonValueKind.String => property.Value.GetString() ?? string.Empty,
            JsonValueKind.Number => property.Value.ToString(),
            _ => string.Empty
        };
    }

    private static bool ReadExtensionBoolean(
        IReadOnlyDictionary<string, JsonElement> extensionData,
        string propertyName)
    {
        var property = extensionData.FirstOrDefault(item =>
            string.Equals(item.Key, propertyName, StringComparison.OrdinalIgnoreCase));

        return property.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(property.Value.GetString(), out var parsed) && parsed,
            JsonValueKind.Number => property.Value.TryGetInt32(out var number) && number != 0,
            _ => false
        };
    }

    private static StockTransferLineViewModel ToLineViewModel(JsonObject line) => new()
    {
        DocumentLineId = ReadString(line, "documentLineID"),
        InventoryMovementId = First(
            ReadString(line, "inventoryMovementID"),
            ReadString(line, "InventoryMovementID")),
        InventoryId = First(ReadString(line, "lineItemID"), ReadString(line, "inventoryItemAccountID")),
        Sku = First(ReadString(line, "lineItemDisplayCode"), ReadString(line, "skuName")),
        ProductName = First(ReadString(line, "description"), ReadString(line, "itemName")),
        Quantity = ReadDecimal(line, "quantity"),
        UnitCost = ReadDecimal(line, "cost") > 0
            ? ReadDecimal(line, "cost")
            : ReadDecimal(line, "unitPrice"),
        UnitOfMeasurementId = ReadString(line, "unitOfMeasurementID")
    };

    private static string? Validate(StockTransferViewModel transfer)
    {
        if (string.IsNullOrWhiteSpace(transfer.FromBranchId)) return "From branch is required.";
        if (string.IsNullOrWhiteSpace(transfer.ToBranchId)) return "To branch is required.";
        if (string.Equals(transfer.FromBranchId.Trim(), transfer.ToBranchId.Trim(), StringComparison.OrdinalIgnoreCase))
            return "From branch and to branch must be different.";
        if (transfer.Lines.Count == 0) return "Select at least one product.";
        if (transfer.Lines.Any(line => string.IsNullOrWhiteSpace(line.InventoryId))) return "A selected product is missing its inventory ID.";
        if (transfer.Lines.Any(line => line.Quantity <= 0)) return "Transfer quantity must be greater than zero.";
        return null;
    }

    private static bool TransferLinesMatch(
        IReadOnlyCollection<StockTransferLineViewModel> actual,
        IReadOnlyCollection<StockTransferLineViewModel> expected)
    {
        var actualTotals = actual
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId))
            .GroupBy(line => LineMatchKey(line), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.OrdinalIgnoreCase);

        var expectedTotals = expected
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId))
            .GroupBy(line => LineMatchKey(line), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.OrdinalIgnoreCase);

        return actualTotals.Count == expectedTotals.Count &&
               expectedTotals.All(item =>
                   actualTotals.TryGetValue(item.Key, out var actualQuantity) &&
                   actualQuantity == item.Value);
    }

    private static string LineMatchKey(StockTransferLineViewModel line) =>
        $"{line.InventoryId.Trim()}|{NormalizeUom(line.UnitOfMeasurementId)}";

    private static string NormalizeUom(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "UNIT" : value.Trim().ToUpperInvariant();

    private static bool LooksLikeDocumentId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value, "Success", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains(' ');

    private static bool SameKey(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool SameText(string? left, string? right) =>
        string.Equals(
            left?.Trim() ?? string.Empty,
            right?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

    private static void Set(JsonObject line, string name, string? value) => line[name] = value;
    private static void Set(JsonObject line, string name, int value) => line[name] = value;
    private static void Set(JsonObject line, string name, decimal value) => line[name] = value;
    private static void Set(JsonObject line, string name, bool value) => line[name] = value;
    private static void Set(JsonObject line, string name, DateTime value) => line[name] = value;

    private static string ReadString(JsonObject line, string name)
    {
        var property = line.FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
        return property.Value?.ToString().Trim('"') ?? string.Empty;
    }

    private static decimal ReadDecimal(JsonObject line, string name)
    {
        var property = line.FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
        if (property.Value is null) return 0;
        if (property.Value is JsonValue jsonValue && jsonValue.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
        return decimal.TryParse(property.Value.ToString(), out var parsed) ? parsed : 0;
    }

    private static ApiCallResult<bool> ToBoolean(ApiCallResult<string> result, string fallback) =>
        result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallback);

    private static string TransferKey(StockTransferDocumentDTO document) =>
        First(
            document.DocumentID,
            document.DisplayCode,
            $"{document.FinancialDate:O}|{document.NumericCode}");

    private static string NormalizeBranchId(string? branchId) =>
        string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
