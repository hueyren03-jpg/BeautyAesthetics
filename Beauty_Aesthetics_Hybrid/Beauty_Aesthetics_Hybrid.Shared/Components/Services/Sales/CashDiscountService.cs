using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public sealed class CashDiscountService : ICashDiscountService
{
    private readonly CashDiscountAC cashDiscountAC;

    public CashDiscountService(CashDiscountAC cashDiscountAC) => this.cashDiscountAC = cashDiscountAC;

    public async Task<ApiCallResult<IReadOnlyList<CashDiscountRuleDTO>>> LoadAvailableAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await cashDiscountAC.LoadProxyAsync(cancellationToken);
        if (!result.Success || result.Value is null)
            return ApiCallResult<IReadOnlyList<CashDiscountRuleDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load discount rules.");

        var now = DateTime.Now;
        var rules = result.Value
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id))
            .Where(rule => rule.IsAvailable(branchId, now))
            .OrderBy(rule => rule.Description)
            .ToList();

        return ApiCallResult<IReadOnlyList<CashDiscountRuleDTO>>.Ok(result.StatusCode, rules);
    }
}
