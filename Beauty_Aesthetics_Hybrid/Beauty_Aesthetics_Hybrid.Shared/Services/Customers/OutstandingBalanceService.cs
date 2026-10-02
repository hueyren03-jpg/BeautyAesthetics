using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.Enum;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public interface IOutstandingBalanceService
{
    Task<CustomerOperationResult<MemberOtherBalanceSummaryDTO>> LoadSummaryAsync(
        string customerId,
        DateTime cutOffDate,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>> LoadDocumentsAsync(
        string customerId,
        string branchGroupId,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<OutstandingSettlementSaveResultDTO>> CreateSettlementAsync(
        OutstandingSettlementSaveRequestDTO request,
        CancellationToken cancellationToken = default);
}

public sealed class OutstandingBalanceService : IOutstandingBalanceService
{
    private readonly ICustomerService customerService;
    private readonly ARReceiptAC arReceiptAC;

    public OutstandingBalanceService(
        ICustomerService customerService,
        ARReceiptAC arReceiptAC)
    {
        this.customerService = customerService;
        this.arReceiptAC = arReceiptAC;
    }

    public async Task<CustomerOperationResult<MemberOtherBalanceSummaryDTO>> LoadSummaryAsync(
        string customerId,
        DateTime cutOffDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerId = customerId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            return CustomerOperationResult<MemberOtherBalanceSummaryDTO>.Fail(
                "Customer ID is required.");
        }

        // Outstanding Step 1 is summary/display only.
        // Do not treat this as invoice settlement or a POS payment.
        var result = await customerService.GetOtherBalanceSummaryAsync(
            normalizedCustomerId,
            cutOffDate,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<MemberOtherBalanceSummaryDTO>.Fail(
                result.ErrorMessage ?? "Unable to load outstanding balance.");
        }

        Console.WriteLine(
            $"[Outstanding Step 1] Customer={normalizedCustomerId} | " +
            $"CutOffDate={cutOffDate:yyyy-MM-dd} | Outstanding={result.Value.Outstanding:N2}");

        return CustomerOperationResult<MemberOtherBalanceSummaryDTO>.Ok(result.Value);
    }

    public async Task<CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>> LoadDocumentsAsync(
        string customerId,
        string branchGroupId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerId = customerId?.Trim() ?? string.Empty;
        var normalizedGroupId = branchGroupId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Fail(
                "Customer ID is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedGroupId))
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Fail(
                "Branch group ID is required to load outstanding documents.");
        }

        var result = await arReceiptAC.RetrieveSettlementLinesAsync(
            normalizedCustomerId,
            normalizedGroupId,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Fail(
                result.ErrorMessage ?? "Unable to load outstanding documents.");
        }

        // Senang settles only rows where Outstanding > 0 and allocates oldest
        // FinancialDate first. Step 2 keeps that same ordering but remains read-only.
        var documents = result.Value
            .Where(document => document.RemainingAmount > 0m)
            .OrderBy(document =>
                document.FinancialDate.Year <= 1900
                    ? DateTime.MaxValue
                    : document.FinancialDate)
            .ThenBy(document => document.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine(
            $"[Outstanding Step 2] Customer={normalizedCustomerId} | " +
            $"Group={normalizedGroupId} | Documents={documents.Count} | " +
            $"TotalOutstanding={documents.Sum(document => document.RemainingAmount):N2}");

        foreach (var document in documents)
        {
            Console.WriteLine(
                $"[Outstanding Step 2] DOCUMENT | Source={document.SourceId} | " +
                $"No={document.DocumentNumber} | Date={document.FinancialDate:yyyy-MM-dd} | " +
                $"Original={document.EffectiveOriginalAmount:N2} | " +
                $"Paid={document.EffectivePaidAmount:N2} | " +
                $"Remaining={document.RemainingAmount:N2} | " +
                $"Due={(document.DueDate.Year <= 1900 ? "—" : document.DueDate.ToString("yyyy-MM-dd"))} | " +
                $"Branch={document.BranchID}");
        }

        return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Ok(documents);
    }

    public async Task<CustomerOperationResult<OutstandingSettlementSaveResultDTO>> CreateSettlementAsync(
        OutstandingSettlementSaveRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var customerId = request.CustomerID?.Trim() ?? string.Empty;
        var branchId = request.BranchID?.Trim() ?? string.Empty;
        var groupId = request.GroupID?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Customer ID is required.");
        }

        if (string.IsNullOrWhiteSpace(branchId))
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Branch ID is required.");
        }

        if (string.IsNullOrWhiteSpace(groupId))
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Branch group ID is required.");
        }

        var requested = request.SelectedAmounts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0m)
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => Math.Round(pair.Value, 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        if (requested.Count == 0)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Select at least one outstanding document to settle.");
        }

        // Step 5: never save against the stale Step 2/3 display rows.
        // Re-fetch the full EBI settlement objects immediately before CreateRecord.
        var rawResult = await arReceiptAC.RetrieveSettlementLinesRawAsync(
            customerId,
            groupId,
            cancellationToken);

        if (!rawResult.Success || rawResult.Value is null)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                rawResult.ErrorMessage ?? "Unable to revalidate outstanding documents.");
        }

        var rawLines = rawResult.Value
            .Where(line => line.Outstanding > 0m)
            .OrderBy(line => line.FinancialDate)
            .ToList();

        var usedLines = new HashSet<ud_ARAPPaymentOffSetLineDM>();
        var offsetLines = new ObservableCollection<ud_ARAPPaymentOffSetLineDM>();
        var now = DateTime.Now;

        if (requested.Count > 1)
        {
            Console.WriteLine(
                $"[Outstanding Step 7] MULTI-INVOICE START | Customer={customerId} | " +
                $"Documents={requested.Count} | " +
                $"RequestedTotal={requested.Values.Sum():N2}");
        }

        foreach (var selection in requested)
        {
            var line = rawLines.FirstOrDefault(candidate =>
                !usedLines.Contains(candidate) &&
                SettlementLineAliases(candidate).Contains(
                    selection.Key,
                    StringComparer.OrdinalIgnoreCase));

            if (line is null)
            {
                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {selection.Key} is no longer available. Refresh and try again.");
            }

            var liveOutstanding = Math.Round(
                Math.Max(0m, line.Outstanding),
                2,
                MidpointRounding.AwayFromZero);

            if (selection.Value > liveOutstanding + 0.009m)
            {
                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {selection.Key} now has only RM {liveOutstanding:N2} available.");
            }

            var allocated = Math.Round(
                Math.Min(selection.Value, liveOutstanding),
                2,
                MidpointRounding.AwayFromZero);

            var remainingAfterSettlement = Math.Round(
                Math.Max(0m, liveOutstanding - allocated),
                2,
                MidpointRounding.AwayFromZero);

            Console.WriteLine(
                $"[Outstanding Step 6] {(remainingAfterSettlement > 0m ? "PARTIAL" : "FULL")} SETTLEMENT | " +
                $"Source={selection.Key} | Document={line.DisplayCode} | " +
                $"Before={liveOutstanding:N2} | Pay={allocated:N2} | " +
                $"RemainingAfter={remainingAfterSettlement:N2}");

            line.AllocatedAmount = allocated;
            line.LocalAllocatedAmount = allocated;
            line.ActualLocalSettlement = allocated;
            line.SettlementDate = now;
            line.SettlementExchangeRate = 1.0m;
            line.SaveAction = EntityState.Added;
            line.IsDirty = true;

            usedLines.Add(line);
            offsetLines.Add(line);

            if (requested.Count > 1)
            {
                Console.WriteLine(
                    $"[Outstanding Step 7] LINE | Source={selection.Key} | " +
                    $"Document={line.DisplayCode} | Allocated={allocated:N2} | " +
                    $"RemainingAfter={remainingAfterSettlement:N2}");
            }
        }

        if (offsetLines.Count != requested.Count)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Not all selected outstanding documents could be matched to live settlement lines.");
        }

        var requestedTotal = Math.Round(
            requested.Values.Sum(),
            2,
            MidpointRounding.AwayFromZero);

        var totalAllocated = Math.Round(
            offsetLines.Sum(line => line.AllocatedAmount),
            2,
            MidpointRounding.AwayFromZero);

        if (Math.Abs(totalAllocated - requestedTotal) > 0.009m)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                $"Outstanding settlement total changed during validation. Requested RM {requestedTotal:N2}, validated RM {totalAllocated:N2}.");
        }

        if (requested.Count > 1)
        {
            Console.WriteLine(
                $"[Outstanding Step 7] MULTI-INVOICE VALIDATED | Documents={offsetLines.Count} | " +
                $"TotalAllocated={totalAllocated:N2}");
        }

        if (totalAllocated <= 0m)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Outstanding settlement amount must be greater than zero.");
        }

        var currencyId = offsetLines
            .Select(line => line.CurrencyID)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim()
            ?? request.CurrencyID?.Trim()
            ?? string.Empty;

        var currencyName = offsetLines
            .Select(line => line.CurrencyName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim()
            ?? request.CurrencyName?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currencyId))
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Currency is unavailable for this outstanding settlement.");
        }

        var bankAccountId = string.IsNullOrWhiteSpace(request.FinancialAccountID)
            ? "000000000000005"
            : request.FinancialAccountID.Trim();
        var bankAccountName = string.IsNullOrWhiteSpace(request.FinancialAccountName)
            ? "Bank Account"
            : request.FinancialAccountName.Trim();

        foreach (var line in offsetLines)
        {
            if (string.IsNullOrWhiteSpace(line.CurrencyID))
            {
                line.CurrencyID = currencyId;
                line.CurrencyName = currencyName;
            }
        }

        var receipt = new OutstandingARReceiptCreateDTO
        {
            Header = new OutstandingARReceiptHeaderDTO
            {
                BranchID = branchId,
                EditBranchID = branchId,
                AccountID = customerId,
                AccountName = request.CustomerName?.Trim() ?? string.Empty,
                ReferenceNumber = string.Empty,
                CustomerCurrencyID = currencyId,
                CustomerCurrencyName = currencyName,
                LocalCurrencyID = currencyId,
                LocalCurrencyName = currencyName,
                ExchangeRate = 1m,
                Remarks = string.Empty,
                GroupID = groupId,
                BankExchangeRate = 1m,
                BankAccountID = bankAccountId,
                BankAccountName = bankAccountName,
                BankCurrencyID = currencyId,
                BankCurrencyName = currencyName,
                BankAmountReceived = totalAllocated,
                BankCharges = 0m,
                TotalAllocatedAmount = totalAllocated,
                SaveAction = 1,
                FinancialAccountID = bankAccountId,
                IsDirty = true,
                blnIsPeriodClosed = true,
                blnIsBankReconciliationDone = true
            },
            SettlementLines = offsetLines
        };

        Console.WriteLine(
            $"[Outstanding Step 5] CREATE AR RECEIPT | Customer={customerId} | " +
            $"Branch={branchId} | Group={groupId} | Documents={offsetLines.Count} | " +
            $"Amount={totalAllocated:N2} | FinancialAccount={bankAccountId}");

        var saveResult = await arReceiptAC.CreateRecordAsync(
            receipt,
            totalAllocated,
            offsetLines.Count,
            cancellationToken);

        if (!saveResult.Success || saveResult.Value is null)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                saveResult.ErrorMessage ?? "Unable to save outstanding settlement.");
        }

        Console.WriteLine(
            $"[Outstanding Step 5] AR RECEIPT SAVED | Id={saveResult.Value.Id} | " +
            $"DisplayCode={saveResult.Value.DisplayCode} | " +
            $"Amount={saveResult.Value.TotalAllocatedAmount:N2}");

        if (requested.Count > 1)
        {
            Console.WriteLine(
                $"[Outstanding Step 7] MULTI-INVOICE SAVE COMPLETE | " +
                $"Receipt={saveResult.Value.DisplayCode} | Documents={offsetLines.Count} | " +
                $"Amount={saveResult.Value.TotalAllocatedAmount:N2}");
        }

        return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Ok(saveResult.Value);
    }

    private static IReadOnlyList<string> SettlementLineAliases(
        ud_ARAPPaymentOffSetLineDM line)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = JsonSerializer.SerializeToElement(line);
            AddAlias(json, aliases, "SourceDocumentID");
            AddAlias(json, aliases, "SourceDocumentId");
            AddAlias(json, aliases, "DocumentID");
            AddAlias(json, aliases, "DocumentId");
            AddAlias(json, aliases, "ARAPOutstandingID");
            AddAlias(json, aliases, "ARAPOutstandingId");
            AddAlias(json, aliases, "ID");
            AddAlias(json, aliases, "Id");
            AddAlias(json, aliases, "DisplayCode");
        }
        catch
        {
        }

        return aliases.ToList();
    }

    private static void AddAlias(
        JsonElement element,
        ISet<string> aliases,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                aliases.Add(value.Trim());
            }
        }
    }
}
