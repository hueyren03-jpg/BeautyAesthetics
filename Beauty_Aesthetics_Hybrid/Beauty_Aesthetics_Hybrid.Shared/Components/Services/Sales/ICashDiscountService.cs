using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public interface ICashDiscountService
{
    Task<ApiCallResult<IReadOnlyList<CashDiscountRuleDTO>>> LoadAvailableAsync(
        string branchId,
        CancellationToken cancellationToken = default);
}
