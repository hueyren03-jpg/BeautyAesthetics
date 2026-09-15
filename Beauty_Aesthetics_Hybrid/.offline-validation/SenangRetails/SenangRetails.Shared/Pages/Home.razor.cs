using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using EBI.DM;
using Microsoft.JSInterop;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.BluetoothPrinterService;
using static SenangRetails.Shared.Components.Reports.MemberCreditBalanceReport;
using static SenangRetails.Shared.Components.Reports.PackageBalanceReport;

namespace SenangRetails.Shared.Pages
{
    public partial class Home : BasePage
    {
        private string CompanyName = "";
        private string BranchName = "";
        private bool IsSOSVisible = false;
        private bool IsOperationVisible = false;
        private bool IsNotificationVisible = false;
        private bool IsWorkspaceVisible = false;
        private bool IsLanguagePickerVisible = false;
        private bool IsPrinterModalVisible = false;
        private bool IsPackageModalVisible = false;
        private bool IsCreditModalVisible = false;
        private List<PrinterOption> printerOptions = new();
        private PrinterOption? editingNetworkPrinter = null;
        private string editingIpValue = "";

        private string AppVersion = "v1";

        private bool IsLoadingDashboard = true;
        private int TotalMembersCount = 0;
        private List<CustomerDM> RecentMembers = new();
        private decimal SalesToday = 0m;
        private int BillsCountToday = 0;
        private decimal BillsAvgSpendingToday = 0m;
        private int PendingOrdersCount = 0;
        private Timer? _salesRefreshTimer;

        // Retail Operational Section DTOs
        public class IncomingTransferItem
        {
            public string TransferNo { get; set; } = string.Empty;
            public string FromBranch { get; set; } = string.Empty;
            public int TotalItems { get; set; }
            public DateTime Date { get; set; } = DateTime.Now;
        }

        public class PendingSalesOrderItem
        {
            public string OrderNo { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime Date { get; set; } = DateTime.Now;
        }

        public class PendingDoItem
        {
            public string DoNo { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Status { get; set; } = "Pending Dispatch";
            public DateTime Date { get; set; } = DateTime.Now;
        }

        public class PendingInvoiceItem
        {
            public string InvoiceNo { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public DateTime DueDate { get; set; } = DateTime.Now;
        }

        public class PendingGrnItem
        {
            public string PoNo { get; set; } = string.Empty;
            public string SupplierName { get; set; } = string.Empty;
            public int ExpectedItems { get; set; }
            public DateTime IssuedDate { get; set; } = DateTime.Now;
        }

        private List<IncomingTransferItem> incomingTransfers = new();
        private List<PendingSalesOrderItem> pendingSalesOrders = new();
        private List<PendingDoItem> pendingDeliveryOrders = new();
        private List<PendingInvoiceItem> pendingInvoices = new();
        private List<PendingGrnItem> pendingGrns = new();

        private int UnreadNotificationCount => NotificationSvc.Notifications.Count(n => !n.IsRead);

        private bool isLoading = false;
        private int ExpiringPackageCount => packageData?.Count ?? 0;
        private int ExpiringCreditCount => creditData?.Count ?? 0;

        private List<LowStockItem> lowStockItems = new();
        private bool isLoadingLowStock = false;
        private int lowStockCount => lowStockItems?.Count ?? 0;

        protected override async Task OnInitializedAsync()
        {
            CompanyName = AppState.CurrentBranch?.CompanyName ?? "";
            BranchName = AppState.CurrentBranch?.Branch ?? "";

            NotificationSvc.OnChanged += OnNotificationsChanged;
            AppState.OnSalesTodayChanged += OnSalesTodayChanged;
            LangSvc.OnLanguageChanged += OnLangChanged;

            // Load staff data first if needed for filtering
            await LoadStaffDataAsync();

            await LoadLowStockItemsAsync();
            await LoadPackageAsync();
            await LoadCreditAsync();

            IsLoadingDashboard = true;
            try
            {
                var memberTask = DashboardSvc.GetMemberDashboardDataAsync();
                var salesSummaryTask = DashboardSvc.GetSalesSummaryTodayAsync(string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID);

                await Task.WhenAll(memberTask, salesSummaryTask);

                (TotalMembersCount, RecentMembers) = memberTask.Result;
                (SalesToday, BillsCountToday, BillsAvgSpendingToday) = salesSummaryTask.Result;
                AppState.SalesToday = SalesToday;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Home] Dashboard load error: {ex.Message}");
            }
            finally
            {
                IsLoadingDashboard = false;
            }

