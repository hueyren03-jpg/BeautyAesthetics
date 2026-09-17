using System.Net;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class InventoryPendingAcceptService : IInventoryPendingAcceptService
{
    private readonly InventoryPendingAcceptAC pendingAcceptAC;
    private readonly StockGrnAC stockGrnAC;
    private readonly StockTransferAC stockTransferAC;

    public InventoryPendingAcceptService(
        InventoryPendingAcceptAC pendingAcceptAC,
        StockGrnAC stockGrnAC,
        StockTransferAC stockTransferAC)
    {
        this.pendingAcceptAC = pendingAcceptAC;
        this.stockGrnAC = stockGrnAC;
        this.stockTransferAC = stockTransferAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<PendingStockReceiptViewModel>>> LoadPendingAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await pendingAcceptAC.GetPendingByBranchAsync(branchId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return Failure<IReadOnlyList<PendingStockReceiptViewModel>>(result);
        }

        var documents = result.Value
            .Where(line => !string.IsNullOrWhiteSpace(line.DocumentID))
            .GroupBy(line => line.DocumentID!, StringComparer.OrdinalIgnoreCase)
            .Select(group => MapDocument(group.Key, branchId, group))
            .OrderByDescending(document => document.FinancialDate)
            .ToList();

        return ApiCallResult<IReadOnlyList<PendingStockReceiptViewModel>>.Ok(result.StatusCode, documents);
    }

    public async Task<ApiCallResult<IReadOnlyList<PendingStockReceiptLineViewModel>>> LoadDetailsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await pendingAcceptAC.GetDetailsAsync(documentId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return Failure<IReadOnlyList<PendingStockReceiptLineViewModel>>(result);
        }

        return ApiCallResult<IReadOnlyList<PendingStockReceiptLineViewModel>>.Ok(
            result.StatusCode,
            result.Value.Select(MapLine).ToList());
    }

    public async Task<ApiCallResult<string>> AcceptAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<string>.Failure(HttpStatusCode.BadRequest, "Stock transfer document ID is missing.");
        }

        var detailsResult = await pendingAcceptAC.GetDetailsAsync(documentId, cancellationToken);
        if (!detailsResult.Success || detailsResult.Value is null || detailsResult.Value.Count == 0)
        {
            return detailsResult.IsUnauthorized
                ? ApiCallResult<string>.Unauthorized(detailsResult.StatusCode)
                : ApiCallResult<string>.Failure(
                    detailsResult.StatusCode,
                    detailsResult.ErrorMessage ?? "Unable to load the incoming transfer before receiving it.");
        }

        var destinationBranchId = FirstValue(detailsResult.Value.Select(line => line.BranchID), "HQ");
        var receipt = MapDocument(documentId, destinationBranchId, detailsResult.Value);
        return await AcceptCoreAsync(receipt, detailsResult.Value, cancellationToken);
    }

    public async Task<ApiCallResult<string>> AcceptAsync(
        PendingStockReceiptViewModel receipt,
        CancellationToken cancellationToken = default)
    {
        if (receipt is null || string.IsNullOrWhiteSpace(receipt.DocumentId))
        {
            return ApiCallResult<string>.Failure(HttpStatusCode.BadRequest, "Stock transfer document ID is missing.");
        }

        var detailsResult = await pendingAcceptAC.GetDetailsAsync(receipt.DocumentId, cancellationToken);
        if (!detailsResult.Success || detailsResult.Value is null || detailsResult.Value.Count == 0)
        {
            return detailsResult.IsUnauthorized
                ? ApiCallResult<string>.Unauthorized(detailsResult.StatusCode)
                : ApiCallResult<string>.Failure(
                    detailsResult.StatusCode,
                    detailsResult.ErrorMessage ?? "Unable to load the incoming transfer before receiving it.");
        }

        return await AcceptCoreAsync(receipt, detailsResult.Value, cancellationToken);
    }

    private async Task<ApiCallResult<string>> AcceptCoreAsync(
        PendingStockReceiptViewModel receipt,
        IReadOnlyList<PendingStockReceiptLineDTO> pendingLines,
        CancellationToken cancellationToken)
    {
        var destinationBranchId = NormalizeBranchId(
            string.IsNullOrWhiteSpace(receipt.DestinationBranchId)
                ? FirstValue(pendingLines.Select(line => line.BranchID), "HQ")
                : receipt.DestinationBranchId);
        var transferDisplayCode = FirstValue(
            pendingLines.Select(line => line.DisplayCode),
            string.IsNullOrWhiteSpace(receipt.DisplayCode) ? receipt.DocumentId : receipt.DisplayCode);

        StockTransferEnvelopeDTO? sourceTransfer = null;
        var transferResult = await stockTransferAC.LoadRecordAsync(receipt.DocumentId, cancellationToken);
        if (transferResult.Success && transferResult.Value is not null)
        {
            sourceTransfer = transferResult.Value;
            transferDisplayCode = FirstValue(
                new[]
                {
                    sourceTransfer.Document.DisplayCode,
                    sourceTransfer.Document.ReferenceNumber,
                    transferDisplayCode
                },
                receipt.DocumentId);

            // The Stock Transfer itself is the authoritative source for the receiving branch.
            // Pending movement detail rows may expose the movement/source branch instead,
            // which can cause the linked GRN to be searched or created under the wrong branch.
            var transferDestinationBranchId = FirstNonEmpty(
                sourceTransfer.Document.ToBranchID,
                sourceTransfer.Document.OrderBranchID);
            if (!string.IsNullOrWhiteSpace(transferDestinationBranchId))
            {
                destinationBranchId = NormalizeBranchId(transferDestinationBranchId);
                receipt.DestinationBranchId = destinationBranchId;
            }
        }

        var existingGrnResult = await LoadDestinationGrnsAsync(destinationBranchId, receipt.FinancialDate, cancellationToken);
        if (!existingGrnResult.Success || existingGrnResult.Value is null)
        {
            return ApiCallResult<string>.Failure(
                existingGrnResult.StatusCode,
                existingGrnResult.ErrorMessage ?? "Unable to verify whether this transfer already has a GRN.");
        }

        var existingGrn = FindLinkedGrn(existingGrnResult.Value, receipt.DocumentId, transferDisplayCode);

        var acceptResult = await pendingAcceptAC.AcceptStockInAsync(receipt.DocumentId, cancellationToken);
        if (!acceptResult.Success)
        {
            return acceptResult;
        }

        if (existingGrn is not null)
        {
            return ApiCallResult<string>.Ok(
                acceptResult.StatusCode,
                FirstNonEmpty(existingGrn.DocumentID, existingGrn.DisplayCode, "GRN created"));
        }

        if (LooksLikeDocumentReference(acceptResult.Value))
        {
            var returnedGrn = await stockGrnAC.LoadRecordAsync(acceptResult.Value!, cancellationToken);
            if (returnedGrn.Success &&
                returnedGrn.Value?.Document is not null &&
                !string.IsNullOrWhiteSpace(returnedGrn.Value.Document.DocumentID))
            {
                return ApiCallResult<string>.Ok(acceptResult.StatusCode, returnedGrn.Value.Document.DocumentID!);
            }
        }

        // Some deployments create the GRN asynchronously when AcceptStockIn succeeds.
        // Give that path a short opportunity before creating the audit GRN ourselves.
        foreach (var delay in new[] { 0, 300, 700 })
        {
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var generatedResult = await LoadDestinationGrnsAsync(destinationBranchId, receipt.FinancialDate, cancellationToken);
            if (!generatedResult.Success || generatedResult.Value is null)
            {
                continue;
            }

            var generatedGrn = FindLinkedGrn(generatedResult.Value, receipt.DocumentId, transferDisplayCode);
            if (generatedGrn is not null)
            {
                return ApiCallResult<string>.Ok(
                    acceptResult.StatusCode,
                    FirstNonEmpty(generatedGrn.DocumentID, generatedGrn.DisplayCode, "GRN created"));
            }
        }

        var createGrnResult = await CreateTransferGrnAsync(
            receipt,
            pendingLines,
            sourceTransfer,
            destinationBranchId,
            transferDisplayCode,
            cancellationToken);

        if (!createGrnResult.Success)
        {
            return ApiCallResult<string>.Failure(
                createGrnResult.StatusCode,
                $"Stock was received, but the GRN could not be created. {createGrnResult.ErrorMessage ?? "GRN creation failed."}");
        }

        return createGrnResult;
    }

    private async Task<ApiCallResult<string>> CreateTransferGrnAsync(
        PendingStockReceiptViewModel receipt,
        IReadOnlyList<PendingStockReceiptLineDTO> pendingLines,
        StockTransferEnvelopeDTO? sourceTransfer,
        string destinationBranchId,
        string transferDisplayCode,
        CancellationToken cancellationToken)
    {
        var templateResult = await stockGrnAC.LoadRecordAsync(string.Empty, cancellationToken);
        if (!templateResult.Success || templateResult.Value is null)
        {
            return ApiCallResult<string>.Failure(
                templateResult.StatusCode,
                templateResult.ErrorMessage ?? "Unable to prepare the transfer GRN.");
        }

        var envelope = templateResult.Value;
        var document = envelope.Document;
        var receiptDate = DateTime.Today;
        var sourceDocumentTypeId = sourceTransfer?.Document.DocumentTypeID
            ?? pendingLines.Select(line => line.DocumentTypeID).FirstOrDefault(value => value != 0);
        var sourceDocumentTypeName = FirstNonEmpty(
            sourceTransfer?.Document.FriendlyDocumentName,
            "Stock Transfer");
        var totalCost = pendingLines.Sum(line => Math.Max(0m, line.TotalCost));
        var exchangeRate = document.ExchangeRate <= 0 ? 1m : document.ExchangeRate;

        document.DocumentTypeID = document.DocumentTypeID == 0 ? 51 : document.DocumentTypeID;
        document.FriendlyDocumentName = "GRN";
        document.BranchID = destinationBranchId;
        document.EditBranchID = destinationBranchId;
        document.FinancialDate = receiptDate;
        document.PostingDate = receiptDate;
        document.IsPostingDateDifferent = false;
        document.CreatedByDocumentTypeID = sourceDocumentTypeId;
        document.CreatedByDocumentTypeName = sourceDocumentTypeName;
        document.CreatedByDocumentID = receipt.DocumentId;
        document.CreatedByDocumentDisplayCode = transferDisplayCode;
        document.OrderBranchID = destinationBranchId;
        document.ReferenceNumber = transferDisplayCode;
        document.Remarks = string.IsNullOrWhiteSpace(receipt.Remarks) ? document.Remarks : receipt.Remarks.Trim();
        document.ExchangeRate = exchangeRate;
        document.TotalBeforeTax = totalCost;
        document.TaxableAmount = totalCost;
        document.TaxAmount = 0m;
        document.RoundingAmount = 0m;
        document.TotalAfterTax = totalCost;
        document.LocalTotalBeforeTax = Math.Round(totalCost * exchangeRate, 2);
        document.LocalTaxableAmount = document.LocalTotalBeforeTax;
        document.LocalTaxAmount = 0m;
        document.LocalRoundingAmount = 0m;
        document.LocalTotalAfterTax = document.LocalTotalBeforeTax;
        document.SaveAction = "Added";
        document.IsDirty = true;

        var sourceLines = sourceTransfer?.DocumentLines ?? [];
        var blankTemplate = envelope.DocumentLines.FirstOrDefault();
        var grnLines = new List<JsonObject>();

        for (var index = 0; index < pendingLines.Count; index++)
        {
            var pendingLine = pendingLines[index];
            var sourceLine = FindSourceLine(sourceLines, pendingLine);
            var line = blankTemplate?.DeepClone().AsObject() ?? new JsonObject();
            var inventoryId = FirstNonEmpty(
                pendingLine.LineItemID,
                pendingLine.InventoryItemAccountID,
                ReadString(sourceLine, "lineItemID"),
                ReadString(sourceLine, "inventoryItemAccountID"));
            var sku = FirstNonEmpty(
                pendingLine.ItemDisplayCode,
                pendingLine.MatrixCode,
                ReadString(sourceLine, "lineItemDisplayCode"),
                ReadString(sourceLine, "skuName"));
            var itemName = FirstNonEmpty(
                ReadString(sourceLine, "itemName"),
                ReadString(sourceLine, "description"),
                sku,
                inventoryId);
            var uom = FirstNonEmpty(ReadString(sourceLine, "unitOfMeasurementID"), "UNIT");
            var quantity = Math.Max(0m, pendingLine.Quantity);
            var amount = Math.Max(0m, pendingLine.TotalCost);
            var unitCost = quantity > 0 ? amount / quantity : 0m;
            var inventoryTypeId = ReadInt(sourceLine, "inventoryTypeID", 1);

            Set(line, "isLoading", false);
            Set(line, "documentLineID", Guid.NewGuid().ToString());
            Set(line, "documentID", document.DocumentID);
            Set(line, "ownerDocumentTypeID", document.DocumentTypeID);
            Set(line, "lineOrder", index + 1);
            Set(line, "lineItemID", inventoryId);
            Set(line, "inventoryItemAccountID", inventoryId);
            Set(line, "lineItemDisplayCode", sku);
            Set(line, "skuName", sku);
            Set(line, "description", itemName);
            Set(line, "itemName", itemName);
            Set(line, "quantity", quantity);

            // AcceptStockIn is the inventory movement for an incoming transfer.
            // Keep this generated GRN as the linked audit document so we do not
            // intentionally apply the received quantity a second time.
            Set(line, "adjustedQuantity", 0m);

            Set(line, "unitOfMeasurementID", uom);
            Set(line, "inventoryTypeID", inventoryTypeId);
            Set(line, "unitPrice", unitCost);
            Set(line, "cost", unitCost);
            Set(line, "subTotal", amount);
            Set(line, "amount", amount);
            Set(line, "taxableAmount", amount);
            Set(line, "subTotalBeforeGST", amount);
            Set(line, "taxAmount", 0m);
            Set(line, "taxPercentage", 0m);
            Set(line, "isTaxInclusive", false);
            Set(line, "isPurchaseTax", false);
            Set(line, "branchID", destinationBranchId);
            Set(line, "editBranchID", destinationBranchId);
            Set(line, "financialDate", receiptDate);
            Set(line, "sourceDocumentLineID", FirstNonEmpty(pendingLine.DocumentLineID, ReadString(sourceLine, "documentLineID")));
            Set(line, "targetTransactionID", pendingLine.InventoryMovementID);
            Set(line, "batchNo", pendingLine.BatchNo);
            Set(line, "groupID", pendingLine.GroupID);
            Set(line, "saveAction", "Added");
            Set(line, "isDirty", true);
            grnLines.Add(line);
        }

        envelope.DocumentLines = grnLines;

        var createResult = await stockGrnAC.CreateRecordAsync(envelope, cancellationToken);
        if (!createResult.Success)
        {
            return ApiCallResult<string>.Failure(
                createResult.StatusCode,
                createResult.ErrorMessage ?? "Unable to create the transfer GRN.");
        }

        if (LooksLikeDocumentReference(createResult.Value))
        {
            return ApiCallResult<string>.Ok(createResult.StatusCode, createResult.Value!);
        }

        foreach (var delay in new[] { 0, 250, 600 })
        {
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var lookupResult = await LoadDestinationGrnsAsync(destinationBranchId, receiptDate, cancellationToken);
            if (!lookupResult.Success || lookupResult.Value is null)
            {
                continue;
            }

            var created = FindLinkedGrn(lookupResult.Value, receipt.DocumentId, transferDisplayCode);
            if (created is not null)
            {
                return ApiCallResult<string>.Ok(
                    createResult.StatusCode,
                    FirstNonEmpty(created.DocumentID, created.DisplayCode, "GRN created"));
            }
        }

        return ApiCallResult<string>.Ok(createResult.StatusCode, "GRN created");
    }

    private Task<ApiCallResult<List<StockGrnDocumentDTO>>> LoadDestinationGrnsAsync(
        string branchId,
        DateTime referenceDate,
        CancellationToken cancellationToken) =>
        stockGrnAC.LoadProxyAsync(new StockGrnProxyRequestDTO
        {
            BranchID = NormalizeBranchId(branchId),
            StartDate = (referenceDate == default ? DateTime.Today : referenceDate.Date).AddDays(-7),
            EndDate = DateTime.Today.AddDays(2).AddTicks(-1),
            PageNumber = 1,
            PageSize = 200
        }, cancellationToken);

    private static StockGrnDocumentDTO? FindLinkedGrn(
        IEnumerable<StockGrnDocumentDTO> grns,
        string transferDocumentId,
        string transferDisplayCode) =>
        grns.FirstOrDefault(grn =>
            !grn.IsVoid &&
            (SameKey(grn.CreatedByDocumentID, transferDocumentId) ||
             SameKey(grn.CreatedByDocumentDisplayCode, transferDisplayCode) ||
             (grn.CreatedByDocumentTypeName?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true &&
              SameKey(grn.ReferenceNumber, transferDisplayCode))));

    private static JsonObject? FindSourceLine(
        IEnumerable<JsonObject> sourceLines,
        PendingStockReceiptLineDTO pendingLine)
    {
        var byDocumentLine = sourceLines.FirstOrDefault(line =>
            SameKey(ReadString(line, "documentLineID"), pendingLine.DocumentLineID));
        if (byDocumentLine is not null)
        {
            return byDocumentLine;
        }

        return sourceLines.FirstOrDefault(line =>
            SameKey(ReadString(line, "lineItemID"), pendingLine.LineItemID) ||
            SameKey(ReadString(line, "inventoryItemAccountID"), pendingLine.InventoryItemAccountID));
    }

    private static PendingStockReceiptViewModel MapDocument(
        string documentId,
        string destinationBranchId,
        IEnumerable<PendingStockReceiptLineDTO> source)
    {
        var lines = source.ToList();
        var first = lines[0];
        return new PendingStockReceiptViewModel
        {
            DocumentId = documentId,
            DisplayCode = FirstValue(lines.Select(line => line.DisplayCode), documentId),
            FinancialDate = first.FinancialDate,
            SourceName = FirstValue(lines.Select(line => line.AccountName), "Stock transfer"),
            DestinationBranchId = string.IsNullOrWhiteSpace(destinationBranchId)
                ? FirstValue(lines.Select(line => line.BranchID), string.Empty)
                : destinationBranchId.Trim(),
            Remarks = FirstValue(lines.Select(line => line.Remarks), string.Empty),
            TotalQuantity = lines.Sum(line => line.Quantity),
            TotalCost = lines.Sum(line => line.TotalCost),
            Lines = lines.Select(MapLine).ToList()
        };
    }

    private static PendingStockReceiptLineViewModel MapLine(PendingStockReceiptLineDTO line) => new()
    {
        LineId = line.DocumentLineID ?? string.Empty,
        InventoryMovementId = line.InventoryMovementID ?? string.Empty,
        ItemId = line.LineItemID ?? line.InventoryItemAccountID ?? string.Empty,
        ItemCode = line.ItemDisplayCode ?? line.MatrixCode ?? "-",
        ItemName = line.AccountName ?? "-",
        Quantity = line.Quantity,
        BatchNo = line.BatchNo ?? string.Empty,
        TotalCost = line.TotalCost
    };

    private static string NormalizeBranchId(string? branchId) =>
        string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();

    private static string FirstValue(IEnumerable<string?> values, string fallback) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? fallback;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static bool SameKey(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeDocumentReference(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 160 &&
        !value.Any(char.IsWhiteSpace) &&
        !value.Contains("success", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("created", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(JsonObject? line, string propertyName)
    {
        if (line is null)
        {
            return string.Empty;
        }

        var property = line.FirstOrDefault(item =>
            string.Equals(item.Key, propertyName, StringComparison.OrdinalIgnoreCase));
        return property.Value?.ToString().Trim('"') ?? string.Empty;
    }

    private static int ReadInt(JsonObject? line, string propertyName, int fallback)
    {
        var value = ReadString(line, propertyName);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static void Set(JsonObject line, string name, string? value) => line[name] = value;
    private static void Set(JsonObject line, string name, int value) => line[name] = value;
    private static void Set(JsonObject line, string name, decimal value) => line[name] = value;
    private static void Set(JsonObject line, string name, bool value) => line[name] = value;
    private static void Set(JsonObject line, string name, DateTime value) => line[name] = value;

    private static ApiCallResult<T> Failure<T>(ApiCallResult<List<PendingStockReceiptLineDTO>> source) =>
        source.IsUnauthorized
            ? ApiCallResult<T>.Unauthorized(source.StatusCode)
            : ApiCallResult<T>.Failure(source.StatusCode, source.ErrorMessage ?? "Pending stock receipt request failed.");
}
