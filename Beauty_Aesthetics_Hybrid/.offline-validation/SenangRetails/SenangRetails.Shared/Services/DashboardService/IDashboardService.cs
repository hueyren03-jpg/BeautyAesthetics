using SenangRetails.Shared.Entities;
using EBI.DM;

namespace SenangRetails.Shared.Services.DashboardService
{
    public interface IDashboardService
    {
        Task<(int ActiveCount, List<CustomerDM> Recent)> GetMemberDashboardDataAsync();
        Task<decimal> GetSalesTodayAsync(string branchId);
        Task<(decimal TotalSales, int BillsCount, decimal AvgSpending)> GetSalesSummaryTodayAsync(string branchId);
    }
}
