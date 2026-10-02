using System.Text.Json;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

/// <summary>
/// Step 12 local AR Receipt history for receipts created by BeautyAesthetics.
///
/// The current Senang Retail repository contains only mock Outstanding history UI
/// and exposes no proven AR Receipt history/list API beyond RetrieveSettlementLines
/// and CreateRecord. Until the backend supplies a supported list endpoint, Beauty
/// persists only AR Receipts that were actually returned successfully by CreateRecord.
/// This keeps history real (never mock/generated) and avoids guessing an API.
/// </summary>
public sealed class OutstandingPaymentHistoryService
{
    private const string StorageKey = "beauty.outstanding-payment-history.v1";
    private const int MaxHistoryRecords = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime jsRuntime;
    private readonly SemaphoreSlim gate = new(1, 1);

    public OutstandingPaymentHistoryService(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    public async Task<IReadOnlyList<OutstandingPaymentHistoryDTO>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> RecordAsync(
        OutstandingPaymentHistoryDTO record,
        CancellationToken cancellationToken = default)
    {
        if (record is null ||
            (string.IsNullOrWhiteSpace(record.ReceiptID) &&
             string.IsNullOrWhiteSpace(record.ReceiptNo)))
        {
            return false;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var records = (await LoadCoreAsync(cancellationToken)).ToList();

            var existingIndex = records.FindIndex(item =>
                (!string.IsNullOrWhiteSpace(record.ReceiptID) &&
                 string.Equals(
                     item.ReceiptID,
                     record.ReceiptID,
                     StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(record.ReceiptNo) &&
                 string.Equals(
                     item.ReceiptNo,
                     record.ReceiptNo,
                     StringComparison.OrdinalIgnoreCase)));

            if (existingIndex >= 0)
            {
                records[existingIndex] = record;
            }
            else
            {
                records.Add(record);
            }

            records = records
                .OrderByDescending(item => item.PaymentDate)
                .Take(MaxHistoryRecords)
                .ToList();

            var json = JsonSerializer.Serialize(records, JsonOptions);

            await jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                StorageKey,
                json);

            Console.WriteLine(
                $"[Outstanding Step 12] HISTORY SAVED | Receipt={record.ReceiptNo} | " +
                $"Id={record.ReceiptID} | Amount={record.PaidAmount:N2} | " +
                $"Documents={record.Documents.Count} | TotalHistory={records.Count}");

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Outstanding Step 12] HISTORY SAVE WARNING | Receipt={record.ReceiptNo} | " +
                $"Error={ex.Message}");
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<OutstandingPaymentHistoryDTO>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                StorageKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<OutstandingPaymentHistoryDTO>();
            }

            var records = JsonSerializer.Deserialize<List<OutstandingPaymentHistoryDTO>>(
                json,
                JsonOptions)
                ?? new List<OutstandingPaymentHistoryDTO>();

            return records
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.ReceiptID) ||
                    !string.IsNullOrWhiteSpace(item.ReceiptNo))
                .OrderByDescending(item => item.PaymentDate)
                .Take(MaxHistoryRecords)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Outstanding Step 12] HISTORY LOAD WARNING | Error={ex.Message}");
            return Array.Empty<OutstandingPaymentHistoryDTO>();
        }
    }
}
