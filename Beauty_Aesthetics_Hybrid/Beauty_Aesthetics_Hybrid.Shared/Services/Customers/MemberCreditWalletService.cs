using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public sealed record MemberCreditWalletSnapshot(
    decimal TotalAvailable,
    IReadOnlyList<RedeemableCreditDTO> Accounts);

public interface IMemberCreditWalletService
{
    Task<CustomerOperationResult<MemberCreditWalletSnapshot>> LoadWalletAsync(
        string customerId,
        DateTime purchaseCutOffDate,
        string? branchId = null,
        string? groupId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<IReadOnlyList<CreditRedemptionHistoryDTO>>> LoadHistoryAsync(
        string arapOutstandingId,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<MemberCreditWalletSnapshot>> RevalidateAsync(
        string customerId,
        IReadOnlyDictionary<string, decimal> requestedByAccount,
        DateTime purchaseCutOffDate,
        string? branchId = null,
        string? groupId = null,
        CancellationToken cancellationToken = default);
}

public sealed class MemberCreditWalletService : IMemberCreditWalletService
{
    private readonly ICustomerService customerService;

    public MemberCreditWalletService(ICustomerService customerService)
    {
        this.customerService = customerService;
    }

    public async Task<CustomerOperationResult<MemberCreditWalletSnapshot>> LoadWalletAsync(
        string customerId,
        DateTime purchaseCutOffDate,
        string? branchId = null,
        string? groupId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return CustomerOperationResult<MemberCreditWalletSnapshot>.Fail(
                "Customer ID is required.");
        }

        var result = await customerService.GetRedeemableCreditsAsync(
            customerId,
            purchaseCutOffDate,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<MemberCreditWalletSnapshot>.Fail(
                result.ErrorMessage ?? "Unable to load redeemable Member Credit accounts.");
        }

        var accounts = result.Value
            .Where(credit =>
                credit.IsRedeemable &&
                !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID) &&
                credit.NetBalanceAfterUtilised > 0m &&
                (credit.DueDate.Year <= 1900 ||
                 credit.DueDate.Date >= purchaseCutOffDate.Date) &&
                CustomerMatches(credit, customerId) &&
                BranchScopeAllows(credit, branchId, groupId))
            .OrderBy(credit => credit.DueDate)
            .ThenBy(credit => credit.ARAPOutstandingID)
            .ToList();

        var total = Math.Round(
            accounts.Sum(credit => credit.NetBalanceAfterUtilised),
            2,
            MidpointRounding.AwayFromZero);

        return CustomerOperationResult<MemberCreditWalletSnapshot>.Ok(
            new MemberCreditWalletSnapshot(total, accounts));
    }

    public async Task<CustomerOperationResult<IReadOnlyList<CreditRedemptionHistoryDTO>>> LoadHistoryAsync(
        string arapOutstandingId,
        CancellationToken cancellationToken = default)
    {
        var accountId = arapOutstandingId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return CustomerOperationResult<IReadOnlyList<CreditRedemptionHistoryDTO>>.Fail(
                "Member Credit account ID is required.");
        }

        var result = await customerService.GetCreditRedemptionHistoryAsync(
            accountId,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<IReadOnlyList<CreditRedemptionHistoryDTO>>.Fail(
                result.ErrorMessage ?? "Unable to load Member Credit redemption history.");
        }

