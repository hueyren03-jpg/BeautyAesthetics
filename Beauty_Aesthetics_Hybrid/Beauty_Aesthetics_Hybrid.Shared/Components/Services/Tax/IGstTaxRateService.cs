namespace Beauty_Aesthetics_WebPos.Components.Services.Tax;

public interface IGstTaxRateService
{
    Task<decimal> GetRateForTaxCodeAsync(
        string? taxCodeId,
        CancellationToken cancellationToken = default);
}
