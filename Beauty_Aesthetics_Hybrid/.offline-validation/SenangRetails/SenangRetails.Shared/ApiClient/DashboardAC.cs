using EBI.DM;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient
{
    public class DashboardAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public DashboardAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        /// <summary>Returns all customers (active + inactive) for dashboard use.</summary>
        public async Task<List<CustomerDM>> GetAllCustomersAsync()
        {
            if (!await SetBearerToken()) return new List<CustomerDM>();

            var apiResponse = await PostAsync<object, ApiResponseRoot<List<CustomerDM>>>(
                "api/Customer/SearchByWord",
                new { Id = "" });

            return apiResponse?.result ?? new List<CustomerDM>();
        }

        /// <summary>
        /// Loads today's cash sales for a branch and returns the total amount (sum of TotalAfterTax).
        /// Uses api/Doc_CashSales/GetAppSalesList with today's date range and branch ID.
        /// </summary>
        public async Task<decimal> GetSalesTodayAsync(string branchId)
        {
            if (!await SetBearerToken()) return 0m;

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var request = new CashSalesRequest
            {
                Id = branchId,
                StartDate = today,
                EndDate = tomorrow
            };

            var apiResponse = await PostAsync<CashSalesRequest, ApiResponse<List<Doc_CashSalesDM>>>(
                "api/Doc_CashSales/GetAppSalesList", request);

            if (apiResponse?.StatusCode != 200 || apiResponse.Result == null) return 0m;
            return apiResponse.Result.Sum(s => s.TotalAfterTax);
        }

        /// <summary>
        /// Loads today's cash sales for a branch and returns TotalSales, BillsCount, and AvgSpending.
        /// </summary>
        public async Task<(decimal TotalSales, int BillsCount, decimal AvgSpending)> GetSalesSummaryTodayAsync(string branchId)
        {
            if (!await SetBearerToken()) return (0m, 0, 0m);

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var request = new CashSalesRequest
            {
                Id = branchId,
                StartDate = today,
                EndDate = tomorrow
            };

            var apiResponse = await PostAsync<CashSalesRequest, ApiResponse<List<Doc_CashSalesDM>>>(
                "api/Doc_CashSales/GetAppSalesList", request);

            if (apiResponse?.StatusCode != 200 || apiResponse.Result == null || !apiResponse.Result.Any())
                return (0m, 0, 0m);

            var totalSales = apiResponse.Result.Sum(s => s.TotalAfterTax);
            var billsCount = apiResponse.Result.Count;
            var avgSpending = billsCount > 0 ? totalSales / billsCount : 0m;

            return (totalSales, billsCount, avgSpending);
        }
    }

    public class CashSalesSummaryDM
    {
        public decimal totalAfterTax { get; set; }
        public string? documentID { get; set; }
        public string? displayCode { get; set; }
        public string? accountName { get; set; }
        public DateTime financialDate { get; set; }
    }
}