        // The endpoint is scoped by the requested ARAPOutstandingID, but older
        // backend rows can return SourceARAPOutstandingID as blank. Normalize the
        // source account here so every UI receives the same canonical history.
        var normalized = result.Value
            .Where(history =>
                string.IsNullOrWhiteSpace(history.SourceARAPOutstandingID) ||
                string.Equals(
                    history.SourceARAPOutstandingID.Trim(),
                    accountId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    history.ARAPOutstandingID?.Trim(),
                    accountId,
                    StringComparison.OrdinalIgnoreCase))
            .Select(history => new CreditRedemptionHistoryDTO
            {
                ARAPOutstandingID = history.ARAPOutstandingID?.Trim() ?? string.Empty,
                SourceARAPOutstandingID =
                    string.IsNullOrWhiteSpace(history.SourceARAPOutstandingID)
                        ? accountId
                        : history.SourceARAPOutstandingID.Trim(),
                FinancialDate = history.FinancialDate,
                DisplayCode = history.DisplayCode?.Trim() ?? string.Empty,
                TotalAmount = history.TotalAmount,
                BranchID = history.BranchID?.Trim() ?? string.Empty
            })
            .GroupBy(
                history => new
                {
                    history.ARAPOutstandingID,
                    history.SourceARAPOutstandingID,
                    history.FinancialDate,
                    history.DisplayCode,
                    history.TotalAmount,
                    history.BranchID
                })
            .Select(group => group.First())
            .OrderByDescending(history => history.FinancialDate)
            .ThenBy(history => history.DisplayCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(history => history.ARAPOutstandingID, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine(
            $"[Member Credit History] Account={accountId} | Rows={normalized.Count}");

        return CustomerOperationResult<IReadOnlyList<CreditRedemptionHistoryDTO>>.Ok(
            normalized);
    }

    public async Task<CustomerOperationResult<MemberCreditWalletSnapshot>> RevalidateAsync(
        string customerId,
        IReadOnlyDictionary<string, decimal> requestedByAccount,
        DateTime purchaseCutOffDate,
        string? branchId = null,
        string? groupId = null,
        CancellationToken cancellationToken = default)
    {
        var walletResult = await LoadWalletAsync(
            customerId,
            purchaseCutOffDate,
            branchId,
            groupId,
            cancellationToken);

        if (!walletResult.Success || walletResult.Value is null)
        {
            return CustomerOperationResult<MemberCreditWalletSnapshot>.Fail(
                walletResult.ErrorMessage ??
                "Member Credit balances could not be refreshed before saving.");
        }

        var byAccount = walletResult.Value.Accounts
            .Where(credit => !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID))
            .GroupBy(
                credit => credit.ARAPOutstandingID!.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var requested in requestedByAccount)
        {
            var accountId = requested.Key?.Trim() ?? string.Empty;
            var amount = Math.Round(
                requested.Value,
                2,
                MidpointRounding.AwayFromZero);

            if (string.IsNullOrWhiteSpace(accountId) || amount <= 0m)
            {
                return CustomerOperationResult<MemberCreditWalletSnapshot>.Fail(
                    "A Member Credit allocation contains an invalid account or amount.");
            }

            if (!byAccount.TryGetValue(accountId, out var latest))
            {
                return CustomerOperationResult<MemberCreditWalletSnapshot>.Fail(
                    $"Member Credit {accountId} is no longer available.");
            }

            var available = Math.Round(
                Math.Max(0m, latest.NetBalanceAfterUtilised),
                2,
                MidpointRounding.AwayFromZero);

            if (amount - available > 0.009m)
            {
                return CustomerOperationResult<MemberCreditWalletSnapshot>.Fail(
                    $"Member Credit {accountId} changed before saving. " +
                    $"Latest balance is RM {available:N2}, but RM {amount:N2} is allocated.");
            }
        }

        return walletResult;
    }

    private static bool CustomerMatches(
        RedeemableCreditDTO credit,
        string customerId) =>
        string.IsNullOrWhiteSpace(credit.AccountID) ||
        string.Equals(
            credit.AccountID,
            customerId,
            StringComparison.OrdinalIgnoreCase);

    private static bool BranchScopeAllows(
        RedeemableCreditDTO credit,
        string? branchId,
        string? groupId)
    {
        if (!ScopeAllows(credit.RedeemableAtBranch, branchId) ||
            !ScopeAllows(credit.RedeemableAtGroup, groupId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(branchId) ||
            string.IsNullOrWhiteSpace(credit.BranchID) ||
            string.Equals(
                credit.BranchID,
                branchId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return credit.InterOutletRatio > 0m;
    }

    private static bool ScopeAllows(string? scope, string? currentValue)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        return scope
            .Split([',', ';', '|'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Any(value =>
                value == "*" ||
                value.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                value.Equals(currentValue, StringComparison.OrdinalIgnoreCase));
    }
}