            _salesRefreshTimer = new Timer(async _ =>
            {
                try
                {
                    (SalesToday, BillsCountToday, BillsAvgSpendingToday) = await DashboardSvc.GetSalesSummaryTodayAsync(string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID);
                    await InvokeAsync(StateHasChanged);
                }
                catch { }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    var json = await JS.InvokeAsync<string>("localStorage.getItem", "held_bills_cache");
                    if (!string.IsNullOrEmpty(json))
                    {
                        var orders = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                        PendingOrdersCount = orders.ValueKind == System.Text.Json.JsonValueKind.Array ? orders.GetArrayLength() : 0;
                    }
                }
                catch { }

                if (AppState.CurrentBranch == null)
                {
                    try
                    {
                        var savedDetails = await JS.InvokeAsync<string?>("localStorage.getItem", "currentBranchDetails");
                        if (!string.IsNullOrEmpty(savedDetails))
                        {
                            AppState.CurrentBranch = System.Text.Json.JsonSerializer.Deserialize<BranchItem>(savedDetails);
                            AppState.SelectedBranchGroupID = AppState.CurrentBranch?.BranchGroupID ?? "";
                            var savedId = await JS.InvokeAsync<string?>("localStorage.getItem", "currentBranch");
                            if (!string.IsNullOrEmpty(savedId))
                                AppState.SelectedBranchID = savedId;
                            CompanyName = AppState.CurrentBranch?.CompanyName ?? "";
                            BranchName = AppState.CurrentBranch?.Branch ?? "";
                        }
                    }
                    catch { }
                }

                StateHasChanged();
            }
        }

        private string TotalMembersDisplay
        {
            get
            {
                if (TotalMembersCount >= 100) return "100+";
                return TotalMembersCount.ToString();
            }
        }

        private async void OnNotificationsChanged()
        {
            await SaveNotificationsAsync();
            await InvokeAsync(StateHasChanged);
        }

        private void OnSalesTodayChanged()
        {
            SalesToday = AppState.SalesToday;
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            NotificationSvc.OnChanged -= OnNotificationsChanged;
            AppState.OnSalesTodayChanged -= OnSalesTodayChanged;
            LangSvc.OnLanguageChanged -= OnLangChanged;
            _salesRefreshTimer?.Dispose();
        }

        private void OnLangChanged() => InvokeAsync(StateHasChanged);

        private string GetText(string key) => LangSvc.GetText(key);

        private string FormatNotifMessage(AppNotification notif)
        {
            var template = GetText(notif.MessageKey);
            if (!string.IsNullOrEmpty(notif.MessageParam))
            {
                try { return string.Format(template, notif.MessageParam); }
                catch { return template; }
            }
            return template;
        }

        private bool IsModuleInLocation(string moduleName, string location)
            => WorkspaceSvc.IsModuleInLocation(moduleName, location);

        private List<WorkspaceModule> GetHomeModules()
        {
            var homeModules = new List<WorkspaceModule>();
            WorkspaceModule? sosModule = null;
            foreach (var category in WorkspaceSvc.ModuleCategories)
            {
                foreach (var module in category.Modules.Where(m => m.Location == "Home"))
                {
                    if (module.Route == "sos") sosModule = module;
                    else homeModules.Add(module);
                }
            }
            if (sosModule != null) homeModules.Add(sosModule);
            return homeModules;
        }

        private void HandleModuleClick(WorkspaceModule module)
        {
            if (module.Route == "sos") ShowSOS();
            else NavigateTo(module.Route);
        }

        private void SetModuleLocation(WorkspaceModule module, string location)
        {
            WorkspaceSvc.SetModuleLocation(module, location);
            SaveWorkspace();
            StateHasChanged();
        }

