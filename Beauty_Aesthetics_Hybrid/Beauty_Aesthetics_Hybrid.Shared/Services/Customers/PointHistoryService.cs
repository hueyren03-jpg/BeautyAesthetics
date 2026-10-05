using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public sealed class PointHistoryService : IPointHistoryService
{
    private readonly WebDashboardAC webDashboardAC;
    private readonly ICustomerService customerService;

    public PointHistoryService(
        WebDashboardAC webDashboardAC,
        ICustomerService customerService)
    {
        this.webDashboardAC = webDashboardAC;
        this.customerService = customerService;
    }

    public async Task<CustomerOperationResult<PointHistorySnapshotDTO>> LoadHistoryAsync(
        string customerId,
        string branchId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        customerId = customerId?.Trim() ?? string.Empty;
        branchId = branchId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return CustomerOperationResult<PointHistorySnapshotDTO>.Fail(
                "Customer ID is required before point history can be loaded.");
        }

        // Senang's point-ledger endpoint is customer-scoped and does not accept a branch
        // parameter. Keep branchId only as UI/session context; do not block history loading
        // when the branch context is unavailable.
        var start = fromDate.Date;
        var end = toDate.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var balanceTask = customerService.GetBalanceSummaryAsync(
            customerId,
            cancellationToken);

        var historyTask = webDashboardAC.GetMemberPointBalanceDetailAsync(
            new MemberOtherBalanceDetailRequest
            {
                Id = customerId,
                StartDate = start,
                BalanceType = "Point"
            },
            cancellationToken);

        await Task.WhenAll(balanceTask, historyTask);

        var balanceResult = await balanceTask;
        if (!balanceResult.Success || balanceResult.Value is null)
        {
            return CustomerOperationResult<PointHistorySnapshotDTO>.Fail(
                balanceResult.ErrorMessage ??
                "Unable to load the customer's current point balance.");
        }

        var historyResult = await historyTask;
        if (!historyResult.Success || historyResult.Value is null)
        {
            return CustomerOperationResult<PointHistorySnapshotDTO>.Fail(
                historyResult.ErrorMessage ??
                "Unable to load backend point transactions.");
        }

        var customerDetails = historyResult.Value
            .Where(pair =>
                string.Equals(pair.Key?.Trim(), customerId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Value.CustomerID?.Trim(), customerId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .ToList();

        // The request is already scoped to one customer. Some server versions return
        // a dictionary whose key is not the customer ID, so a single returned member
        // is still safe to consume.
        if (customerDetails.Count == 0 && historyResult.Value.Count == 1)
        {
            customerDetails.Add(historyResult.Value.Values.First());
        }

        var entries = customerDetails
            .SelectMany(detail => detail.Transactions ?? new List<MemberBalanceTransactionDTO>())
            .Where(transaction =>
                transaction.FinancialDate != default &&
                transaction.FinancialDate.Date >= start &&
                transaction.FinancialDate.Date <= end)
            .Select(transaction => new PointHistoryEntryDTO
            {
                DocumentId = transaction.DocumentID?.Trim() ?? string.Empty,
                InvoiceNumber = transaction.BillNo?.Trim() ?? string.Empty,
                FinancialDate = transaction.FinancialDate,
                MovementPoints = transaction.Amount,
                BalanceAfter = transaction.Balance
            })
            .GroupBy(entry => new
            {
                entry.DocumentId,
                entry.InvoiceNumber,
                entry.FinancialDate,
                entry.MovementPoints,
                entry.BalanceAfter
            })
            .Select(group => group.First())
            .OrderByDescending(entry => entry.FinancialDate)
            .ThenByDescending(entry => entry.InvoiceNumber)
            .ToList();

        Console.WriteLine(
            $"[Point Step 14] HISTORY PASS | Customer={customerId} | " +
            $"ContextBranch={(string.IsNullOrWhiteSpace(branchId) ? "NotRequired" : branchId)} | " +
            $"From={start:yyyy-MM-dd} | To={end:yyyy-MM-dd} | Entries={entries.Count} | " +
            $"CurrentBalance={balanceResult.Value.PointBalance:0.##} | " +
            $"Endpoint=/api/WebDashboard/GetMemberOtherBalanceDetail | BalanceType=Point | " +
            $"Source=BackendPointLedger");

        return CustomerOperationResult<PointHistorySnapshotDTO>.Ok(
            new PointHistorySnapshotDTO
            {
                CustomerId = customerId,
                BranchId = branchId,
                FromDate = start,
                ToDate = end,
                CurrentPointBalance = Math.Max(0m, balanceResult.Value.PointBalance),
                Entries = entries
            });
    }
}
