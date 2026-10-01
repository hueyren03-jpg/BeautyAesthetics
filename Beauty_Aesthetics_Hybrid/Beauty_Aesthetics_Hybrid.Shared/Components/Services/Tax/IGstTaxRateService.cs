using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Tax;

public interface IGstTaxRateService
{
    Task<ApiCallResult<IReadOnlyList<GstTaxCodeDTO>>> LoadActiveTaxCodesAsync(
        CancellationToken cancellationToken = default);

    Task<decimal> GetRateForTaxCodeAsync(
        string? taxCodeId,
        CancellationToken cancellationToken = default);
}