        void ToggleWorkspace() => IsWorkspaceVisible = !IsWorkspaceVisible;
        void ShowSOS() => IsSOSVisible = true;
        void HideSOS() => IsSOSVisible = false;
        void ShowOperation() => IsOperationVisible = true;
        void HideOperation() => IsOperationVisible = false;
        void ToggleNotifications() => IsNotificationVisible = !IsNotificationVisible;

        void MarkAsRead(AppNotification notification) => NotificationSvc.MarkRead(notification.Id);
        void ReadAllNotifications() => NotificationSvc.MarkAllRead();
        void ClearAllNotifications() => NotificationSvc.ClearAll();

        void NavigateTo(string page) => NavigationManager.NavigateTo($"/{page}?returnTo=home");

        void OpenLanguagePicker() => IsLanguagePickerVisible = true;
        void CloseLanguagePicker() => IsLanguagePickerVisible = false;
        void SelectLanguage(string lang) { LangSvc.SetLanguage(lang); IsLanguagePickerVisible = false; }
        void OpenPackageModal() { IsPackageModalVisible = true; }
        void ClosePackageModal() => IsPackageModalVisible = false;
        void OpenCreditModal() { IsCreditModalVisible = true; }
        void CloseCreditModal() => IsCreditModalVisible = false;
        void OpenPrinter()
        {
            printerOptions = BluetoothSvc.GetPrinterOptions();
            editingNetworkPrinter = null;
            editingIpValue = "";
            IsPrinterModalVisible = true;
        }

        void ClosePrinterModal() { IsPrinterModalVisible = false; editingNetworkPrinter = null; }

        void SelectPrinterDevice(PrinterOption printer)
        {
            BluetoothSvc.SelectPrinter(printer);
            printerOptions = BluetoothSvc.GetPrinterOptions();
            editingNetworkPrinter = null;
        }

        void StartEditIp(PrinterOption printer) { editingNetworkPrinter = printer; editingIpValue = printer.IpAddress; }

        void SaveIpAddress(PrinterOption printer)
        {
            if (!string.IsNullOrWhiteSpace(editingIpValue))
            {
                BluetoothSvc.UpdateNetworkPrinterIp(printer, editingIpValue);
                printerOptions = BluetoothSvc.GetPrinterOptions();
            }
            editingNetworkPrinter = null;
        }

        void CancelEditIp() => editingNetworkPrinter = null;

        string GetPrinterIcon(PrinterOption printer)
        {
            if (printer.IsIminPrinter) return "🖨️";
            if (printer.IsBluetoothPrinter) return "📡";
            return "🌐";
        }

        void OpenAdminLog() { }

        private string WhatsAppSOSLink
        {
            get
            {
                var rawMessage =
                    "🚨 Hello,\n" +
                    "I am requesting urgent support from SenangRetails.\n\n" +
                    $"Company: {CompanyName}\n" +
                    $"Branch: {BranchName}\n\n" +
                    "Please respond as soon as possible. Thank you.";
                return $"https://wa.me/60146277847?text={Uri.EscapeDataString(rawMessage)}";
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                if (!WorkspaceSvc.IsLoaded) LoadWorkspace();
                LoadNotifications();
            }
        }

        private async void LoadNotifications()
        {
            try
            {
                var json = await JS.InvokeAsync<string?>("localStorage.getItem", "app_notifications");
                if (!string.IsNullOrWhiteSpace(json)) NotificationSvc.LoadFromJson(json);
            }
            catch { }
            StateHasChanged();
        }

        private async Task SaveNotificationsAsync()
        {
            try { await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson()); }
            catch { }
        }

        private async void LoadWorkspace()
        {
            try
            {
                var json = await JS.InvokeAsync<string?>("localStorage.getItem", "workspace_config");
                if (!string.IsNullOrEmpty(json)) WorkspaceSvc.LoadFromJson(json);
                else WorkspaceSvc.MarkLoaded();
            }
            catch { WorkspaceSvc.MarkLoaded(); }
            StateHasChanged();
        }

        private async void SaveWorkspace()
        {
            try { await JS.InvokeVoidAsync("localStorage.setItem", "workspace_config", WorkspaceSvc.ToJson()); }
            catch { }
        }



