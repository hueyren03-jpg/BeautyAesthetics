using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.PointConversions;
using Beauty_Aesthetics_WebPos.Components.Services.Sales;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public sealed class PointHistoryService : IPointHistoryService
{
    private readonly ICashSalesService cashSalesService;
    private readonly ICustomerService customerService;
    private readonly IPointConversionService pointConversionService;

    public PointHistoryService(
        ICashSalesService cashSalesService,
        ICustomerService customerService,
        IPointConversionService pointConversionService)
    {
        this.cashSalesService = cashSalesService;
        this.customerService = customerService;
        this.pointConversionService = pointConversionService;
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

        if (string.IsNullOrWhiteSpace(branchId))
        {
            return CustomerOperationResult<PointHistorySnapshotDTO>.Fail(
                "Working branch is required before point history can be loaded.");
        }

        var start = fromDate.Date;
        var end = toDate.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var balanceTask = customerService.GetBalanceSummaryAsync(
            customerId,
            cancellationToken);
        var customerTask = customerService.LoadCustomerAsync(
            customerId,
            cancellationToken);
        var rulesTask = pointConversionService.GetAllAsync(
            cancellationToken);
        var salesTask = cashSalesService.LoadTransactionsAsync(
            start,
            end,
            branchId,
            resolvePaymentMethods: false,
            cancellationToken: cancellationToken);

        await Task.WhenAll(balanceTask, customerTask, rulesTask, salesTask);

        var balanceResult = await balanceTask;
        if (!balanceResult.Success || balanceResult.Value is null)
        {
            return CustomerOperationResult<PointHistorySnapshotDTO>.Fail(
                balanceResult.ErrorMessage ??
                "Unable to load the customer's current point balance.");
        }

        var salesResult = await salesTask;
        if (!salesResult.Success || salesResult.Value is null)
        {
            return CustomerOperationResult<PointHistorySnapshotDTO>.Fail(
                salesResult.ErrorMessage ??
                "Unable to load Cash Sales for point history.");
        }

        var customerResult = await customerTask;
        var membershipTypeId =
            customerResult.Success && customerResult.Value is not null
                ? customerResult.Value.MembershipTypeId?.Trim() ?? string.Empty
                : string.Empty;

        var rulesResult = await rulesTask;
        var rules = rulesResult.Success && rulesResult.Value is not null
            ? rulesResult.Value.ToList()
            : new List<PointConversionDM>();
        var ruleLookupSucceeded = rulesResult.Success;

        var candidates = salesResult.Value
            .Where(sale =>
                string.Equals(
                    sale.AccountId,
                    customerId,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(sale.DocumentId))
            .OrderByDescending(sale => sale.Date)
            .ToList();

        var entries = new List<PointHistoryEntryDTO>();
        using var throttler = new SemaphoreSlim(4);

        var loaded = await Task.WhenAll(candidates.Select(async sale =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                return await LoadEntryAsync(
                    sale,
                    membershipTypeId,
                    rules,
                    ruleLookupSucceeded,
                    cancellationToken);
            }
            finally
            {
                throttler.Release();
            }
        }));

        entries.AddRange(
            loaded
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .Where(entry =>
                    entry.RedeemedPoints > 0m ||
                    (entry.EarnedPoints.HasValue && entry.EarnedPoints.Value > 0m))
                .OrderByDescending(entry => entry.FinancialDate)
                .ThenByDescending(entry => entry.InvoiceNumber));

        Console.WriteLine(
            $"[Point Step 14] HISTORY PASS | Customer={customerId} | Branch={branchId} | " +
            $"From={start:yyyy-MM-dd} | To={end:yyyy-MM-dd} | Entries={entries.Count} | " +
            $"CurrentBalance={balanceResult.Value.PointBalance:0.##} | " +
            $"Source=BackendCashSales | DedicatedPointHistoryApi=False");

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

    private async Task<PointHistoryEntryDTO?> LoadEntryAsync(
        Transaction sale,
        string membershipTypeId,
        IReadOnlyList<PointConversionDM> rules,
        bool ruleLookupSucceeded,
        CancellationToken cancellationToken)
    {
        var detailResult = await cashSalesService.LoadTransactionAsync(
            sale.DocumentId,
            5,
            cancellationToken);

        if (!detailResult.Success || detailResult.Value is null)
        {
            Console.WriteLine(
                $"[Point Step 14] HISTORY WARNING | Document={sale.DocumentId} | " +
                $"Unable to reload Cash Sale: {detailResult.ErrorMessage}");
            return null;
        }

        var detail = detailResult.Value;
        var redeemed = Math.Round(
            detail.Items.Sum(item => Math.Max(0m, item.Points)),
            2,
            MidpointRounding.AwayFromZero);

        decimal? earned = null;
        var earnedCalculated = false;
        var pointRuleId = string.Empty;

        if (ruleLookupSucceeded &&
            !string.IsNullOrWhiteSpace(membershipTypeId))
        {
            var matchingRules = rules
                .Where(rule =>
                    string.Equals(
                        rule.MemberTypeID?.Trim(),
                        membershipTypeId,
                        StringComparison.OrdinalIgnoreCase) &&
                    PointRuleDateMatches(rule, detail.Date) &&
                    PointRuleBranchMatches(rule, detail.BranchId))
                .OrderByDescending(rule => rule.FromDate)
                .ThenByDescending(rule => rule.PointID)
                .ToList();

            if (matchingRules.Count == 1)
            {
                var rule = matchingRules[0];
                pointRuleId = rule.PointID ?? string.Empty;

                if (!rule.ConvertRebateIntoCashVoucher)
                {
                    earned = CalculateEarnedPoints(detail, rule);
                    earnedCalculated = true;
                }
            }
        }

        return new PointHistoryEntryDTO
        {
            DocumentId = detail.DocumentId,
            InvoiceNumber = string.IsNullOrWhiteSpace(detail.InvoiceNumber)
                ? sale.InvoiceNumber
                : detail.InvoiceNumber,
            BranchId = string.IsNullOrWhiteSpace(detail.BranchId)
                ? sale.BranchId
                : detail.BranchId,
            FinancialDate = detail.Date == default ? sale.Date : detail.Date,
            SaleAmount = detail.Amount != 0m ? detail.Amount : sale.Amount,
            RedeemedPoints = redeemed,
            EarnedPoints = earned,
            EarnedPointsCalculated = earnedCalculated,
            PointRuleId = pointRuleId,
            BalanceAfter = null
        };
    }

    private static decimal CalculateEarnedPoints(
        Transaction transaction,
        PointConversionDM rule)
    {
        if (rule.ForEveryXDollar <= 0m ||
            rule.EqualToXPoint <= 0m)
        {
            return 0m;
        }

        var spendBasis = Math.Max(
            0m,
            rule.ExcludeTaxAmount
                ? transaction.Amount - transaction.Tax
                : transaction.Amount);

        if (spendBasis + 0.009m < Math.Max(0m, rule.MinimumSpend))
        {
            return 0m;
        }

        var calculated =
            (spendBasis / rule.ForEveryXDollar) *
            rule.EqualToXPoint;

        return rule.RoundDownToInteger
            ? decimal.Floor(calculated)
            : Math.Round(
                calculated,
                2,
                MidpointRounding.AwayFromZero);
    }

    private static bool PointRuleDateMatches(
        PointConversionDM rule,
        DateTime saleDate)
    {
        var date = saleDate.Date;
        var fromMatches =
            rule.FromDate.Year <= 1900 ||
            date >= rule.FromDate.Date;
        var toMatches =
            rule.ToDate.Year <= 1900 ||
            date <= rule.ToDate.Date;
        return fromMatches && toMatches;
    }

    private static bool PointRuleBranchMatches(
        PointConversionDM rule,
        string branchId)
    {
        if (string.IsNullOrWhiteSpace(rule.VisibleToBranchID) ||
            string.IsNullOrWhiteSpace(branchId))
        {
            return true;
        }

        var branches = rule.VisibleToBranchID
            .Split(
                new[] { ',', ';', '|' },
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        return branches.Length == 0 ||
               branches.Any(value =>
                   string.Equals(value, "ALL", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, branchId, StringComparison.OrdinalIgnoreCase));
    }
}
