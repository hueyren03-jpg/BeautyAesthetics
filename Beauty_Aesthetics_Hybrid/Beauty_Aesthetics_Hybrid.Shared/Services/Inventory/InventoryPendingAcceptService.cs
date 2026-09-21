using System.Net;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class InventoryPendingAcceptService : IInventoryPendingAcceptService
{
    private readonly InventoryPendingAcceptAC pendingAcceptAC;
    private readonly AppFeedbackService feedback;
    private readonly object acceptedSync = new();
    private readonly HashSet<string> acceptedTransferKeys = new(StringComparer.OrdinalIgnoreCase);

    public InventoryPendingAcceptService(
        InventoryPendingAcceptAC pendingAcceptAC,
        AppFeedbackService feedback)
    {
        this.pendingAcceptAC = pendingAcceptAC;
        this.feedback = feedback;
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
            .Where(document => !WasAcceptedTransfer(document.DocumentId, document.DisplayCode))
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
            const string message = "Stock transfer document ID is missing.";
            feedback.Warning(message, "Transfer not received");
            return ApiCallResult<string>.Failure(HttpStatusCode.BadRequest, message);
        }

        // Stock Transfer is its own document workflow.
        // AcceptStockIn receives the pending transfer at the destination branch.
        var result = await pendingAcceptAC.AcceptStockInAsync(documentId, cancellationToken);
        if (result.Success)
        {
            RememberAcceptedTransfer(documentId);
            feedback.Success("Incoming stock transfer received successfully.", "Transfer received");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to receive the stock transfer.", "Transfer not received");
        }

        return result;
    }

    public async Task<ApiCallResult<string>> AcceptAsync(
        PendingStockReceiptViewModel receipt,
        CancellationToken cancellationToken = default)
    {
        if (receipt is null || string.IsNullOrWhiteSpace(receipt.DocumentId))
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.BadRequest,
                "Stock transfer document ID is missing.");
        }

        var result = await AcceptAsync(receipt.DocumentId, cancellationToken);
        if (result.Success)
        {
            RememberAcceptedTransfer(receipt.DocumentId, receipt.DisplayCode);
        }

        return result;
    }

    public bool WasAcceptedTransfer(string? documentId, string? displayCode = null)
    {
        lock (acceptedSync)
        {
            return MatchesAcceptedKey(documentId) || MatchesAcceptedKey(displayCode);
        }
    }

    public void RememberAcceptedTransfer(string? documentId, string? displayCode = null)
    {
        lock (acceptedSync)
        {
            AddAcceptedKey(documentId);
            AddAcceptedKey(displayCode);
        }
    }

    private bool MatchesAcceptedKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && acceptedTransferKeys.Contains(value.Trim());

    private void AddAcceptedKey(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            acceptedTransferKeys.Add(value.Trim());
        }
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

    private static string FirstValue(IEnumerable<string?> values, string fallback) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? fallback;

    private static ApiCallResult<T> Failure<T>(ApiCallResult<List<PendingStockReceiptLineDTO>> source) =>
        source.IsUnauthorized
            ? ApiCallResult<T>.Unauthorized(source.StatusCode)
            : ApiCallResult<T>.Failure(
                source.StatusCode,
                source.ErrorMessage ?? "Pending stock receipt request failed.");
}
