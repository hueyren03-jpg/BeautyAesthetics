using SenangRetails.Shared.Models;
using SenangRetails.Shared.Pages;
using static SenangRetails.Shared.Pages.CompanySettings;
using static SenangRetails.Shared.Pages.TransactionReport;

namespace SenangRetails.Shared.Services
{
    public interface ITaxService
    {
        Task<ApiResponse<List<TaxCodeItem>>?> LoadProxyByParentIDAsync(string id);
    }
}