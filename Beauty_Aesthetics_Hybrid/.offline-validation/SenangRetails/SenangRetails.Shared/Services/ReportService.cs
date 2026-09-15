using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Transactions;
using EBI.DM;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Pages;
using SenangRetails.Shared.Services.AuthService;
using static SenangRetails.Shared.Components.Reports.BalanceSummaryReport;
using static SenangRetails.Shared.Components.Reports.BalanceTransactionReport;
using static SenangRetails.Shared.Components.Reports.MemberCreditBalanceReport;
using static SenangRetails.Shared.Components.Reports.PackageBalanceReport;
using static SenangRetails.Shared.Components.Reports.PackageTransactionReport;
using static SenangRetails.Shared.Components.Reports.StaffSalesReport;
using static SenangRetails.Shared.Components.Reports.StockBalanceWithCostReport;
using static SenangRetails.Shared.Components.Reports.StockMovementDetailReport;
using static SenangRetails.Shared.Components.Reports.StockMovementSummaryReport;
using static SenangRetails.Shared.Pages.CollectionReport;
using static SenangRetails.Shared.Pages.DashboardReport;
using static SenangRetails.Shared.Pages.EInvoices;
using static SenangRetails.Shared.Pages.Home;
using static SenangRetails.Shared.Pages.LastVisitReport;
using static SenangRetails.Shared.Pages.MemberListReport;
using static SenangRetails.Shared.Pages.Members;
using static SenangRetails.Shared.Pages.SalesByItemReport;
using static SenangRetails.Shared.Pages.SalesByTypeReport;
using static SenangRetails.Shared.Pages.TaxSummaryReport;
using static SenangRetails.Shared.Pages.TopSellingReport;
using static SenangRetails.Shared.Pages.TopSpendingReport;
using static SenangRetails.Shared.Pages.TransactionReport;

namespace SenangRetails.Shared.Services.DashboardService
{
    public class ReportService : IReportService
    {
        private readonly HttpClient _httpClient;
        private readonly IStoreTokenService _tokenService;

        public ReportService(HttpClient httpClient, IStoreTokenService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        public async Task<ApiResponse<List<HourlySalesItem>>?> GetHourlySalesAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID
                };

                Debug.WriteLine($"GetHourlySales Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "/api/WebDashboard/GetSalesSnapshotHourlySales",
                    request);

                Debug.WriteLine($"GetHourlySales Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<HourlySalesItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetHourlySalesAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<SalesCollectionSummaryItem>>?> GetSalesCollectionSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesCollectionSummary Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesCollectionSummary",
                    request);

                Debug.WriteLine($"GetSalesCollectionSummary Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesCollectionSummaryItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesCollectionSummaryAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<PaymentSalesItem>>?> GetSalesByCollectionAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesByCollection Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesByCollection",
                    request);

                Debug.WriteLine($"GetSalesByCollection Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<PaymentSalesItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesByCollectionAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<Visitor>>?> GetSalesByVisitorsAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesByVisitors Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesByVisitors",
                    request);

                Debug.WriteLine($"GetSalesByVisitors Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<Visitor>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesByVisitorsAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<SalesByType>?> GetSalesByTypeAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesByType Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesByType",
                    request);

                Debug.WriteLine($"GetSalesByType Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Change this line to deserialize as single object, not list
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<SalesByType>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesByTypeAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<SalesByItemType>>?> GetSalesByItemTypeAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID,
            int inventoryTypeID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = inventoryTypeID,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesByItemType Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesByItemType",
                    request);

                Debug.WriteLine($"GetSalesByItemType Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesByItemType>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesByItemTypeAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<SalesItemTypeByItemDetail>>?> GetSalesItemTypeByItemDetailByDateAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID,
            int inventoryTypeID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = inventoryTypeID,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesItemTypeByItemDetailByDate Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesItemTypeByItemDetailByDate",
                    request);

                Debug.WriteLine($"GetSalesItemTypeByItemDetailByDate Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesItemTypeByItemDetail>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesItemTypeByItemDetailByDateAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<SalesBySKUItem>>?> GetSalesByItemAsync(
           DateTime startDate,
           DateTime endDate,
           string branchID,
           int inventoryTypeID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = inventoryTypeID,
                    branchID = branchID
                };

                Debug.WriteLine($"GetSalesByItem Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetSalesByItem",
                    request);

                Debug.WriteLine($"GetSalesByItem Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesBySKUItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSalesByItemAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<TaxPayableSummaryItem>>?> GetGSTPayableSummaryByDateAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID,
            string taxGroupID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID,
                    taxGroupID = taxGroupID
                };

                Debug.WriteLine($"GetGSTPayableSummaryByDate Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetGSTPayableSummaryByDate",
                    request);

