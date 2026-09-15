using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Entities;
using System.Diagnostics;
using EBI.DM;

namespace SenangRetails.Shared.Services.DashboardService
{
    public class DashboardService : IDashboardService
    {
        private readonly DashboardAC _dashboardAC;
        public DashboardService(DashboardAC dashboardAC)
        {
            _dashboardAC = dashboardAC;
        }

        /// <summary>
        /// Returns active member count and the 5 most recently created members in one API call.
        /// </summary>
        public async Task<(int ActiveCount, List<CustomerDM> Recent)> GetMemberDashboardDataAsync()
        {
            var all = await _dashboardAC.GetAllCustomersAsync();

            var recent = all
                .OrderByDescending(c => c.CreatedDateTime)
                .Take(2)
                .ToList();

            Debug.WriteLine($"[Dashboard] Total members: {all.Count}, Recent: {recent.Count}");
            return (all.Count, recent);
        }

        public async Task<decimal> GetSalesTodayAsync(string branchId)
        {
            var total = await _dashboardAC.GetSalesTodayAsync(branchId);
            Debug.WriteLine($"[Dashboard] Sales today: {total}");
            return total;
        }

        public async Task<(decimal TotalSales, int BillsCount, decimal AvgSpending)> GetSalesSummaryTodayAsync(string branchId)
        {
            return await _dashboardAC.GetSalesSummaryTodayAsync(branchId);
        }

    }
}
