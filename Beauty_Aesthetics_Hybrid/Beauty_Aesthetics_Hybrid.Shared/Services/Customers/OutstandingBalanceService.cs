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

    Task<CustomerOperationResult<IReadOnlyList<OutstandingARReceiptHistoryDTO>>> LoadPaymentHistoryAsync(
        string branchId,
        DateTime startDate,
        DateTime endDate,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<OutstandingARReceiptDetailDTO>> LoadPaymentReceiptAsync(
        string documentId,
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

    public async Task<CustomerOperationResult<IReadOnlyList<OutstandingARReceiptHistoryDTO>>> LoadPaymentHistoryAsync(
        string branchId,
        DateTime startDate,
        DateTime endDate,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchId = branchId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedBranchId))
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingARReceiptHistoryDTO>>.Fail(
                "Branch ID is required to load Outstanding payment history.");
        }

        if (startDate > endDate)
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingARReceiptHistoryDTO>>.Fail(
                "Outstanding history start date cannot be after the end date.");
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await arReceiptAC.LoadHistoryAsync(
            new OutstandingARReceiptHistoryRequestDTO
            {
                BranchID = normalizedBranchId,
                StartDate = startDate,
                EndDate = endDate,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingARReceiptHistoryDTO>>.Fail(
                result.ErrorMessage ?? "Unable to load Outstanding payment history.");
        }

        var receipts = result.Value
            .Where(receipt =>
                receipt.DocumentTypeID == 11 ||
                receipt.FriendlyDocumentName.Equals(
                    "CustomerReceipt",
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(receipt =>
                receipt.FinancialDate.Year <= 1900
                    ? receipt.CreatedDateTime
                    : receipt.FinancialDate)
            .ThenByDescending(receipt => receipt.DisplayCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine(
            $"[Outstanding Step 12] HISTORY LOADED | Branch={normalizedBranchId} | " +
            $"From={startDate:yyyy-MM-dd} | To={endDate:yyyy-MM-dd} | " +
            $"Page={pageNumber} | PageSize={pageSize} | Records={receipts.Count} | " +
            $"Total={receipts.Sum(receipt => receipt.EffectiveAmount):N2}");

        return CustomerOperationResult<IReadOnlyList<OutstandingARReceiptHistoryDTO>>.Ok(receipts);
    }

    public async Task<CustomerOperationResult<OutstandingARReceiptDetailDTO>> LoadPaymentReceiptAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var normalizedDocumentId = documentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDocumentId))
        {
            return CustomerOperationResult<OutstandingARReceiptDetailDTO>.Fail(
                "AR Receipt Document ID is required.");
        }

        var result = await arReceiptAC.LoadRecordAsync(
            normalizedDocumentId,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<OutstandingARReceiptDetailDTO>.Fail(
                result.ErrorMessage ?? "Unable to load Outstanding payment receipt.");
        }

        Console.WriteLine(
            $"[Outstanding Step 12] RECEIPT LOADED | Id={normalizedDocumentId} | " +
            $"Receipt={result.Value.Receipt.DisplayCode} | " +
            $"Customer={result.Value.Receipt.AccountID} | " +
            $"Amount={result.Value.Receipt.EffectiveAmount:N2} | " +
            $"Documents={result.Value.SettlementLines.Count}");

        return CustomerOperationResult<OutstandingARReceiptDetailDTO>.Ok(result.Value);
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

        var payments = request.Payments
            .Where(payment =>
                payment.PaymentTypeID != 0 &&
                payment.Amount > 0m)
            .Select(payment => new OutstandingSettlementPaymentDTO
            {
                PaymentTypeID = payment.PaymentTypeID,
                PaymentMethod = payment.PaymentMethod?.Trim() ?? string.Empty,
                FinancialAccountID = payment.FinancialAccountID?.Trim() ?? string.Empty,
                FinancialAccountName = payment.FinancialAccountName?.Trim() ?? string.Empty,
                Amount = Math.Round(
                    payment.Amount,
                    2,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();

        if (payments.Any(payment => payment.PaymentTypeID == -10))
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Member Credit cannot be used for Outstanding settlement.");
        }

        if (payments.Count > 1)
        {
            return await CreateSplitSettlementAsync(
                request,
                payments,
                cancellationToken);
        }

        if (payments.Count == 1)
        {
            request.FinancialAccountID = payments[0].FinancialAccountID;
            request.FinancialAccountName = payments[0].FinancialAccountName;
        }

        var requested = request.SelectedAmounts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0m)
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => Math.Round(pair.Value, 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        var expectedOutstanding = request.ExpectedOutstandingAmounts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value >= 0m)
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => Math.Round(pair.Value, 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        if (requested.Count == 0)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Select at least one outstanding document to settle.");
        }

        Console.WriteLine(
            $"[Outstanding Step 9] REVALIDATE START | Customer={customerId} | " +
            $"Documents={requested.Count} | SnapshotDocuments={expectedOutstanding.Count} | " +
            $"RequestedTotal={requested.Values.Sum():N2}");

        // Step 9: never save against the stale Step 2/3 display rows.
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

            if (expectedOutstanding.TryGetValue(selection.Key, out var expectedBalance) &&
                Math.Abs(expectedBalance - liveOutstanding) > 0.009m)
            {
                Console.WriteLine(
                    $"[Outstanding Step 9] STALE BALANCE BLOCKED | Source={selection.Key} | " +
                    $"Document={line.DisplayCode} | Expected={expectedBalance:N2} | " +
                    $"Live={liveOutstanding:N2} | Requested={selection.Value:N2}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {line.DisplayCode} changed from RM {expectedBalance:N2} to RM {liveOutstanding:N2}. " +
                    "The latest balance has been reloaded. Review the amount and try again.");
            }

            if (selection.Value > liveOutstanding + 0.009m)
            {
                Console.WriteLine(
                    $"[Outstanding Step 9] OVERPAYMENT BLOCKED | Source={selection.Key} | " +
                    $"Document={line.DisplayCode} | Live={liveOutstanding:N2} | Requested={selection.Value:N2}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {line.DisplayCode} now has only RM {liveOutstanding:N2} available. " +
                    "The latest balance has been reloaded. Review the amount and try again.");
            }

            Console.WriteLine(
                $"[Outstanding Step 9] REVALIDATE PASS | Source={selection.Key} | " +
                $"Document={line.DisplayCode} | Live={liveOutstanding:N2} | Requested={selection.Value:N2}");

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

        // Step 10: enforce the exact AR Receipt save contract before the API call.
        // Every settlement row must retain a real source document and belong to
        // the selected customer; header totals must equal the settlement rows.
        foreach (var line in offsetLines)
        {
            if (string.IsNullOrWhiteSpace(line.DocumentID))
            {
                Console.WriteLine(
                    $"[Outstanding Step 10] SAVE BLOCKED | Reason=Missing source DocumentID | " +
                    $"DisplayCode={line.DisplayCode}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {line.DisplayCode} is missing its source document ID. Refresh and try again.");
            }

            if (!string.IsNullOrWhiteSpace(line.CustomerAccountID) &&
                !string.Equals(
                    line.CustomerAccountID.Trim(),
                    customerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"[Outstanding Step 10] SAVE BLOCKED | Reason=Customer mismatch | " +
                    $"Document={line.DisplayCode} | ExpectedCustomer={customerId} | " +
                    $"LineCustomer={line.CustomerAccountID}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {line.DisplayCode} no longer belongs to the selected customer. Refresh and try again.");
            }

            if (line.AllocatedAmount <= 0m ||
                Math.Abs(line.AllocatedAmount - line.LocalAllocatedAmount) > 0.009m ||
                Math.Abs(line.AllocatedAmount - line.ActualLocalSettlement) > 0.009m)
            {
                Console.WriteLine(
                    $"[Outstanding Step 10] SAVE BLOCKED | Reason=Invalid settlement amounts | " +
                    $"Document={line.DisplayCode} | Allocated={line.AllocatedAmount:N2} | " +
                    $"Local={line.LocalAllocatedAmount:N2} | Actual={line.ActualLocalSettlement:N2}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding settlement values for {line.DisplayCode} are invalid. Refresh and try again.");
            }
        }

        var saveLineTotal = Math.Round(
            offsetLines.Sum(line => line.AllocatedAmount),
            2,
            MidpointRounding.AwayFromZero);

        if (Math.Abs(saveLineTotal - totalAllocated) > 0.009m)
        {
            Console.WriteLine(
                $"[Outstanding Step 10] SAVE BLOCKED | Reason=Header/line total mismatch | " +
                $"Header={totalAllocated:N2} | Lines={saveLineTotal:N2}");

            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                $"Outstanding settlement total mismatch. Header RM {totalAllocated:N2}, settlement lines RM {saveLineTotal:N2}.");
        }

        Console.WriteLine(
            $"[Outstanding Step 10] SAVE PREFLIGHT PASS | Customer={customerId} | " +
            $"Documents={offsetLines.Count} | Amount={totalAllocated:N2} | " +
            $"FinancialAccount={bankAccountId}");

        foreach (var line in offsetLines)
        {
            Console.WriteLine(
                $"[Outstanding Step 10] SOURCE LINE | DocumentID={line.DocumentID} | " +
                $"DisplayCode={line.DisplayCode} | Allocated={line.AllocatedAmount:N2} | " +
                $"ActualSettlement={line.ActualLocalSettlement:N2}");
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
            $"[Outstanding Step 10] CREATE AR RECEIPT | Endpoint=/api/Doc_ARReceipt/CreateRecord | " +
            $"Customer={customerId} | Branch={branchId} | Group={groupId} | " +
            $"Documents={offsetLines.Count} | Amount={totalAllocated:N2} | " +
            $"FinancialAccount={bankAccountId}");

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

        await VerifySettlementAfterSaveAsync(
            customerId,
            groupId,
            offsetLines,
            saveResult.Value,
            cancellationToken);

        Console.WriteLine(
            $"[Outstanding Step 10] SAVE COMPLETE | Id={saveResult.Value.Id} | " +
            $"DisplayCode={saveResult.Value.DisplayCode} | " +
            $"Amount={saveResult.Value.TotalAllocatedAmount:N2} | " +
            $"SourceDocuments={offsetLines.Count}");

        if (requested.Count > 1)
        {
            Console.WriteLine(
                $"[Outstanding Step 7] MULTI-INVOICE SAVE COMPLETE | " +
                $"Receipt={saveResult.Value.DisplayCode} | Documents={offsetLines.Count} | " +
                $"Amount={saveResult.Value.TotalAllocatedAmount:N2}");
        }

        return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Ok(saveResult.Value);
    }

    private async Task<CustomerOperationResult<OutstandingSettlementSaveResultDTO>> CreateSplitSettlementAsync(
        OutstandingSettlementSaveRequestDTO request,
        IReadOnlyList<OutstandingSettlementPaymentDTO> payments,
        CancellationToken cancellationToken)
    {
        var customerId = request.CustomerID?.Trim() ?? string.Empty;
        var groupId = request.GroupID?.Trim() ?? string.Empty;

        var requested = request.SelectedAmounts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0m)
            .Select(pair => new KeyValuePair<string, decimal>(
                pair.Key.Trim(),
                Math.Round(
                    pair.Value,
                    2,
                    MidpointRounding.AwayFromZero)))
            .ToList();

        var expectedOutstanding = request.ExpectedOutstandingAmounts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value >= 0m)
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => Math.Round(pair.Value, 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        if (requested.Count == 0)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Select at least one outstanding document to settle.");
        }

        var selectedTotal = Math.Round(
            requested.Sum(pair => pair.Value),
            2,
            MidpointRounding.AwayFromZero);

        var paymentTotal = Math.Round(
            payments.Sum(payment => payment.Amount),
            2,
            MidpointRounding.AwayFromZero);

        if (Math.Abs(selectedTotal - paymentTotal) > 0.009m)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                $"Split payment total must equal RM {selectedTotal:N2}. Current payment total is RM {paymentTotal:N2}.");
        }

        // Validate every selected document before the first split receipt is saved.
        // Each individual receipt is still revalidated again by CreateSettlementAsync.
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

            if (expectedOutstanding.TryGetValue(selection.Key, out var expectedBalance) &&
                Math.Abs(expectedBalance - liveOutstanding) > 0.009m)
            {
                Console.WriteLine(
                    $"[Outstanding Step 9] SPLIT STALE BALANCE BLOCKED | Source={selection.Key} | " +
                    $"Document={line.DisplayCode} | Expected={expectedBalance:N2} | " +
                    $"Live={liveOutstanding:N2} | Requested={selection.Value:N2}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {line.DisplayCode} changed from RM {expectedBalance:N2} to RM {liveOutstanding:N2}. " +
                    "The latest balance has been reloaded. Review the split payment and try again.");
            }

            if (selection.Value > liveOutstanding + 0.009m)
            {
                Console.WriteLine(
                    $"[Outstanding Step 9] SPLIT OVERPAYMENT BLOCKED | Source={selection.Key} | " +
                    $"Document={line.DisplayCode} | Live={liveOutstanding:N2} | Requested={selection.Value:N2}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Outstanding document {line.DisplayCode} now has only RM {liveOutstanding:N2} available. " +
                    "The latest balance has been reloaded. Review the split payment and try again.");
            }

            if (!expectedOutstanding.ContainsKey(selection.Key))
            {
                expectedOutstanding[selection.Key] = liveOutstanding;
            }

            Console.WriteLine(
                $"[Outstanding Step 9] SPLIT REVALIDATE PASS | Source={selection.Key} | " +
                $"Document={line.DisplayCode} | Live={liveOutstanding:N2} | Requested={selection.Value:N2}");

            usedLines.Add(line);
        }

        var remainingByDocument = requested.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        var plans = new List<(OutstandingSettlementPaymentDTO Payment, Dictionary<string, decimal> Allocations)>();

        foreach (var payment in payments)
        {
            var remainingPayment = payment.Amount;
            var allocations = new Dictionary<string, decimal>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var selection in requested)
            {
                if (remainingPayment <= 0.009m)
                {
                    break;
                }

                var remainingTarget = remainingByDocument[selection.Key];
                if (remainingTarget <= 0.009m)
                {
                    continue;
                }

                var allocated = Math.Round(
                    Math.Min(remainingTarget, remainingPayment),
                    2,
                    MidpointRounding.AwayFromZero);

                if (allocated <= 0m)
                {
                    continue;
                }

                allocations[selection.Key] = allocated;
                remainingByDocument[selection.Key] = Math.Round(
                    remainingTarget - allocated,
                    2,
                    MidpointRounding.AwayFromZero);
                remainingPayment = Math.Round(
                    remainingPayment - allocated,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            if (remainingPayment > 0.009m || allocations.Count == 0)
            {
                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    $"Unable to allocate split payment {payment.PaymentMethod} RM {payment.Amount:N2} to the selected outstanding documents.");
            }

            plans.Add((payment, allocations));
        }

        if (remainingByDocument.Values.Any(value => value > 0.009m))
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                "Split payment allocations do not fully cover the selected outstanding amount.");
        }

        Console.WriteLine(
            $"[Outstanding Step 8] SPLIT PAYMENT START | Customer={customerId} | " +
            $"Methods={plans.Count} | Documents={requested.Count} | Total={selectedTotal:N2}");

        foreach (var plan in plans)
        {
            Console.WriteLine(
                $"[Outstanding Step 8] METHOD | Type={plan.Payment.PaymentTypeID} | " +
                $"Method={plan.Payment.PaymentMethod} | Amount={plan.Payment.Amount:N2} | " +
                $"FinancialAccount={plan.Payment.FinancialAccountID} | " +
                $"Documents={plan.Allocations.Count}");
        }

        var receiptIds = new List<string>();
        var receiptCodes = new List<string>();
        var verifiedRemaining = new Dictionary<string, decimal>(
            StringComparer.OrdinalIgnoreCase);
        var verificationMessages = new List<string>();
        var allVerificationsCompleted = true;
        var allVerificationsPassed = true;
        decimal? verifiedCustomerOutstanding = null;
        decimal savedTotal = 0m;

        foreach (var plan in plans)
        {
            var saveResult = await CreateSettlementAsync(
                new OutstandingSettlementSaveRequestDTO
                {
                    CustomerID = request.CustomerID,
                    CustomerName = request.CustomerName,
                    BranchID = request.BranchID,
                    GroupID = request.GroupID,
                    CurrencyID = request.CurrencyID,
                    CurrencyName = request.CurrencyName,
                    FinancialAccountID = plan.Payment.FinancialAccountID,
                    FinancialAccountName = plan.Payment.FinancialAccountName,
                    ExpectedOutstandingAmounts = plan.Allocations.Keys
                        .Where(expectedOutstanding.ContainsKey)
                        .ToDictionary(
                            key => key,
                            key => expectedOutstanding[key],
                            StringComparer.OrdinalIgnoreCase),
                    SelectedAmounts = new Dictionary<string, decimal>(
                        plan.Allocations,
                        StringComparer.OrdinalIgnoreCase)
                },
                cancellationToken);

            if (!saveResult.Success || saveResult.Value is null)
            {
                Console.WriteLine(
                    $"[Outstanding Step 8] SPLIT PAYMENT FAILED | Method={plan.Payment.PaymentMethod} | " +
                    $"Amount={plan.Payment.Amount:N2} | SavedBeforeFailure={savedTotal:N2}");

                return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                    savedTotal > 0m
                        ? $"Split payment stopped after RM {savedTotal:N2} was already saved. Reload Outstanding before retrying. {saveResult.ErrorMessage}"
                        : saveResult.ErrorMessage ?? "Unable to save split Outstanding settlement.");
            }

            savedTotal += saveResult.Value.TotalAllocatedAmount;

            allVerificationsCompleted &= saveResult.Value.VerificationCompleted;
            allVerificationsPassed &= saveResult.Value.VerificationPassed;

            if (!string.IsNullOrWhiteSpace(saveResult.Value.VerificationMessage))
            {
                verificationMessages.Add(saveResult.Value.VerificationMessage);
            }

            foreach (var pair in saveResult.Value.VerifiedRemainingAmounts)
            {
                verifiedRemaining[pair.Key] = pair.Value;
            }

            if (saveResult.Value.VerifiedCustomerOutstanding.HasValue)
            {
                verifiedCustomerOutstanding =
                    saveResult.Value.VerifiedCustomerOutstanding.Value;
            }

            foreach (var allocation in plan.Allocations)
            {
                if (expectedOutstanding.TryGetValue(allocation.Key, out var before))
                {
                    expectedOutstanding[allocation.Key] = Math.Round(
                        Math.Max(0m, before - allocation.Value),
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }

            if (!string.IsNullOrWhiteSpace(saveResult.Value.Id))
            {
                receiptIds.Add(saveResult.Value.Id);
            }

            if (!string.IsNullOrWhiteSpace(saveResult.Value.DisplayCode))
            {
                receiptCodes.Add(saveResult.Value.DisplayCode);
            }

            Console.WriteLine(
                $"[Outstanding Step 8] RECEIPT SAVED | Method={plan.Payment.PaymentMethod} | " +
                $"Receipt={saveResult.Value.DisplayCode} | Amount={saveResult.Value.TotalAllocatedAmount:N2}");
        }

        savedTotal = Math.Round(
            savedTotal,
            2,
            MidpointRounding.AwayFromZero);

        if (Math.Abs(savedTotal - selectedTotal) > 0.009m)
        {
            return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Fail(
                $"Split Outstanding settlement saved RM {savedTotal:N2}, expected RM {selectedTotal:N2}. Reload Outstanding before retrying.");
        }

        Console.WriteLine(
            $"[Outstanding Step 8] SPLIT PAYMENT COMPLETE | Receipts={receiptCodes.Count} | " +
            $"Methods={plans.Count} | Total={savedTotal:N2}");

        return CustomerOperationResult<OutstandingSettlementSaveResultDTO>.Ok(
            new OutstandingSettlementSaveResultDTO
            {
                Id = receiptIds.FirstOrDefault() ?? string.Empty,
                DisplayCode = receiptCodes.Count > 0
                    ? string.Join(", ", receiptCodes)
                    : string.Empty,
                TotalAllocatedAmount = savedTotal,
                SettledDocumentCount = requested.Count,
                ReceiptIds = receiptIds,
                ReceiptCodes = receiptCodes,
                VerificationCompleted = allVerificationsCompleted,
                VerificationPassed = allVerificationsPassed,
                VerificationMessage = allVerificationsPassed
                    ? $"All {receiptCodes.Count} Outstanding receipt(s) were verified against the latest backend balances."
                    : string.Join(" ", verificationMessages.Distinct(StringComparer.OrdinalIgnoreCase)),
                VerifiedCustomerOutstanding = verifiedCustomerOutstanding,
                VerifiedRemainingAmounts = verifiedRemaining
            });
    }

    private async Task VerifySettlementAfterSaveAsync(
        string customerId,
        string groupId,
        IReadOnlyCollection<ud_ARAPPaymentOffSetLineDM> savedLines,
        OutstandingSettlementSaveResultDTO saveResult,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"[Outstanding Step 11] VERIFY START | Receipt={saveResult.DisplayCode} | " +
            $"Customer={customerId} | Documents={savedLines.Count} | " +
            $"Paid={saveResult.TotalAllocatedAmount:N2}");

        var verificationResult = await arReceiptAC.RetrieveSettlementLinesRawAsync(
            customerId,
            groupId,
            cancellationToken);

        if (!verificationResult.Success || verificationResult.Value is null)
        {
            saveResult.VerificationCompleted = false;
            saveResult.VerificationPassed = false;
            saveResult.VerificationMessage =
                $"AR Receipt {saveResult.DisplayCode} was saved, but the latest Outstanding lines could not be reloaded for verification.";

            Console.WriteLine(
                $"[Outstanding Step 11] VERIFY NOT CONFIRMED | Receipt={saveResult.DisplayCode} | " +
                $"Reason={verificationResult.ErrorMessage ?? "Unable to reload settlement lines"}");
            return;
        }

        var liveLines = verificationResult.Value;
        var allLinesPassed = true;

        foreach (var savedLine in savedLines)
        {
            var before = Math.Round(
                Math.Max(0m, savedLine.Outstanding),
                2,
                MidpointRounding.AwayFromZero);
            var paid = Math.Round(
                Math.Max(0m, savedLine.AllocatedAmount),
                2,
                MidpointRounding.AwayFromZero);
            var expectedRemaining = Math.Round(
                Math.Max(0m, before - paid),
                2,
                MidpointRounding.AwayFromZero);

            var savedAliases = SettlementLineAliases(savedLine);
            var liveLine = liveLines.FirstOrDefault(candidate =>
                SettlementLineAliases(candidate).Any(alias =>
                    savedAliases.Contains(alias, StringComparer.OrdinalIgnoreCase)));

            // Senang removes fully settled rows from RetrieveSettlementLines,
            // so an absent line is correctly interpreted as zero remaining.
            var liveRemaining = liveLine is null
                ? 0m
                : Math.Round(
                    Math.Max(0m, liveLine.Outstanding),
                    2,
                    MidpointRounding.AwayFromZero);

            var verificationKey =
                !string.IsNullOrWhiteSpace(savedLine.DocumentID)
                    ? savedLine.DocumentID
                    : !string.IsNullOrWhiteSpace(savedLine.DisplayCode)
                        ? savedLine.DisplayCode
                        : savedAliases.FirstOrDefault() ?? "unknown";

            saveResult.VerifiedRemainingAmounts[verificationKey] = liveRemaining;

            var linePassed =
                Math.Abs(expectedRemaining - liveRemaining) <= 0.009m;
            allLinesPassed &= linePassed;

            Console.WriteLine(
                $"[Outstanding Step 11] DOCUMENT {(linePassed ? "PASS" : "MISMATCH")} | " +
                $"DocumentID={savedLine.DocumentID} | DisplayCode={savedLine.DisplayCode} | " +
                $"Before={before:N2} | Paid={paid:N2} | " +
                $"ExpectedRemaining={expectedRemaining:N2} | LiveRemaining={liveRemaining:N2}");
        }

        var summaryResult = await customerService.GetOtherBalanceSummaryAsync(
            customerId,
            DateTime.Now,
            cancellationToken);

        if (summaryResult.Success && summaryResult.Value is not null)
        {
            saveResult.VerifiedCustomerOutstanding =
                Math.Round(
                    summaryResult.Value.Outstanding,
                    2,
                    MidpointRounding.AwayFromZero);

            Console.WriteLine(
                $"[Outstanding Step 11] SUMMARY RELOADED | Customer={customerId} | " +
                $"Outstanding={saveResult.VerifiedCustomerOutstanding.Value:N2}");
        }
        else
        {
            Console.WriteLine(
                $"[Outstanding Step 11] SUMMARY NOT CONFIRMED | Customer={customerId} | " +
                $"Reason={summaryResult.ErrorMessage ?? "Unable to reload summary"}");
        }

        saveResult.VerificationCompleted =
            summaryResult.Success && summaryResult.Value is not null;
        saveResult.VerificationPassed =
            allLinesPassed && saveResult.VerificationCompleted;

        saveResult.VerificationMessage = saveResult.VerificationPassed
            ? $"AR Receipt {saveResult.DisplayCode} was verified against the latest Outstanding balances."
            : allLinesPassed
                ? $"AR Receipt {saveResult.DisplayCode} was saved and its source documents were verified, but the overall Outstanding summary could not be confirmed."
                : $"AR Receipt {saveResult.DisplayCode} was saved, but the post-save Outstanding balance did not match the expected reduction. Refresh Outstanding before taking any further action.";

        Console.WriteLine(
            $"[Outstanding Step 11] VERIFY {(saveResult.VerificationPassed ? "PASS" : "WARNING")} | " +
            $"Receipt={saveResult.DisplayCode} | Documents={savedLines.Count} | " +
            $"SummaryOutstanding={(saveResult.VerifiedCustomerOutstanding.HasValue ? saveResult.VerifiedCustomerOutstanding.Value.ToString("N2") : "unavailable")}");
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
