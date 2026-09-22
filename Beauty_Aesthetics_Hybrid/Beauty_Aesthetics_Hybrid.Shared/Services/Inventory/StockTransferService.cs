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

public sealed class StockTransferService : IStockTransferService
{
    private readonly StockTransferAC stockTransferAC;
    private readonly IInventoryPendingAcceptService pendingAcceptService;
    private readonly AppFeedbackService feedback;

    public StockTransferService(
        StockTransferAC stockTransferAC,
        IInventoryPendingAcceptService pendingAcceptService,
        AppFeedbackService feedback)
    {
        this.stockTransferAC = stockTransferAC;
        this.pendingAcceptService = pendingAcceptService;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<StockTransferViewModel>>> LoadTransfersAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return ApiCallResult<IReadOnlyList<StockTransferViewModel>>.Failure(
                HttpStatusCode.BadRequest,
                "Select a working branch before loading stock transfer history.");
        }

        var result = await stockTransferAC.LoadProxyAsync(new StockTransferProxyRequestDTO
        {
            BranchID = branchId.Trim(),
            StartDate = DateTime.Today.AddYears(-2),
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
            PageNumber = 1,
            PageSize = 200
        }, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<StockTransferViewModel>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load stock transfers.");
        }

        var transfers = result.Value
            .Where(document => !document.IsVoid)
            .DistinctBy(TransferKey, StringComparer.OrdinalIgnoreCase)
            .Select(ToViewModel)
            .OrderByDescending(transfer => transfer.Date)
            .ThenByDescending(transfer => transfer.DisplayCode)
            .ToList();

        await ApplyPendingApiStatusesAsync(transfers, cancellationToken);

        return ApiCallResult<IReadOnlyList<StockTransferViewModel>>.Ok(
            result.StatusCode,
            transfers);
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

        var result = await stockTransferAC.LoadRecordAsync(documentId.Trim(), cancellationToken);
        if (!result.Success || result.Value?.objDoc_StockTransfer is null)
        {
            return ApiCallResult<StockTransferViewModel>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load stock transfer details.");
        }

        var transfer = ToViewModel(result.Value.objDoc_StockTransfer);
        transfer.Lines = (result.Value.lstDocumentLine ?? new ObservableCollection<DocumentLineTableDM>())
            .Where(line => line.SaveAction != EntityState.Deleted)
            .Select(ToLineViewModel)
            .ToList();

        await ApplyPendingApiStatusAsync(transfer, cancellationToken);

