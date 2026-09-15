using System.Threading;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.TaxRateService
{
    public interface IGstTaxRateService
    {
        Task<decimal> GetRateForTaxCodeAsync(string? taxCodeId, CancellationToken cancellationToken = default);
    }
}
