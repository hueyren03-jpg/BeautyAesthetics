using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.CashDrawerService
{
    public interface ICashDrawerService
    {
        Task<List<CashDrawerLogModel>> GetLogsAsync();
        Task<CashDrawerSummaryModel> GetSummaryAsync();
        Task<bool> RecordTransactionAsync(CashDrawerLogModel log);
    }
}