        private List<StaffResponseDTO> staffList = new();
        private string errorMessage = string.Empty;
        private async Task LoadStaffDataAsync()
        {
            isLoading = true;
            errorMessage = string.Empty;
            try
            {
                var response = await StaffService.GetStaffListAsync();
                if (response != null && response.StatusCode == 200)
                {
                    staffList = response.Result ?? new List<StaffResponseDTO>();
                }
                else
                {
                    errorMessage = response?.Message ?? "Failed to load staff data.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while fetching data.";
                Console.WriteLine(ex.Message);
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }
        private string GetStaffName(string? staffId)
        {
            if (string.IsNullOrEmpty(staffId))
                return LangSvc.GetText("Unassigned");

            var staff = staffList.FirstOrDefault(s =>
                string.Equals(s.MasterAccountID, staffId, StringComparison.OrdinalIgnoreCase));

            return staff?.AccountName ?? staffId;
        }
        private string GetStatusClass(int labelId) => labelId switch
        {
            4 => "label-paid",
            3 => "label-pending",
            6 => "label-cancelled",
            5 => "label-noshow",
            1 => "label-confirmed",
            2 => "label-completed",
            _ => ""
        };
        private string GetStatusLabel(int statusId) => statusId switch
        {
            1 => "Confirmed",
            2 => "Completed",
            3 => "Pending Payment",
            4 => "Paid",
            5 => "No Show",
            6 => "Cancelled",
            _ => string.Empty
        };
        private List<PackageBalance> packageData = new();
        private async Task LoadPackageAsync()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                var today = DateTime.Today;
                var customerID = "All";
                var cutOffDateTime = today.AddMonths(1).AddDays(1).AddTicks(-1);
                var branches = AppState.SelectedBranchID;

                ApiResponse<List<PackageBalance>>? apiResponse = null;

                apiResponse = await ReportService.GetBalanceByCustomerID_NonExpiredAsync(
                    customerID, cutOffDateTime, branches);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    packageData = apiResponse.Result;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while fetching data.";
                Console.WriteLine(ex.Message);
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private List<MemberCreditBalance> creditData = new();
        private async Task LoadCreditAsync()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                var today = DateTime.Today;
                var customerID = "All";
                var cutOffDateTime = today.AddMonths(1).AddDays(1).AddTicks(-1);
                var branches = AppState.SelectedBranchID;

                ApiResponse<List<MemberCreditBalance>>? apiResponse = null;

                apiResponse = await ReportService.GetMemberCreditBalanceSummary_WithExpiry_NonExpiredAsync(
                            customerID, cutOffDateTime, branches);


                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    creditData = apiResponse.Result;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error loading data: {ex.Message}";
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task LoadLowStockItemsAsync()
        {
            isLoadingLowStock = true;
            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
                var apiResponse = await ReportService.GetStockBelowReorderPointAsync(branchId);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    lowStockItems = apiResponse.Result; // This should work
                }
                else
                {
                    lowStockItems = new List<LowStockItem>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading low stock items: {ex.Message}");
                lowStockItems = new List<LowStockItem>();
            }
            finally
            {
                isLoadingLowStock = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        public class LowStockItem
        {
            public string InventoryItemAccountID { get; set; } = string.Empty;
            public string DisplayCode { get; set; } = string.Empty;
            public string ProductCode { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public string ItemGroupName { get; set; } = string.Empty;
            public string BrandName { get; set; } = string.Empty;
            public decimal ExistingQuantity { get; set; }
            public decimal StockReorderLevel { get; set; }
            public decimal StockMaxLevel { get; set; }
            public decimal StockPackLevel { get; set; }
            public decimal SalesOrderQuantity { get; set; }
            public decimal VendorOrderedQuantity { get; set; }
            public decimal ShortFall { get; set; }
            public decimal RecommendedOrderQuantiity { get; set; }
            public decimal NeedToOrder { get; set; }
            public bool IsPurchased { get; set; }
            public string MasterAccountBranchID { get; set; } = string.Empty;
            public string? DefaultSupplierID { get; set; }
        }
    }
}