        return ApiCallResult<StockTransferViewModel>.Ok(result.StatusCode, transfer);
    }

    public async Task<ApiCallResult<bool>> CreateTransferAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(transfer);
        if (validationError is not null)
        {
            feedback.Warning(validationError, "Check stock transfer");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        HashSet<string>? existingTransferIds = null;
        var beforeCreate = await LoadTransfersAsync(transfer.FromBranchId, cancellationToken);
        if (beforeCreate.Success && beforeCreate.Value is not null)
        {
            existingTransferIds = beforeCreate.Value
                .Where(item => !string.IsNullOrWhiteSpace(item.DocumentId))
                .Select(item => item.DocumentId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var envelope = new Doc_StockTransfer();
        ApplyDocument(envelope.objDoc_StockTransfer, transfer);
        ApplyLines(envelope, transfer, isNew: true);
        envelope.objDoc_StockTransfer.SaveAction = EntityState.Added;
        envelope.objDoc_StockTransfer.IsDirty = true;

        var createResult = await stockTransferAC.CreateRecordAsync(envelope, cancellationToken);
        if (!createResult.Success)
        {
            var message = createResult.ErrorMessage ?? "Unable to create stock transfer.";
            feedback.Error(message, "Transfer not created");
            return ApiCallResult<bool>.Failure(createResult.StatusCode, message);
        }

        transfer.DocumentId = First(
            envelope.objDoc_StockTransfer.DocumentID,
            LooksLikeDocumentId(createResult.Value) ? createResult.Value : null,
            transfer.DocumentId);
        transfer.DocumentTypeId = envelope.objDoc_StockTransfer.DocumentTypeID;
        transfer.DisplayCode = First(
            envelope.objDoc_StockTransfer.DisplayCode,
            transfer.DisplayCode,
            transfer.DocumentId);

        if (!string.IsNullOrWhiteSpace(transfer.DocumentId))
        {
            var createdRecord = await stockTransferAC.LoadRecordAsync(
                transfer.DocumentId,
                cancellationToken);

            if (createdRecord.Success &&
                createdRecord.Value?.objDoc_StockTransfer is not null)
            {
                ApplyCreatedIdentity(
                    transfer,
                    createdRecord.Value.objDoc_StockTransfer);

                transfer.Lines =
                    (createdRecord.Value.lstDocumentLine ??
                     new ObservableCollection<DocumentLineTableDM>())
                    .Where(line => line.SaveAction != EntityState.Deleted)
                    .Select(ToLineViewModel)
                    .ToList();
            }
        }

        if (string.IsNullOrWhiteSpace(transfer.DocumentId) &&
            existingTransferIds is not null)
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
            const string message =
                "Stock Transfer was created, but its document ID could not be resolved safely.";
            feedback.Warning(message, "Transfer requires attention");
            return ApiCallResult<bool>.Failure(HttpStatusCode.Conflict, message);
        }

        feedback.Success("Stock transfer created successfully.", "Transfer created");
        return ApiCallResult<bool>.Ok(createResult.StatusCode, true);
    }

    public async Task<ApiCallResult<bool>> UpdateTransferAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transfer.DocumentId))
        {
            const string message = "Stock transfer document ID is missing.";
            feedback.Warning(message, "Transfer not updated");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, message);
        }

        var validationError = Validate(transfer);
        if (validationError is not null)
        {
            feedback.Warning(validationError, "Check transfer changes");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var loadResult = await stockTransferAC.LoadRecordAsync(
            transfer.DocumentId.Trim(),
            cancellationToken);

        if (!loadResult.Success || loadResult.Value?.objDoc_StockTransfer is null)
        {
            var message =
                loadResult.ErrorMessage ??
                "Unable to load stock transfer before updating.";
            feedback.Error(message, "Transfer not updated");
            return ApiCallResult<bool>.Failure(loadResult.StatusCode, message);
        }

        var envelope = loadResult.Value;
        ApplyDocument(envelope.objDoc_StockTransfer, transfer);
        ApplyLines(envelope, transfer, isNew: false);
        envelope.objDoc_StockTransfer.DocumentID = transfer.DocumentId.Trim();
        envelope.objDoc_StockTransfer.SaveAction = EntityState.Changed;
        envelope.objDoc_StockTransfer.IsDirty = true;

        var result = ToBoolean(
            await stockTransferAC.UpdateRecordAsync(envelope, cancellationToken),
            "Unable to update stock transfer.");

        if (result.Success)
        {
            feedback.Success("Stock transfer updated successfully.", "Transfer updated");
        }
        else
        {
            feedback.Error(
                result.ErrorMessage ?? "Unable to update stock transfer.",
                "Transfer not updated");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> DeleteTransferAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            const string message = "Stock transfer document ID is missing.";
            feedback.Warning(message, "Transfer not deleted");
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, message);
        }

        var result = ToBoolean(
            await stockTransferAC.DeleteAsync(documentId.Trim(), cancellationToken),
            "Unable to delete stock transfer.");

        if (result.Success)
        {
            feedback.Success("Stock transfer deleted successfully.", "Transfer deleted");
        }
        else
        {
            feedback.Error(
                result.ErrorMessage ?? "Unable to delete stock transfer.",
                "Transfer not deleted");
        }

        return result;
    }

    private async Task<StockTransferViewModel?> ResolveCreatedTransferAsync(
        StockTransferViewModel requestedTransfer,
        IReadOnlySet<string> existingTransferIds,
        CancellationToken cancellationToken)
    {
        var afterCreate = await LoadTransfersAsync(
            requestedTransfer.FromBranchId,
            cancellationToken);

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
            var detailResult = await LoadTransferAsync(
                candidate.DocumentId,
                cancellationToken);

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

        return verifiedCandidates.Count == 1
            ? verifiedCandidates[0]
            : null;
    }

    private static void ApplyDocument(
        Doc_StockTransferDM document,
        StockTransferViewModel transfer)
    {
        var fromBranch = NormalizeBranchId(transfer.FromBranchId);
        var toBranch = NormalizeBranchId(transfer.ToBranchId);
        var date = transfer.Date == default ? DateTime.Today : transfer.Date;
        var total = transfer.Lines.Sum(line =>
            Math.Max(0, line.Quantity) * Math.Max(0, line.UnitCost));

        document.BranchID = fromBranch;
        document.EditBranchID = fromBranch;
        document.FromBranchID = fromBranch;
        document.FromBranch = First(transfer.FromBranchName, transfer.FromBranchId);
        document.ToBranchID = toBranch;
        document.ToBranch = First(transfer.ToBranchName, transfer.ToBranchId);
        document.FinancialDate = date;
        document.Remarks = NullIfWhiteSpace(transfer.Remarks);
        document.TotalBeforeTax = total;
        document.TaxableAmount = total;
        document.TotalAfterTax = total;
        document.LocalTotalBeforeTax = total;
        document.LocalTaxableAmount = total;
        document.LocalTotalAfterTax = total;
    }

    private static void ApplyLines(
        Doc_StockTransfer envelope,
        StockTransferViewModel transfer,
        bool isNew)
    {
        var document = envelope.objDoc_StockTransfer ??
            throw new InvalidOperationException(
                "Stock transfer document header is missing.");

        var existingLines =
            envelope.lstDocumentLine?.ToList() ??
            new List<DocumentLineTableDM>();

        var existingById = existingLines
            .Where(line => !string.IsNullOrWhiteSpace(line.DocumentLineID))
            .DistinctBy(line => line.DocumentLineID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                line => line.DocumentLineID,
                StringComparer.OrdinalIgnoreCase);

        var updatedLines = new List<DocumentLineTableDM>();
        var fromBranch = NormalizeBranchId(transfer.FromBranchId);
        var date = transfer.Date == default ? DateTime.Today : transfer.Date;

        for (var index = 0; index < transfer.Lines.Count; index++)
        {
            var source = transfer.Lines[index];
            DocumentLineTableDM? existingLine = null;
            var hasExistingLine =
                !string.IsNullOrWhiteSpace(source.DocumentLineId) &&
                existingById.TryGetValue(source.DocumentLineId, out existingLine);

            var line = hasExistingLine
                ? existingLine!
                : new DocumentLineTableDM();

            var quantity = Math.Max(0, source.Quantity);
            var unitCost = Math.Max(0, source.UnitCost);
            var lineAmount = quantity * unitCost;

            line.DocumentLineID =
                hasExistingLine ? source.DocumentLineId : string.Empty;
            line.DocumentID = document.DocumentID ?? string.Empty;
            line.OwnerDocumentTypeID = document.DocumentTypeID;
            line.LineOrder = index + 1;
            line.LineItemID = source.InventoryId;
            line.InventoryItemAccountID = source.InventoryId;
            line.LineItemDisplayCode = source.Sku;
            line.SKUName = source.Sku;
            line.Description = source.ProductName;
            line.ItemName = source.ProductName;
            line.Quantity = quantity;
            line.AdjustedQuantity = quantity;
            line.UnitPrice = unitCost;
            line.Cost = unitCost;
            line.SubTotal = lineAmount;
            line.SubTotalBeforeGST = lineAmount;
            line.Amount = lineAmount;
            line.TaxableAmount = lineAmount;
            line.UnitOfMeasurementID = source.UnitOfMeasurementId;
            line.InventoryTypeID =
                source.InventoryTypeId <= 0 ? 1 : source.InventoryTypeId;
            line.SKUQuantity = 1;
            line.BranchID = fromBranch;
            line.EditBranchID = fromBranch;
            line.FinancialDate = date;
            line.DocumentDisplayCode = document.DisplayCode;
            line.SaveAction = isNew || !hasExistingLine
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

    private static void ApplyCreatedIdentity(
        StockTransferViewModel transfer,
        Doc_StockTransferDM document)
    {
        transfer.DocumentId = First(document.DocumentID, transfer.DocumentId);
        transfer.DocumentTypeId =
            document.DocumentTypeID != 0
                ? document.DocumentTypeID
                : transfer.DocumentTypeId;
        transfer.DisplayCode = First(
            document.DisplayCode,
            transfer.DisplayCode,
            transfer.DocumentId);
        transfer.FromBranchId = First(
            document.FromBranchID,
            document.BranchID,
            document.EditBranchID,
            transfer.FromBranchId);
        transfer.ToBranchId = First(
            document.ToBranchID,
            transfer.ToBranchId);
        transfer.FromBranchName = First(
            document.FromBranch,
            transfer.FromBranchName,
            transfer.FromBranchId);
        transfer.ToBranchName = First(
            document.ToBranch,
            transfer.ToBranchName,
            transfer.ToBranchId);
        transfer.Remarks = document.Remarks ?? transfer.Remarks;
    }

    private static StockTransferViewModel ToViewModel(
        Doc_StockTransferDM document) => new()
    {
        DocumentId = document.DocumentID ?? string.Empty,
        DocumentTypeId = document.DocumentTypeID,
        DisplayCode = First(document.DisplayCode, document.DocumentID),
        Date = document.FinancialDate == default
            ? DateTime.Today
            : document.FinancialDate,
        BranchId = First(
            document.BranchID,
            document.EditBranchID,
            document.FromBranchID),
        FromBranchId = First(
            document.FromBranchID,
            document.BranchID,
            document.EditBranchID),
        FromBranchName = First(
            document.FromBranch,
            document.FromBranchID,
            document.BranchID),
        ToBranchId = document.ToBranchID ?? string.Empty,
        ToBranchName = First(document.ToBranch, document.ToBranchID),
        Remarks = document.Remarks ?? string.Empty,
        TotalAmount = document.TotalAfterTax,
        Status = document.IsVoid ? "Cancelled" : "In Transit"
    };

    private static StockTransferLineViewModel ToLineViewModel(
        DocumentLineTableDM line) => new()
    {
        DocumentLineId = line.DocumentLineID ?? string.Empty,
        InventoryId = First(line.LineItemID, line.InventoryItemAccountID),
        Sku = First(line.LineItemDisplayCode, line.SKUName),
        ProductName = First(line.Description, line.ItemName),
        Quantity = line.Quantity != 0 ? line.Quantity : line.AdjustedQuantity,
        UnitCost = FirstPositive(line.Cost, line.UnitPrice),
        InventoryTypeId = Math.Max(1, line.InventoryTypeID),
        UnitOfMeasurementId = line.UnitOfMeasurementID ?? string.Empty
    };

    private async Task ApplyPendingApiStatusesAsync(
        IReadOnlyList<StockTransferViewModel> transfers,
        CancellationToken cancellationToken)
    {
        var groups = transfers
            .Where(ShouldResolveFromPendingApi)
            .Where(transfer => !string.IsNullOrWhiteSpace(transfer.ToBranchId))
            .GroupBy(
                transfer => transfer.ToBranchId.Trim(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var pendingResult = await pendingAcceptService.LoadPendingAsync(
                group.Key,
                cancellationToken);

            if (!pendingResult.Success || pendingResult.Value is null)
            {
                continue;
            }

            foreach (var transfer in group)
            {
                transfer.Status = pendingResult.Value.Any(
                        receipt => MatchesPendingTransfer(transfer, receipt))
                    ? "In Transit"
                    : "Completed";
            }
        }
    }

    private async Task ApplyPendingApiStatusAsync(
        StockTransferViewModel transfer,
        CancellationToken cancellationToken)
    {
        if (!ShouldResolveFromPendingApi(transfer) ||
            string.IsNullOrWhiteSpace(transfer.ToBranchId))
        {
            return;
        }

        var pendingResult = await pendingAcceptService.LoadPendingAsync(
            transfer.ToBranchId.Trim(),
            cancellationToken);

        if (!pendingResult.Success || pendingResult.Value is null)
        {
            return;
        }

        transfer.Status = pendingResult.Value.Any(
                receipt => MatchesPendingTransfer(transfer, receipt))
            ? "In Transit"
            : "Completed";
    }

    private static bool ShouldResolveFromPendingApi(
        StockTransferViewModel transfer) =>
        !string.Equals(
            transfer.Status,
            "Cancelled",
            StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(
            transfer.Status,
            "Completed",
            StringComparison.OrdinalIgnoreCase);

    private static bool MatchesPendingTransfer(
        StockTransferViewModel transfer,
        PendingStockReceiptViewModel receipt) =>
        SameKey(transfer.DocumentId, receipt.DocumentId) ||
        SameKey(transfer.DisplayCode, receipt.DisplayCode);

    private static string? Validate(StockTransferViewModel transfer)
    {
        if (string.IsNullOrWhiteSpace(transfer.FromBranchId))
            return "From branch is required.";

        if (string.IsNullOrWhiteSpace(transfer.ToBranchId))
            return "To branch is required.";

        if (string.Equals(
                transfer.FromBranchId.Trim(),
                transfer.ToBranchId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return "From branch and to branch must be different.";
        }

        if (transfer.Lines.Count == 0)
            return "Select at least one product.";

        if (transfer.Lines.Any(
                line => string.IsNullOrWhiteSpace(line.InventoryId)))
        {
            return "A selected product is missing its inventory ID.";
        }

        if (transfer.Lines.Any(line => line.Quantity <= 0))
            return "Transfer quantity must be greater than zero.";

        return null;
    }

    private static bool TransferLinesMatch(
        IReadOnlyCollection<StockTransferLineViewModel> actual,
        IReadOnlyCollection<StockTransferLineViewModel> expected)
    {
        var actualTotals = actual
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId))
            .GroupBy(LineMatchKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => line.Quantity),
                StringComparer.OrdinalIgnoreCase);

        var expectedTotals = expected
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId))
            .GroupBy(LineMatchKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => line.Quantity),
                StringComparer.OrdinalIgnoreCase);

        return actualTotals.Count == expectedTotals.Count &&
               expectedTotals.All(item =>
                   actualTotals.TryGetValue(item.Key, out var actualQuantity) &&
                   actualQuantity == item.Value);
    }

    private static string LineMatchKey(StockTransferLineViewModel line) =>
        $"{line.InventoryId.Trim()}|{NormalizeUom(line.UnitOfMeasurementId)}";

    private static string NormalizeUom(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "UNIT"
            : value.Trim().ToUpperInvariant();

    private static bool LooksLikeDocumentId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value, "Success", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains(' ');

    private static bool SameKey(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(
            left.Trim(),
            right.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static bool SameText(string? left, string? right) =>
        string.Equals(
            left?.Trim() ?? string.Empty,
            right?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

    private static string TransferKey(Doc_StockTransferDM document) =>
        First(
            document.DocumentID,
            document.DisplayCode,
            $"{document.FinancialDate:O}|{document.FromBranchID}|{document.ToBranchID}");

    private static decimal FirstPositive(params decimal[] values) =>
        values.FirstOrDefault(value => value > 0);

    private static ApiCallResult<bool> ToBoolean(
        ApiCallResult<string> result,
        string fallback) =>
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
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ??
        string.Empty;
}