                Debug.WriteLine($"GetGSTPayableSummaryByDate Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<TaxPayableSummaryItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetGSTPayableSummaryByDateAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<EmployeeSalesByItemType>>?> GetEmployeeSalesByItemTypeAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = 0,
                    branchID = branchID
                };

                Debug.WriteLine($"GetEmployeeSalesByItemType Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetEmployeeSalesByItemType",
                    request);

                Debug.WriteLine($"GetEmployeeSalesByItemType Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<EmployeeSalesByItemType>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetEmployeeSalesByItemTypeAsync: {ex.Message}");
                return null;
            }
        }




        public async Task<ApiResponse<List<TopSalesItem>>?> GetTopSalesItemAsync(
            DateTime startDate,
            DateTime endDate,
            int inventoryTypeID,
            int recordCount,
            string rankBy,
            string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = inventoryTypeID,
                    recordCount = recordCount,
                    rankBy = rankBy,
                    branches = branches
                };

                Debug.WriteLine($"GetTopSalesItem Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetTopSalesItem",
                    request);

                Debug.WriteLine($"GetTopSalesItem Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<TopSalesItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetTopSalesItemAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<TopSalesCustomer>>?> GetTopSalesCustomerAsync(
            DateTime startDate,
            DateTime endDate,
            int inventoryTypeID,
            int recordCount,
            string rankBy,
            string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryTypeID = inventoryTypeID,
                    recordCount = recordCount,
                    rankBy = rankBy,
                    branches = branches
                };

                Debug.WriteLine($"GetTopSalesCustomer Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetTopSalesCustomer",
                    request);

                Debug.WriteLine($"GetTopSalesItem Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<TopSalesCustomer>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetTopSalesCustomerAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<CustomerLastVisit>>?> GetCustomerLastVisitAsync(
            int daysRangeFrom,
            int daysRangeTo,
            int pageNumber,
            int pageSize)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    daysRangeFrom = daysRangeFrom,
                    daysRangeTo = daysRangeTo,
                    pageNumber = pageNumber,
                    pageSize = pageSize
                };

                Debug.WriteLine($"GetCustomerLastVisit Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetCustomerLastVisit",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<CustomerLastVisit>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetCustomerLastVisitAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<MemberStatisticSummary>?> GetMemberStatisticSummaryAsync(
            string id)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    id = id
                };

                Debug.WriteLine($"GetMemberStatisticSummary Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetMemberStatisticSummary",
                    request);

                Debug.WriteLine($"GetMemberStatisticSummary Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Change this line to deserialize as single object, not list
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<MemberStatisticSummary>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberStatisticSummaryAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<MemberStatisticDetail>>?> GetMemberStatisticDetailAsync(
           string branchID,
           DateTime startDate,
           DateTime endDate,
           int pageNumber,
           int pageSize)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    branchID = branchID,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    pageNumber = pageNumber,
                    pageSize = pageSize
                };

                Debug.WriteLine($"GetMemberStatisticDetail Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetMemberStatisticDetail",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<MemberStatisticDetail>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberStatisticDetailAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<TransactionDetails>>?> GetTransactionAsync(
           string strID,
           DateTime startDate,
           DateTime endDate)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    strID = strID,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
                };

                Debug.WriteLine($"GetTransaction Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/Doc_CashSales/LoadProxy",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<TransactionDetails>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetTransactionAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<PackageBalance>>?> GetBalanceByCustomerID_NonExpiredAsync(
           string customerID,
           DateTime cutOffDate,
           string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches
                };

                Debug.WriteLine($"GetBalanceByCustomerID_NonExpired Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/CashSales_Series_UnconsumedItem/GetBalanceByCustomerID_NonExpired",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<PackageBalance>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetBalanceByCustomerID_NonExpiredAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<PackageBalance>>?> GetBalanceByCustomerID_ExpiredAsync(
           string customerID,
           DateTime cutOffDate,
           string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches
                };

                Debug.WriteLine($"GetBalanceByCustomerID_Expired Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/CashSales_Series_UnconsumedItem/GetBalanceByCustomerID_Expired",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<PackageBalance>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetBalanceByCustomerID_ExpiredAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<PackageBalance>>?> GetBalanceByCustomerID_FullyRedeemedAsync(
           string customerID,
           DateTime cutOffDate,
           string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches
                };

                Debug.WriteLine($"GetBalanceByCustomerID_FullyRedeemed Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/CashSales_Series_UnconsumedItem/GetBalanceByCustomerID_FullyRedeemed",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<PackageBalance>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetBalanceByCustomerID_FullyRedeemedAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<PackageMovement>>?> GetPackageMovementAsync(
           string customerID,
           DateTime startDate,
           DateTime endDate,
           string branches,
           string movementType)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches,
                    movementType = movementType
                };

                Debug.WriteLine($"GetPackageMovement Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/CashSales_Series_UnconsumedItem/GetPackageMovement",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<PackageMovement>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetPackageMovementAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<EInvoiceDetail>>?> GetAppSalesListAsync(
           string strID,
           DateTime startDate,
           DateTime endDate)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    strID = strID,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
                };

                Debug.WriteLine($"GetAppSalesList Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/Doc_CashSales/GetAppSalesList",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<EInvoiceDetail>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetAppSalesListAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<MemberOtherBalanceSummary>>?> GetMemberOtherBalanceSummaryAsync(
           string id,
           DateTime cutOffDate)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    id = id,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss")
                };

                Debug.WriteLine($"GetMemberOtherBalanceSummary Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetMemberOtherBalanceSummary",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<MemberOtherBalanceSummary>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberOtherBalanceSummaryAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<Dictionary<string, MemberBalanceDetail>>?> GetMemberOtherBalanceDetailAsync(
            string id,
            DateTime startDate,
            string balanceType)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    id = id,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    balanceType = balanceType
                };

                Debug.WriteLine($"GetMemberOtherBalanceDetail Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetMemberOtherBalanceDetail",
                    request);

                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Response Status: {response.StatusCode}");
                Debug.WriteLine($"Response Content: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"API Error: {response.StatusCode} - {responseContent}");
                    return null;
                }

                // Deserialize as Dictionary first
                var apiResult = JsonSerializer.Deserialize<ApiResponse<Dictionary<string, MemberBalanceDetail>>>(
                    responseContent,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberOtherBalanceDetailAsync: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<ApiResponse<List<MemberCreditBalance>>?> GetMemberCreditBalanceSummary_WithExpiry_NonExpiredAsync(
           string customerID,
           DateTime cutOffDate,
           string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches
                };

                Debug.WriteLine($"GetMemberCreditBalanceSummary_WithExpiry_NonExpired Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/ARAPOutstanding_MemberCredit/GetMemberCreditBalanceSummary_WithExpiry_NonExpired",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<MemberCreditBalance>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberCreditBalanceSummary_WithExpiry_NonExpiredAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<MemberCreditBalance>>?> GetMemberCreditBalanceSummary_WithExpiry_ExpiredAsync(
           string customerID,
           DateTime cutOffDate,
           string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches
                };

                Debug.WriteLine($"GetMemberCreditBalanceSummary_WithExpiry_Expired Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/ARAPOutstanding_MemberCredit/GetMemberCreditBalanceSummary_WithExpiry_Expired",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<MemberCreditBalance>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberCreditBalanceSummary_WithExpiry_ExpiredAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<MemberCreditBalance>>?> GetMemberCreditBalanceSummary_WithExpiry_FullyRedeemedAsync(
           string customerID,
           DateTime cutOffDate,
           string branches)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    customerID = customerID,
                    cutOffDate = cutOffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branches = branches
                };

                Debug.WriteLine($"GetMemberCreditBalanceSummary_WithExpiry_FullyRedeemed Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/ARAPOutstanding_MemberCredit/GetMemberCreditBalanceSummary_WithExpiry_FullyRedeemed",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<MemberCreditBalance>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetMemberCreditBalanceSummary_WithExpiry_FullyRedeemedAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<LowStockItem>>?> GetStockBelowReorderPointAsync(
           string id)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    id = id
                };

                Debug.WriteLine($"GetStockBelowReorderPoint Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetStockBelowReorderPoint",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<LowStockItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetStockBelowReorderPointAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<StockBalanceWithCost>>?> GetStockBalanceWithCostAsync(
           string branchIDs,
           DateTime endDate,
           string inventoryIDs)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    branchIDs = branchIDs,
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    inventoryIDs = inventoryIDs
                };

                Debug.WriteLine($"GetStockBalanceWithCost Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetStockBalanceWithCost",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<StockBalanceWithCost>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetStockBalanceWithCostAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<StockMovementSummary>>?> GetStockMovementSummaryAsync(
           string branchIDs,
           DateTime startDate,
           DateTime endDate,
           bool combineBranchValues)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    branchIDs = branchIDs,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    combineBranchValues = combineBranchValues
                };

                Debug.WriteLine($"GetStockMovementSummary Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetStockMovementSummary",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<StockMovementSummary>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetStockMovementSummaryAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<List<StockMovementDetail>>?> GetStockMovementDetailAsync(
           string branchID,
           string inventoryID,
           DateTime startDate,
           DateTime endDate)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    branchID = branchID,
                    inventoryID = inventoryID,
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss")
                };

                Debug.WriteLine($"GetStockMovementDetail Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/WebDashboard/GetStockMovementDetail",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<StockMovementDetail>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetStockMovementDetailAsync: {ex.Message}");
                return null;
            }
        }
    }
}