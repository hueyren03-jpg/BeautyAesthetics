using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using static SenangRetails.Shared.Pages.DashboardReport;

namespace SenangRetails.Shared.Pages
{
    public partial class DashboardReport
    {
        private BarChart? barChart = default!;
        private ChartData? chartData;
        private BarChartOptions? barChartOptions;
        private string? errorMessage;
        private bool isLoadingChart = true;
        private double totalSales = 0;
        private string debugInfo = "";
        private bool isChartInitialized = false;
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private List<PaymentSummary> paymentSummary = new();
        private bool isLoadingPayment = true;
        private string? paymentError;
        private List<Visitor> visitorsList = new();
        private bool isLoadingVisitors = false;
        private string visitorsError = string.Empty;
        private int totalVisitors = 0;
        private SalesByType? salesByTypeData;
        private bool isLoadingSalesByType = false;
        private string salesByTypeError = string.Empty;
        private List<SalesByItemType> serviceItems = new();
        private List<SalesByItemType> productItems = new();
        private List<SalesByItemType> packageItems = new();
        private List<SalesByItemType> topUpItems = new();
        private bool isLoadingTopSelling = false;
        private string topSellingError = string.Empty;

        // New KPI values
        private string topSellingItemName = "N/A";
        private decimal topSellingItemSales = 0;
        private string topSellingCategory = "N/A";

        [Parameter] public string SelectedDateRange { get; set; } = "Today";

        private readonly string[] TimeSlots = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToArray();
        private string selectedFormTab = "Service";

        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService DashboardService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            InitializeChartOptions();
            SetDateRange(selectedDateRange);
            await LoadAllDataAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!isChartInitialized && barChart != null && chartData != null && barChartOptions != null && !isLoadingChart)
            {
                try
                {
                    debugInfo += "\nAttempting to initialize chart...";
                    await barChart.InitializeAsync(chartData, barChartOptions);
                    isChartInitialized = true;
                    debugInfo += "\nChart initialized successfully";
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    debugInfo += $"\nChart init error: {ex.Message}";
                    Console.WriteLine(debugInfo);
                }
            }
        }

        private void InitializeChartOptions()
        {
            barChartOptions = new BarChartOptions
            {
                Responsive = true,
                MaintainAspectRatio = true
            };

            barChartOptions.IndexAxis = "y";

            barChartOptions.Interaction = new Interaction { Mode = InteractionMode.Y };

            barChartOptions.Scales = new Scales();

            barChartOptions.Scales.X = new ChartAxes
            {
                Title = new ChartAxesTitle { Text = "Sales Amount (RM)", Display = true },
                BeginAtZero = true
            };

            barChartOptions.Scales.Y = new ChartAxes
            {
                Title = new ChartAxesTitle { Text = "Hour of Day", Display = true }
            };
        }

        private DateTime GetStartDateTime(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
        }

        private DateTime GetEndDateTime(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 23, 59, 00);
        }

        private async Task LoadAllDataAsync()
        {
            await Task.WhenAll(
                LoadHourlySalesDataAsync(),
                LoadPaymentDataAsync(),
                LoadVisitorsDataAsync(),
                LoadSalesByTypeDataAsync(),
                LoadTopSellingDataAsync()
            );

            // Calculate additional KPIs after all data is loaded
            CalculateAdditionalKPIs();
        }

        private void CalculateAdditionalKPIs()
        {
            // Find Top Selling Item across all categories
            var allItems = new List<SalesByItemType>();
            allItems.AddRange(serviceItems);
            allItems.AddRange(productItems);
            allItems.AddRange(packageItems);
            allItems.AddRange(topUpItems);

            var topItem = allItems.OrderByDescending(x => x.Sales).FirstOrDefault();
            if (topItem != null && topItem.Sales > 0)
            {
                topSellingItemName = topItem.AccountName ?? "N/A";
                topSellingItemSales = topItem.Sales;
            }
            else
            {
                topSellingItemName = "N/A";
                topSellingItemSales = 0;
            }

            // Determine Top Selling Category from salesByTypeData
            if (salesByTypeData != null)
            {
                var categorySales = new Dictionary<string, decimal>
                {
                    { "Service", salesByTypeData.Service },
                    { "Product", salesByTypeData.Product },
                    { "Package", salesByTypeData.Package },
                    { "Top Up", salesByTypeData.TopUp }
                };

                var topCategory = categorySales.OrderByDescending(x => x.Value).FirstOrDefault();
                if (topCategory.Value > 0)
                {
                    topSellingCategory = topCategory.Key;
                }
                else
                {
                    topSellingCategory = "N/A";
                }
            }
            else
            {
                topSellingCategory = "N/A";
            }
        }

        private async Task LoadTopSellingDataAsync()
        {
            isLoadingTopSelling = true;
            topSellingError = string.Empty;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;

                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);

                var serviceTask = DashboardService.GetSalesByItemTypeAsync(startDateTime, endDateTime, branchId, 3);
                var productTask = DashboardService.GetSalesByItemTypeAsync(startDateTime, endDateTime, branchId, 1);
                var packageTask = DashboardService.GetSalesByItemTypeAsync(startDateTime, endDateTime, branchId, 5);
                var topUpTask = DashboardService.GetSalesByItemTypeAsync(startDateTime, endDateTime, branchId, 7);

                await Task.WhenAll(serviceTask, productTask, packageTask, topUpTask);

                var serviceResponse = await serviceTask;
                if (serviceResponse != null && serviceResponse.StatusCode == 200 && serviceResponse.Result != null)
                {
                    serviceItems = serviceResponse.Result
                        .OrderByDescending(x => x.Sales)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    serviceItems = new List<SalesByItemType>();
                }

                var productResponse = await productTask;
                if (productResponse != null && productResponse.StatusCode == 200 && productResponse.Result != null)
                {
                    productItems = productResponse.Result
                        .OrderByDescending(x => x.Sales)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    productItems = new List<SalesByItemType>();
                }

                var packageResponse = await packageTask;
                if (packageResponse != null && packageResponse.StatusCode == 200 && packageResponse.Result != null)
                {
                    packageItems = packageResponse.Result
                        .OrderByDescending(x => x.Sales)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    packageItems = new List<SalesByItemType>();
                }

                var topUpResponse = await topUpTask;
                if (topUpResponse != null && topUpResponse.StatusCode == 200 && topUpResponse.Result != null)
                {
                    topUpItems = topUpResponse.Result
                        .OrderByDescending(x => x.Sales)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    topUpItems = new List<SalesByItemType>();
                }
            }
            catch (Exception ex)
            {
                topSellingError = $"Error loading top selling data: {ex.Message}";
                serviceItems = new List<SalesByItemType>();
                productItems = new List<SalesByItemType>();
                packageItems = new List<SalesByItemType>();
                topUpItems = new List<SalesByItemType>();
            }
            finally
            {
                isLoadingTopSelling = false;
                StateHasChanged();
            }
        }

        private async Task LoadSalesByTypeDataAsync()
        {
            isLoadingSalesByType = true;
            salesByTypeError = string.Empty;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;

                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);

                var apiResponse = await DashboardService.GetSalesByTypeAsync(startDateTime, endDateTime, branchId);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    salesByTypeData = apiResponse.Result;
                }
                else
                {
                    salesByTypeError = apiResponse?.Message ?? "Failed to load sales by type data";
                    salesByTypeData = null;
                }
            }
            catch (Exception ex)
            {
                salesByTypeError = $"Error loading sales by type: {ex.Message}";
                salesByTypeData = null;
            }
            finally
            {
                isLoadingSalesByType = false;
                StateHasChanged();
            }
        }

        private async Task LoadVisitorsDataAsync()
        {
            isLoadingVisitors = true;
            visitorsError = string.Empty;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;

                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);

                var apiResponse = await DashboardService.GetSalesByVisitorsAsync(startDateTime, endDateTime, branchId);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    visitorsList = apiResponse.Result;
                    totalVisitors = visitorsList.Count;
                }
                else
                {
                    visitorsError = apiResponse?.Message ?? "Failed to load visitors data";
                    visitorsList = new List<Visitor>();
                    totalVisitors = 0;
                }
            }
            catch (Exception ex)
            {
                visitorsError = $"Error loading visitors: {ex.Message}";
                visitorsList = new List<Visitor>();
                totalVisitors = 0;
            }
            finally
            {
                isLoadingVisitors = false;
                StateHasChanged();
            }
        }

        private async Task LoadPaymentDataAsync()
        {
            isLoadingPayment = true;
            paymentError = null;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;

                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);

                var apiResponse = await DashboardService.GetSalesByCollectionAsync(startDateTime, endDateTime, branchId);

                if (apiResponse == null || apiResponse.StatusCode != 200 || apiResponse.Result == null)
                {
                    paymentError = apiResponse?.Message ?? "Failed to load payment data";
                    return;
                }

                paymentSummary = apiResponse.Result
                    .GroupBy(x => x.ReportGroup ?? "Unknown")
                    .Select(g => new PaymentSummary
                    {
                        ReportGroup = g.Key,
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .Where(x => x.TotalAmount != 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                paymentError = $"Error loading payment data: {ex.Message}";
                Console.WriteLine(ex);
            }
            finally
            {
                isLoadingPayment = false;
                StateHasChanged();
            }
        }

        private async Task LoadHourlySalesDataAsync()
        {
            isLoadingChart = true;
            errorMessage = null;
            isChartInitialized = false;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;

                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);

                var apiResponse = await DashboardService.GetHourlySalesAsync(startDateTime, endDateTime, branchId);

                if (apiResponse == null || apiResponse.StatusCode != 200 || apiResponse.Result == null)
                {
                    errorMessage = apiResponse?.Message ?? "Failed to retrieve sales data";
                    return;
                }

                var salesData = new List<double?>();
                for (int hour = 0; hour < 24; hour++)
                {
                    var hourData = apiResponse.Result.Find(x => x.DateHour == hour);
                    salesData.Add(hourData?.Sales ?? 0);
                }

                totalSales = salesData.Sum() ?? 0;

                chartData = new ChartData
                {
                    Labels = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToList(),
                    Datasets = new List<IChartDataset>
                    {
                        new BarChartDataset
                        {
                            Label = "Hourly Sales (RM)",
                            Data = salesData,
                            BackgroundColor = new List<string> { "#F2600C" },
                            BorderColor = new List<string> { "#F2600C" },
                            BorderWidth = new List<double> { 0 }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load hourly sales: {ex.Message}";
                Console.WriteLine(errorMessage);
            }
            finally
            {
                isLoadingChart = false;
                StateHasChanged();
            }
        }

        private void SelectFormTab(string tab)
        {
            selectedFormTab = tab;
            StateHasChanged();
        }

        private string selectedDateRange = "Today";

        private void SetDateRange(string range)
        {
            selectedDateRange = range;
            var today = DateTime.Today;

            switch (range)
            {
                case "Today":
                    startDate = today;
                    endDate = today;
                    break;
                case "Week":
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = today.AddDays(-diff).Date;
                    endDate = startDate.AddDays(6).Date;
                    break;
                case "Month":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
                case "Year":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = new DateTime(today.Year, 12, 31);
                    break;
                case "Custom":
                    startDate = today;
                    endDate = today;
                    break;
            }

            // Validate after setting dates
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
                return;
            }

            StateHasChanged();
            _ = LoadAllDataAsync();
        }

        private void GoBack()
        {
            var returnUrl = Navigation.Uri.Contains("returnTo=admin") ? "/admin" : "/home";
            Navigation.NavigateTo(returnUrl);
        }

        private async Task RefreshData()
        {
            // Validate date range
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
                return;
            }

            await LoadAllDataAsync();
        }

        private bool showAlertDialogModal = false;
        private string alertDialogTitle = string.Empty;
        private string alertDialogMessage = string.Empty;

        private void ShowAlertDialog(string title, string message)
        {
            alertDialogTitle = title;
            alertDialogMessage = message;
            showAlertDialogModal = true;
            StateHasChanged();
        }

        private void CloseAlertDialog()
        {
            showAlertDialogModal = false;
            StateHasChanged();
        }

        private void ShowNotification(string message)
        {
            ShowAlertDialog("Notification", message);
        }

        public class HourlySalesItem
        {
            public string? BranchID { get; set; }
            public int DateHour { get; set; }
            public double Sales { get; set; }
        }

        public class PaymentSummary
        {
            public string ReportGroup { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
        }

        public class PaymentSalesItem
        {
            public string? ReportGroup { get; set; }
            public DateTime FinancialDate { get; set; }
            public int Sorting { get; set; }
            public decimal Amount { get; set; }
        }

        public class VisitorSummary
        {
            public decimal TotalVisitor { get; set; }
        }

        public class Visitor
        {
            public string? AccountName { get; set; }
            public string? MemberType { get; set; }
            public int Nos { get; set; }
            public decimal TotalAfterTax { get; set; }
        }

        public class SalesByType
        {
            public decimal TotalBeforeTax_NonServiceCharge { get; set; }
            public decimal TotalBeforeTax_ServiceCharge { get; set; }
            public decimal TotalBeforeTax { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal TourismTax { get; set; }
            public decimal HeritageTax { get; set; }
            public decimal RoundingAmount { get; set; }
            public decimal TotalAfterTax { get; set; }
            public decimal Product { get; set; }
            public decimal Service { get; set; }
            public decimal Voucher { get; set; }
            public decimal Package { get; set; }
            public decimal TopUp { get; set; }
            public decimal Bundle { get; set; }
            public decimal ServiceCharge { get; set; }
            public decimal Deposit { get; set; }
        }

        public class SalesByItemType
        {
            public string? BranchID { get; set; }
            public string? AccountName { get; set; }
            public decimal Quantity { get; set; }
            public decimal Sales { get; set; }
        }
    }
}