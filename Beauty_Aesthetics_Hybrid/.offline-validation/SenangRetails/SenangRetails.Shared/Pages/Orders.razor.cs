using EBI.DM;
using EBI.Enum;
using EBI.UC;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Enums;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersBalanceSummary;
using SenangRetails.Shared.Models.DTOs.MembersCredit;
using SenangRetails.Shared.Models.DTOs.MembersPackage;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.CashDiscountService;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.CustomerService;
using SenangRetails.Shared.Services.EmployeeService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.MembersCreditService;
using SenangRetails.Shared.Services.MembershipTypeService;
using SenangRetails.Shared.Services.MembersPackageService;
using SenangRetails.Shared.Services.NotificationService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using SenangRetails.Shared.Services.TaxRateService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Pages
{
    public partial class Orders : IDisposable
    {
        [Inject] private ProductCacheService ProductCacheService { get; set; } = default!;
        [Inject] private IInventoryService InventoryService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.PaymentService.IPaymentService PaymentService { get; set; } = default!;
        [Inject] private ICashSalesService CashSalesService { get; set; } = default!;
        [Inject] private IEmployeeService EmployeeService { get; set; } = default!;
        [Inject] private ICustomerService _customerService { get; set; } = default!;
        [Inject] private INotificationService NotificationSvc { get; set; } = default!;
        [Inject] private ICashDiscountService cashDiscountService { get; set; } = default!;
        [Inject] private IGstTaxRateService GstTaxRateService { get; set; } = default!;
        [Inject] private IMembershipTypeService MembershipTypeService { get; set; } = default!;
        [Inject] private IMembersPackageService MembersPackageService { get; set; } = default!;
        [Inject] private IMembersCreditService MembersCreditService { get; set; } = default!;
        [Inject] private StaffService StaffService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.CashDrawerService.ICashDrawerService CashDrawerSvc { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.PriceGroupService.IPriceGroupService PriceGroupService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.PromotionSetupService.IPromotionSetupService PromotionSetupService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.CommissionSetupService.ICommissionSetupService CommissionService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.BarcodeSetupService.IBarcodeSetupService BarcodeService { get; set; } = default!;

        private string inlineBarcode = "";
        private List<PromotionSetupModel> activePromotions = new();
        private Dictionary<string, decimal> productPriceGroupOverrides = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, List<InventoryDM>> groupedItems = new();
        private string selectedCategory = "";
        private bool isLoadingItems = true;
        private List<MembershipTypeDM> AllMembershipTypes = new();
        private string selectedInventoryType = "All";
        private bool isSalesMode = true;
        private List<CashSales_Series_UnconsumedItemDM> customerPackages = new();
        private bool isLoadingPackages = false;
        private List<CashSales_Series_UnconsumedItemDM> redemptionHistory = new();
        private bool isLoadingHistory = false;
        private string? selectedPackageAutoID;
        private List<ARAPOutstanding_MemberCreditDM> customerCreditDetails = new();
        private List<ARAPOutstandingDM> creditRedemptionHistory = new();
        private bool isLoadingCreditHistory = false;
        private string? selectedCreditArapID;
        private MemberBalanceSummaryResult? balanceSummary;
        private MemberOtherBalanceSummaryResult? otherBalanceSummary;
        private bool showModeSwitchConfirm = false;
        private bool showSelectCustomerModal = false;
        private bool pendingModeTarget;
        private bool statsExpanded = false;
        public DateTime SchedulerLayoutAppointmentStartTime { get; set; } //for user to pass in service start time, for end time auto calculation for service item with duration setting.
        private int mintNewDocumentLineID = 1;
        public bool mblnUseRounding { get; set; }

        private Doc_CashSales? mobjDoc_CashSales;
        private CustomerDM? currentCustomer;
        private DateTime mPreSelectedStartTime;
        private DateTime mPreSelectedEndTime;
        private string mPreSelectedSeatNo = "";
        private bool mPreSelectedIsOpenItem;
        private DateTime mSchedulerLayoutAppointmentStartTime;

        // Handle customer search
        private string searchTerm = "";   // for the api
        private List<CustomerDM> _customerList = new List<CustomerDM>();
        private CancellationTokenSource? _cts;
        private string CustomerSearchTerm  // for the ui
        {
            get => searchTerm;
            set
            {
                searchTerm = value;

                // Cancel the previous timer/search if the user types again
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                // Start a delayed task
                _ = DebouncedSearch(_cts.Token);
            }
        }

        private async Task DebouncedSearch(CancellationToken ct)
        {
            try
            {
                // Wait 300ms. If the user types again, this task is cancelled.
                await Task.Delay(300, ct);

                if (!ct.IsCancellationRequested)
                {
                    _customerList = await _customerService.SearchCustomer(searchTerm);
                    StateHasChanged();
                }
            }
            catch (TaskCanceledException)
            {
                // Normal behavior when typing fast
            }
        }

        private CustomerDM? stagedCustomer;

        private async Task OpenSelectCustomerModal()
        {
            showSelectCustomerModal = true;
            stagedCustomer = AppState.SelectedCustomer;
            _customerList = await _customerService.SearchCustomer(searchTerm);
            StateHasChanged();
        }

        private async Task ConfirmCustomerSelection()
        {
            if (stagedCustomer == null) return;

            AppState.IsOutstandingPaymentMode = false;
            AppState.OutstandingPaymentAmount = 0m;

            // 1. If we already have an order with items, ensure it's saved to the held list first
            if (currentOrder != null && currentOrder.lstDocumentLine.Any())
            {
                await AutoHoldSync();
            }

            // 2. Check if this specific customer already has a HELD bill
            var existingHeldOrder = heldOrders.FirstOrDefault(o => o.objDoc_CashSales.AccountID == stagedCustomer.MasterAccountID);

            if (existingHeldOrder != null)
            {
                // Switch to the existing held bill for this customer
                currentOrder = existingHeldOrder;
                mobjDoc_CashSales = currentOrder;
                activeOrderId = existingHeldOrder.objDoc_CashSales.DocumentID;
                isSalesMode = existingHeldOrder.objDoc_CashSales.DocumentTypeID == 5;
            }
            else
            {
                // 3. Start a brand NEW sale for this new customer
                int docTypeId = isSalesMode ? 5 : 52;
                currentOrder = new Doc_CashSales(docTypeId, Guid.NewGuid().ToString());
                currentOrder.objDoc_CashSales.AccountID = stagedCustomer.MasterAccountID ?? "";

                mobjDoc_CashSales = currentOrder;
                activeOrderId = currentOrder.objDoc_CashSales.DocumentID;
                isSalesMode = true;
            }

            currentOrder.objDoc_CashSales.AccountName = stagedCustomer.AccountName ?? "";
            currentOrder.objDoc_CashSales.Phone = stagedCustomer.Phone ?? "";

            // 4. Sync AppState and LocalStorage
            AppState.SelectedCustomer = stagedCustomer;
            await JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", currentOrder.objDoc_CashSales.DocumentID);

            // 5. Reload customer specific details (Credits/Packages)
            await LoadCustomerPackageBalance();
            await LoadCustomerWalletDetails();
            await LoadSummaryBalances();

            // 6. Reset UI state and close modal
            selectedCreditToRedeem = null;
            stagedCreditToRedeem = null;
            selectedCreditsToRedeem.Clear();
            stagedCustomer = null;
            showSelectCustomerModal = false;
            StateHasChanged();
        }

        // Handle Gender Change while adding new member
        private void HandleGenderChange(string gender, object? isChecked)
        {
            bool @checked = (bool)(isChecked ?? false);

            if (@checked)
            {
                newGender = gender;
            }
            else
            {
                newGender = "";
            }
        }

        // Open Whatsapp
        private string GetWhatsAppUrl()
        {
            if (currentOrder?.objDoc_CashSales == null || string.IsNullOrEmpty(currentOrder.objDoc_CashSales.Phone)) return "";

            var cleanPhone = currentOrder.objDoc_CashSales.Phone.Replace(" ", "").Replace("(", "").Replace(")", "")
                                                               .Replace("-", "").Replace("_", "");

            if (cleanPhone.StartsWith("0"))
                cleanPhone = "60" + cleanPhone.Substring(1);
            else if (cleanPhone.StartsWith("+"))
                cleanPhone = cleanPhone.Substring(1);
            else if (!cleanPhone.StartsWith("60"))
                cleanPhone = "60" + cleanPhone;

            return $"https://wa.me/{cleanPhone}";
        }

        private async Task OpenWhatsApp()
        {
            var url = GetWhatsAppUrl();

            try
            {
                await JS.InvokeVoidAsync("open", url, "_top");
            }
            catch (Exception ex)
            {
                await JS.InvokeVoidAsync("eval", $"window.location.href = '{url}'");
            }

        }

        // Open Email
        private async Task OpenEmailClient()
        {
            if (AppState.SelectedCustomer == null || string.IsNullOrWhiteSpace(AppState.SelectedCustomer.Email))
            {
                ShowNotification("This customer does not have a registered email address.");
                return;
            }

            var email = AppState.SelectedCustomer.Email;
            var url = $"mailto:{email}?subject=";

            try { await JS.InvokeVoidAsync("open", url, "_top"); }
            catch { await JS.InvokeVoidAsync("eval", $"window.location.href = '{url}'"); }
        }

        // staff commission row
        private string commissionType = "";
        private List<StaffCommissionRow> commissionRows = new();

        public class StaffCommissionRow
        {
            public string id { get; set; } = Guid.NewGuid().ToString();
            public string StaffId { get; set; } = "";
            public string StaffName { get; set; } = "";
            public decimal Amount { get; set; } = 0;
            public string CommissionType { get; set; } = "%";
        }

        private void AddCommissionRow()
        {
            commissionRows.Add(new StaffCommissionRow());
        }

        private void RemoveCommissionRow(StaffCommissionRow row)
        {
            if (row != null)
            {
                commissionRows.Remove(row);
                StateHasChanged();
            }
        }

        private void OpenNumpadForCommission(StaffCommissionRow row)
        {
            currentCommissionRowId = row.id;
            OpenNumpadForField("StaffCommission", row.Amount.ToString("F2"));
        }

        private string? currentCommissionRowId;

        // select staff modal variables
        private bool showSelectStaffModal = false;
        private string staffSearchTerm = "";
        private StaffResponseDTO? stagedStaff = null;
        private StaffCommissionRow? activeEditingCommissionRow = null;
        private List<StaffResponseDTO> filteredStaffList = new();

        private string StaffSearchTerm
        {
            get => staffSearchTerm;
            set
            {
                if (staffSearchTerm != value)
                {
                    staffSearchTerm = value;
                    FilterStaffModalList();
                }
            }
        }

        private void OpenStaffModal(StaffCommissionRow row)
        {
            activeEditingCommissionRow = row;
            staffSearchTerm = string.Empty;
            filteredStaffList = staffList.Where(s => string.Equals(s.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
            stagedStaff = staffList.FirstOrDefault(s => s.MasterAccountID == row.StaffId);
            showSelectStaffModal = true;
        }

        private async Task ConfirmStaffSelection()
        {
            if (stagedStaff == null || activeEditingCommissionRow == null) return;
            activeEditingCommissionRow.StaffId = stagedStaff.MasterAccountID;
            activeEditingCommissionRow.StaffName = stagedStaff.AccountName;

            // Auto-calculate commission based on staff's assigned commission scheme
            try
            {
                string inEvent = "Sales";
                if (selectedOrderItem != null)
                {
                    if (selectedOrderItem.InventoryTypeID == 3) // Service
                        inEvent = "Service";
                    else if (selectedOrderItem.ActivityTypeID == 2 || selectedOrderItem.ActivityTypeID == 5)
                        inEvent = "Redemption";
                }

                if (!string.IsNullOrEmpty(stagedStaff.CommissionSchemeID))
                {
                    var (amt, type) = await CommissionService.GetCommissionForStaffAsync(stagedStaff.CommissionSchemeID, inEvent, selectedOrderItem?.UnitPrice ?? 0);
                    if (amt > 0)
                    {
                        activeEditingCommissionRow.Amount = amt;
                        activeEditingCommissionRow.CommissionType = type;
                    }
                }
                else if (selectedOrderItem != null && activeEditingCommissionRow.Amount == 0)
                {
                    // Fallback to item default commission if staff doesn't have a specific scheme
                    if (AppState?.lstAllSalesItems?.TryGetValue(selectedOrderItem.LineItemID, out var inv) == true)
                    {
                        var formula = inv.StaffCommissionA;
                        if (!string.IsNullOrEmpty(formula))
                        {
                            var (parsedAmt, isPercent) = ParseCommissionFormula(formula);
                            if (parsedAmt > 0)
                            {
                                activeEditingCommissionRow.Amount = parsedAmt;
                                activeEditingCommissionRow.CommissionType = isPercent ? "%" : "MYR";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating staff commission: {ex.Message}");
            }
            
            showSelectStaffModal = false;
            stagedStaff = null;
            staffSearchTerm = string.Empty;
            activeEditingCommissionRow = null;
            StateHasChanged();
        }

        private static (decimal amount, bool isPercent) ParseCommissionFormula(string? formula)
        {
            if (string.IsNullOrEmpty(formula)) return (0, true);
            var str = formula.TrimStart('T', 'F').TrimEnd('A');
            var isPercent = str.EndsWith("%");
            str = str.TrimEnd('%');
            if (decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (val, isPercent);
            return (0, true);
        }

        private void OpenBarcodePopup()
        {
            _barcode = "";
            isScanning = false;
            isBarcodePopupOpen = true;
        }

        private void CloseBarcodePopup()
        {
            isScanning = false;
            isBarcodePopupOpen = false;
        }

        private void StartScanning()
        {
            isScanning = true;
        }

        private void CancelScanning()
        {
            isScanning = false;
        }

        private async Task OnBarcodeScan(string scannedCode)
        {
            if (!string.IsNullOrEmpty(scannedCode))
            {
                _barcode = scannedCode;
                isScanning = false;
                await SubmitBarcode();
            }
        }

        private async Task HandleBarcodeKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await SubmitBarcode();
            }
        }

        private async Task HandleInlineBarcodeKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await SubmitInlineBarcode();
            }
        }

        private async Task SubmitInlineBarcode()
        {
            if (string.IsNullOrWhiteSpace(inlineBarcode)) return;
            var code = inlineBarcode.Trim();
            inlineBarcode = "";

            await ProcessScannedBarcode(code, isInline: true);
            StateHasChanged();
        }

        private async Task SubmitBarcode()
        {
            if (string.IsNullOrWhiteSpace(_barcode)) return;
            var scannedBarcode = _barcode.Trim();
            CloseBarcodePopup();

            await ProcessScannedBarcode(scannedBarcode, isInline: false);
            StateHasChanged();
        }

        private async Task ProcessScannedBarcode(string rawBarcode, bool isInline)
        {
            if (string.IsNullOrWhiteSpace(rawBarcode)) return;
            var code = rawBarcode.Trim();

            // 1. First, evaluate against Barcode Reading Setup rules (Scale / Price / Quantity embedded barcodes)
            try
            {
                var parsedResult = await BarcodeService.ParseBarcodeAsync(code);
                if (parsedResult != null && parsedResult.Success && !string.IsNullOrEmpty(parsedResult.ItemCode))
                {
                    var ruleItem = groupedItems.Values
                        .SelectMany(x => x)
                        .FirstOrDefault(item =>
                            string.Equals(item.DisplayCode, parsedResult.ItemCode, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(item.VendorItemCode, parsedResult.ItemCode, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(item.MasterAccountID, parsedResult.ItemCode, StringComparison.OrdinalIgnoreCase));

                    if (ruleItem != null)
                    {
                        decimal overridePrice = parsedResult.Price ?? 0;
                        decimal overrideQty = parsedResult.Quantity > 0 ? parsedResult.Quantity : 1;
                        await AddToOrder(ruleItem, dclOverrideUnitPrice: overridePrice, dclOverrideQuantity: overrideQty);

                        string priceInfo = parsedResult.Price.HasValue ? $", RM {parsedResult.Price.Value:F2}" : "";
                        ShowNotification($"Scanned {ruleItem.SalesDescription} (Qty: {overrideQty}{priceInfo})");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing barcode rule: {ex.Message}");
            }

            // 2. Direct catalog match by VendorItemCode, DisplayCode, or MasterAccountID
            var matchedItem = groupedItems.Values
                .SelectMany(x => x)
                .FirstOrDefault(item =>
                    string.Equals(item.VendorItemCode, code, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.DisplayCode, code, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.MasterAccountID, code, StringComparison.OrdinalIgnoreCase));

            if (matchedItem != null)
            {
                await AddToOrder(matchedItem);
                ShowNotification($"Added {matchedItem.SalesDescription}");
            }
            else
            {
                if (isInline)
                {
                    ShowNotification($"Item with barcode/code \"{code}\" not found.");
                }
                else
                {
                    ShowAlertDialog("Barcode Scanner", $"Item with barcode \"{code}\" not found in the catalog.");
                }
            }
        }

        private void FilterStaffModalList()
        {
            var baseActiveStaff = staffList.Where(s => string.Equals(s.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(staffSearchTerm))
            {
                filteredStaffList = baseActiveStaff.ToList();
            }
            else
            {
                filteredStaffList = baseActiveStaff
                    .Where(s => s.AccountName != null && s.AccountName.Contains(staffSearchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }


        // --- For showVoucherModal (Packages) ---
        private async Task ProceedToEditPackageRedemption()
        {
            if (isSalesMode)
            {
                ShowNotification("Please switch to 'Redemption' mode at the top bar before redeeming packages.");
                return;
            }

            if (selectedPackageToRedeem == null)
            {
                ShowNotification("Please select a package first.");
                return;
            }

            isRedeemingPackage = true;
            StateHasChanged();
            await ApplyPackageRedemption();
        }

        // For the credit modal
        private ARAPOutstanding_MemberCreditDM? selectedCreditToRedeem;
        private ARAPOutstanding_MemberCreditDM? stagedCreditToRedeem;
        private List<ARAPOutstanding_MemberCreditDM> selectedCreditsToRedeem = new();
        private bool showMultiCreditModal = false;

        private async Task FetchCreditHistory(string arapId)
        {
            selectedCreditArapID = arapId;
            stagedCreditToRedeem = customerCreditDetails.FirstOrDefault(c => c.ARAPOutstandingID == arapId);

            isLoadingCreditHistory = true;
            creditRedemptionHistory.Clear();
            StateHasChanged();

            try
            {
                creditRedemptionHistory = await MembersCreditService.GetCreditRedemptionHistoryAsync(arapId);
            }
            finally
            {
                isLoadingCreditHistory = false;
                StateHasChanged();
            }
        }

        private void OpenMultiCreditModal()
        {
            foreach (var credit in customerCreditDetails)
            {
                credit.IsSelected = selectedCreditsToRedeem.Any(c => c.ARAPOutstandingID == credit.ARAPOutstandingID);
            }
            showMultiCreditModal = true;
            StateHasChanged();
        }

        private void ToggleCreditSelection(ARAPOutstanding_MemberCreditDM credit, bool isSelected)
        {
            credit.IsSelected = isSelected;
            if (isSelected)
            {
                if (!selectedCreditsToRedeem.Any(c => c.ARAPOutstandingID == credit.ARAPOutstandingID))
                {
                    selectedCreditsToRedeem.Add(credit);
                }
            }
            else
            {
                selectedCreditsToRedeem.RemoveAll(c => c.ARAPOutstandingID == credit.ARAPOutstandingID);
            }
            StateHasChanged();
        }

        private void ApplyMultiCreditSelection()
        {
            selectedCreditToRedeem = selectedCreditsToRedeem.FirstOrDefault();
            stagedCreditToRedeem = selectedCreditToRedeem;
            selectedCreditArapID = selectedCreditToRedeem?.ARAPOutstandingID;
            showMultiCreditModal = false;

            if (mobjDoc_CashSales != null)
            {
                mobjDoc_CashSales.Recalculate();
                _ = CalculateTotals();
                _ = AutoHoldSync();
            }
            StateHasChanged();
        }

        private void CloseMultiCreditModal()
        {
            showMultiCreditModal = false;
            StateHasChanged();
        }

        private void ProceedToEditCreditRedemption()
        {
            if (isSalesMode)
            {
                ShowNotification("Please switch to 'Redemption' mode at the top bar first.");
                return;
            }

            if (stagedCreditToRedeem == null)
            {
                ShowNotification("Please select a credit account from the table.");
                return;
            }
            selectedCreditToRedeem = stagedCreditToRedeem;
            selectedCreditsToRedeem.Clear();
            selectedCreditsToRedeem.Add(selectedCreditToRedeem);
            showBalanceModal = false;

            if (mobjDoc_CashSales != null)
            {
                mobjDoc_CashSales.Recalculate();
                _ = CalculateTotals();
                _ = AutoHoldSync();
            }
        }

        private void ClearCreditSelection()
        {
            selectedCreditToRedeem = null;
            stagedCreditToRedeem = null;
            selectedCreditArapID = null;
            selectedCreditsToRedeem.Clear();
            foreach (var credit in customerCreditDetails)
            {
                credit.IsSelected = false;
            }
            creditRedemptionHistory.Clear();
            showBalanceModal = false;
            showMultiCreditModal = false;

            if (mobjDoc_CashSales != null)
            {
                mobjDoc_CashSales.Recalculate();
                _ = CalculateTotals();
                _ = AutoHoldSync();
            }
        }

        // Load customer credit, package and points
        private async Task LoadSummaryBalances()
        {
            balanceSummary = null;
            otherBalanceSummary = null;

            if (currentOrder?.objDoc_CashSales == null || string.IsNullOrEmpty(currentOrder.objDoc_CashSales.AccountID)) return;

            var summary = await _customerService.GetMemberBalanceSummaryAsync(currentOrder.objDoc_CashSales.AccountID);
            if (summary != null)
            {
                balanceSummary = summary;
            }

            var otherSummaryList = await _customerService.GetMemberOtherBalanceSummaryAsync(currentOrder.objDoc_CashSales.AccountID, DateTime.UtcNow);
            if (otherSummaryList != null && otherSummaryList.Any())
            {
                otherBalanceSummary = otherSummaryList.FirstOrDefault();
            }
            StateHasChanged();
        }

        private async Task LoadCustomerWalletDetails()
        {
            customerCreditDetails.Clear();
            selectedCreditsToRedeem.Clear();

            if (currentOrder?.objDoc_CashSales == null || string.IsNullOrEmpty(currentOrder.objDoc_CashSales.AccountID)) return;

            var allCredits = await MembersCreditService.GetCustomerCreditDetailsAsync(currentOrder.objDoc_CashSales.AccountID);
            if (allCredits != null)
            {
                customerCreditDetails = allCredits
                    .Where(c => c.DueDate >= DateTime.Now)
                    .OrderBy(c => c.ARAPOutstandingID)
                    .ToList();

                // If in redemption mode, restore credit selections from existing document lines
                if (currentOrder.objDoc_CashSales.DocumentTypeID == 52 && currentOrder.lstDocumentLine != null)
                {
                    foreach (var line in currentOrder.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted))
                    {
                        if (!string.IsNullOrEmpty(line.MemberCreditAccountID))
                        {
                            var match = customerCreditDetails.FirstOrDefault(c => c.ARAPOutstandingID == line.MemberCreditAccountID);
                            if (match != null)
                            {
                                match.IsSelected = true;
                                if (!selectedCreditsToRedeem.Any(c => c.ARAPOutstandingID == match.ARAPOutstandingID))
                                {
                                    selectedCreditsToRedeem.Add(match);
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(line.MembershipCredit))
                        {
                            var parsed = ParseMembershipCredit(line.MembershipCredit);
                            foreach (var pCredit in parsed)
                            {
                                var matches = customerCreditDetails.Where(c => c.MemberTypeID == pCredit.MemberTypeID).ToList();
                                foreach (var match in matches)
                                {
                                    match.IsSelected = true;
                                    if (!selectedCreditsToRedeem.Any(c => c.ARAPOutstandingID == match.ARAPOutstandingID))
                                    {
                                        selectedCreditsToRedeem.Add(match);
                                    }
                                }
                            }
                        }
                    }

                    selectedCreditToRedeem = selectedCreditsToRedeem.FirstOrDefault();
                    stagedCreditToRedeem = selectedCreditToRedeem;
                    selectedCreditArapID = selectedCreditToRedeem?.ARAPOutstandingID;
                }
            }

            StateHasChanged();
        }

        private async Task FetchHistory(string autoId)
        {
            selectedPackageAutoID = autoId;
            isLoadingHistory = true;
            redemptionHistory.Clear();
            StateHasChanged();

            try
            {
                redemptionHistory = await MembersPackageService.GetRedemptionHistoryAsync(autoId);
            }
            finally
            {
                isLoadingHistory = false;
                StateHasChanged();
            }
        }

        private async Task SetSalesMode(bool isSales)
        {
            if (isSalesMode == isSales) return;

            pendingModeTarget = isSales;
            showModeSwitchConfirm = true;
            StateHasChanged();
        }

        private async Task ConfirmModeSwitch()
        {
            if (currentOrder != null)
            {
                currentOrder.lstDocumentLine.Clear();
                await AutoHoldSync();
            }

            await ExecuteModeSwitch(pendingModeTarget);
            showModeSwitchConfirm = false;
        }

        private bool showMoreActionsMenu = false;

        private void ToggleMoreActions()
        {
            showMoreActionsMenu = !showMoreActionsMenu;
        }

        private void TriggerScanBarcode()
        {
            showMoreActionsMenu = false;
            OpenBarcodePopup();
        }

        private async Task TriggerCashDrawer()
        {
            showMoreActionsMenu = false;
            await OpenCashDrawerModal();
        }

        private bool showCashDrawerModal = false;
        private string cashDrawerTab = "Cash In"; // "Cash In", "Cash Out", "Logs"
        private decimal cashDrawerAmount = 0;
        private string cashDrawerReason = "Starting Float";
        private string cashDrawerNotes = "";
        private DateTime cashDrawerDate = DateTime.Today;
        private string cashDrawerTimeString = DateTime.Now.ToString("HH:mm");
        private string cashDrawerPerformedBy = "Admin";
        private string cashDrawerBranch = "HQ";
        private string cashDrawerCounter = "Counter 1";
        private bool isSavingCashDrawer = false;
        private CashDrawerSummaryModel? cashDrawerSummary;
        private List<CashDrawerLogModel> cashDrawerLogs = new();

        private async Task OpenCashDrawerModal()
        {
            showCashDrawerModal = true;
            cashDrawerTab = "Cash In";
            cashDrawerAmount = 0;
            cashDrawerReason = "Starting Float";
            cashDrawerNotes = "";
            cashDrawerDate = DateTime.Today;
            cashDrawerTimeString = DateTime.Now.ToString("HH:mm");
            cashDrawerPerformedBy = !string.IsNullOrEmpty(AppState?.UserEmail) ? AppState.UserEmail : (!string.IsNullOrEmpty(AppState?.UserID) ? AppState.UserID : "Admin");
            cashDrawerBranch = AppState?.CurrentBranch?.Branch ?? "HQ";
            cashDrawerCounter = "Counter 1";
            await RefreshCashDrawerData();
        }

        private void CloseCashDrawerModal()
        {
            showCashDrawerModal = false;
        }

        private async Task SelectCashDrawerTab(string tab)
        {
            cashDrawerTab = tab;
            if (tab == "Cash In") cashDrawerReason = "Starting Float";
            else if (tab == "Cash Out") cashDrawerReason = "Petty Cash Expense";

            await RefreshCashDrawerData();
        }

        private async Task RefreshCashDrawerData()
        {
            if (CashDrawerSvc != null)
            {
                cashDrawerSummary = await CashDrawerSvc.GetSummaryAsync();
                cashDrawerLogs = await CashDrawerSvc.GetLogsAsync();
            }
            StateHasChanged();
        }

        private async Task SaveCashDrawerEntry()
        {
            if (cashDrawerAmount <= 0)
            {
                ShowNotification("Please enter a valid amount greater than RM 0.00");
                return;
            }

            isSavingCashDrawer = true;
            try
            {
                DateTime logTimestamp = cashDrawerDate;
                if (TimeSpan.TryParse(cashDrawerTimeString, out var ts))
                {
                    logTimestamp = cashDrawerDate.Date.Add(ts);
                }

                var log = new CashDrawerLogModel
                {
                    Type = cashDrawerTab,
                    Amount = cashDrawerAmount,
                    Reason = cashDrawerReason,
                    Notes = cashDrawerNotes,
                    PerformedBy = string.IsNullOrWhiteSpace(cashDrawerPerformedBy) ? "Staff" : cashDrawerPerformedBy,
                    Branch = string.IsNullOrWhiteSpace(cashDrawerBranch) ? "HQ" : cashDrawerBranch,
                    Counter = string.IsNullOrWhiteSpace(cashDrawerCounter) ? "Counter 1" : cashDrawerCounter,
                    Timestamp = logTimestamp
                };

                var success = await CashDrawerSvc.RecordTransactionAsync(log);
                if (success)
                {
                    ShowNotification($"{cashDrawerTab} of RM {cashDrawerAmount:N2} recorded successfully.");
                    cashDrawerAmount = 0;
                    cashDrawerNotes = "";
                    await RefreshCashDrawerData();
                }
                else
                {
                    ShowNotification("Failed to record cash drawer transaction.");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Error: {ex.Message}");
            }
            finally
            {
                isSavingCashDrawer = false;
                StateHasChanged();
            }
        }

        private async Task ExecuteModeSwitch(bool targetMode)
        {
            isSalesMode = targetMode;

            if (currentOrder != null)
            {
                currentOrder.objDoc_CashSales.DocumentTypeID = targetMode ? 5 : 52; // 5=Sales, 52=Redemption
                await AutoHoldSync();
            }
            StateHasChanged();
        }

        private async Task LoadCustomerPackageBalance()
        {
            customerPackages.Clear();

            if (currentOrder?.objDoc_CashSales == null || string.IsNullOrEmpty(currentOrder.objDoc_CashSales.AccountID)) return;

            isLoadingPackages = true;
            try
            {
                var rawPackages = await MembersPackageService.GetPackageBalanceByCustomerIDAsync(currentOrder.objDoc_CashSales.AccountID);
                if (rawPackages != null)
                {
                    customerPackages = rawPackages;
                }
            }
            finally
            {
                isLoadingPackages = false;
                StateHasChanged();
            }
        }

        private string GetItemTypeName(int typeId) => typeId switch
        {
            1 => "Product",
            3 => "Service",
            5 => "Package",
            7 => "TopUp",
            _ => "Product"
        };

        private List<string> categories => groupedItems
            .SelectMany(kvp => kvp.Value)
            .Where(item => selectedInventoryType == "All" || GetItemTypeName(item.InventoryTypeID) == selectedInventoryType)
            .Select(item => string.IsNullOrWhiteSpace(item.ItemGroupName) ? "Uncategorized" : item.ItemGroupName)
            .Distinct()
            .OrderBy(name => name == "Uncategorized")
            .ThenBy(name => name)
            .ToList();

        private void SelectInventoryType(string type)
        {
            selectedInventoryType = type;
            selectedCategory = "";
            StateHasChanged();
        }

        private List<string> GetVisibleCategories()
        {
            var availableInType = categories;

            var baseCategories = string.IsNullOrEmpty(selectedCategory)
                ? availableInType
                : availableInType.Where(c => c == selectedCategory);

            return baseCategories.Where(cat =>
                groupedItems.ContainsKey(cat) &&
                groupedItems[cat].Any(item =>
                    (selectedInventoryType == "All" || GetItemTypeName(item.InventoryTypeID) == selectedInventoryType) &&
                    (string.IsNullOrWhiteSpace(itemSearchTerm) || (item.AccountName ?? "").Contains(itemSearchTerm, StringComparison.OrdinalIgnoreCase))
                )
            ).ToList();
        }

        // Load staff for staff commission row
        private List<StaffResponseDTO> staffList = new();
        private bool isStaffLoading = false;
        private async Task LoadStaffDataAsync()
        {
            isStaffLoading = true;
            try
            {
                var response = await StaffService.GetStaffListAsync();
                if (response != null && response.StatusCode == 200 && response.Result != null)
                {
                    staffList = response.Result
                        .Where(s => string.Equals(s.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(s => s.AccountName)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading staff for commission: {ex.Message}");
            }
            finally
            {
                isStaffLoading = false;
                StateHasChanged();
            }
        }

        // For the confirmation dialog
        private bool showGenericConfirm = false;
        private string confirmTitle = "";
        private string confirmMessage = "";
        private Func<Task>? onConfirmAction;

        private void AskConfirmation(string title, string message, Func<Task> action)
        {
            confirmTitle = title;
            confirmMessage = message;
            onConfirmAction = action;
            showGenericConfirm = true;
        }

        // Delete Order Confirmation
        private void RequestDeleteOrder()
        {
            AskConfirmation(
                "Delete Order?",
                "Are you sure you want to clear the entire cart? This action cannot be undone.",
                async () => { await DeleteCurrentOrder(); });
        }

        // Remove Item Confirmation
        private void RequestRemoveItem(DocumentLineTableDM item)
        {
            AskConfirmation(
                "Remove Item?",
                $"Are you sure you want to remove '{item.Description}' from the cart?",
                async () => {
                    RemoveItem(item);
                    await AutoHoldSync();
                });
        }

        private async Task ChangeQuantity(DocumentLineTableDM item, decimal delta)
        {
            if (item == null) return;

            decimal newQty = item.Quantity + delta;

            if (newQty <= 0)
            {
                AskConfirmation(
                    "Remove Item?",
                    $"Setting quantity to 0 will remove '{item.Description}' from the cart. Proceed?",
                    async () => {
                        RemoveItem(item);
                        await AutoHoldSync();
                    });
            }
            else
            {
                if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52 && item.ActivityTypeID == 6 && delta > 0)
                {
                    decimal newItemCost = item.UnitPrice * delta;
                    decimal currentCartTotal = mobjDoc_CashSales.lstDocumentLine
                        .Where(x => x.SaveAction != EntityState.Deleted && x.ActivityTypeID == 6)
                        .Sum(x => x.UnitPrice * x.Quantity);
                    decimal requiredTotal = currentCartTotal + newItemCost;

                    decimal selectedCreditTotal = selectedCreditsToRedeem.Sum(x => x.NetBalanceAfterUtilised);

                    if (selectedCreditsToRedeem.Any() && selectedCreditTotal < requiredTotal)
                    {
                        OpenMultiCreditModal();
                        ShowNotification($"Selected credit amount is insufficient to cover the expenses. Please select additional credit(s) to cover the total of RM {requiredTotal:F2}.");
                        return;
                    }
                }

                // Verify redemption package balance limit on increment
                if (delta > 0 && (item.ActivityTypeID == 2 || item.ActivityTypeID == 5) && !string.IsNullOrEmpty(item.SourceDocumentLineID))
                {
                    var pkg = customerPackages.FirstOrDefault(p => p.DocumentLineID == item.SourceDocumentLineID || p.AutoID == item.SourceDocumentLineID);
                    if (pkg != null)
                    {
                        decimal otherLinesQty = currentOrder?.lstDocumentLine
                            .Where(line => line != item && 
                                           line.SaveAction != EntityState.Deleted && 
                                           (line.ActivityTypeID == 2 || line.ActivityTypeID == 5) && 
                                           line.SourceDocumentLineID == item.SourceDocumentLineID)
                            .Sum(line => line.Quantity) ?? 0;

                        decimal maxAllowed = pkg.NetBalanceAfterUtilised - otherLinesQty;
                        if (maxAllowed < 0) maxAllowed = 0;

                        if (newQty > maxAllowed)
                        {
                            ShowAlertDialog("Limit Exceeded", $"Quantity cannot exceed the available balance of <strong>{maxAllowed}</strong>.");
                            return;
                        }
                    }
                }

                item.Quantity = newQty;
                // Cap the discount so it does not exceed the new subtotal
                decimal subtotal = item.UnitPrice * item.Quantity;
                if (item.Discount > subtotal)
                {
                    item.Discount = subtotal;
                }
                await UpdateLineAfterQuantityChanged(item);
            }
        }

        private bool showMobileCart = false;

        private void OpenMobileCart() => showMobileCart = true;
        private void CloseMobileCart() => showMobileCart = false;

        protected override void OnInitialized()
        {
            ProductCacheService.OnCacheUpdated += HandleCacheUpdated;
        }

        public void Dispose()
        {
            ProductCacheService.OnCacheUpdated -= HandleCacheUpdated;
        }

        private void ApplyCachedItems(List<InventoryDM> items)
        {
            var displayedItems = items.Where(i =>
                !(i.InventoryTypeID == 5 && i.lstPackage != null && i.lstPackage.Any(p => p.PackageQuantityTypeID == 1))
            ).ToList();

            groupedItems = displayedItems
                .GroupBy(i => string.IsNullOrWhiteSpace(i.ItemGroupName) ? "Uncategorized" : i.ItemGroupName)
                .ToDictionary(g => g.Key, g => g.ToList());

            AppState.lstAllSalesItems = items
                .Where(i => !string.IsNullOrWhiteSpace(i.MasterAccountID))
                .GroupBy(i => i.MasterAccountID, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        private void HandleCacheUpdated()
        {
            InvokeAsync(async () =>
            {
                if (ProductCacheService.Items != null)
                {
                    await RefreshPriceGroupOverridesAsync();
                    ApplyCachedItems(ProductCacheService.Items);
                    StateHasChanged();
                }
            });
        }

        protected override async Task OnInitializedAsync()
        {
            AppState.IsOutstandingPaymentMode = false;
            AppState.OutstandingPaymentAmount = 0m;
            await LoadHeldBillsFromStorage();
            await LoadStaffDataAsync();

            if (currentOrder == null)
            {
                currentOrder = new Doc_CashSales(5, Guid.NewGuid().ToString());
            }
            mobjDoc_CashSales = currentOrder;

            //if (AppState.SelectedCustomer != null)
            //{
            //    var member = AppState.SelectedCustomer;

            //    if (currentOrder.objDoc_CashSales == null || currentOrder.objDoc_CashSales.AccountID != member.MasterAccountID)
            //    {
            //        var existingHeldOrder = heldOrders.FirstOrDefault(o => o.objDoc_CashSales.AccountID == member.MasterAccountID);

            //        if (existingHeldOrder != null)
            //        {
            //            currentOrder = existingHeldOrder;
            //            mobjDoc_CashSales = currentOrder;
            //            activeOrderId = existingHeldOrder.objDoc_CashSales.DocumentID;
            //            isSalesMode = existingHeldOrder.objDoc_CashSales.DocumentTypeID == 5;
            //        }
            //        else
            //        {
            //            currentOrder = new Doc_CashSales(5, Guid.NewGuid().ToString());
            //            currentOrder.objDoc_CashSales.AccountID = member.MasterAccountID ?? string.Empty;
            //            currentOrder.objDoc_CashSales.AccountName = member.AccountName ?? "";
            //            currentOrder.objDoc_CashSales.Phone = member.Phone ?? "";

            //            mobjDoc_CashSales = currentOrder;
            //            activeOrderId = currentOrder.objDoc_CashSales.DocumentID;
            //            isSalesMode = true;
            //        }

            //        await JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", currentOrder.objDoc_CashSales.DocumentID);
            //    }
            //}
            try
            {
                var savedJson = await JS.InvokeAsync<string>("localStorage.getItem", SettingsKey);
                if (!string.IsNullOrEmpty(savedJson))
                {
                    var savedSettings = JsonSerializer.Deserialize<FormFieldSettings>(savedJson);
                    if (savedSettings != null) formSettings = savedSettings;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            try
            {
                activePromotions = await PromotionSetupService.GetPromotionsAsync() ?? new();
            }
            catch (Exception ex) { Console.WriteLine($"Error loading promotions: {ex.Message}"); }

            isLoadingItems = true;
            var cachedOrOnlineItems = await ProductCacheService.EnsureLoadedAsync(AppState.SelectedBranchID);
            if (cachedOrOnlineItems != null && cachedOrOnlineItems.Count > 0)
            {
                ApplyCachedItems(cachedOrOnlineItems);
            }
            isLoadingItems = false;
            ProductCacheService.TriggerBackgroundRefresh();

            var mtResponse = await MembershipTypeService.GetAllMembershipTypesAsync();
            if (mtResponse != null)
            {
                AllMembershipTypes = mtResponse.Where(x => x.Active).ToList();
            }
            isLoadingItems = false;

            if (currentOrder?.objDoc_CashSales != null && !string.IsNullOrEmpty(currentOrder.objDoc_CashSales.AccountID))
            {
                await LoadCustomerPackageBalance();
                await LoadCustomerWalletDetails();
                await LoadSummaryBalances();
            }
            await RefreshPriceGroupOverridesAsync();
            StateHasChanged();
        }

        private async Task RefreshPriceGroupOverridesAsync()
        {
            try
            {
                var groups = await PriceGroupService.GetPriceGroupsAsync();
                var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                if (groups != null)
                {
                    foreach (var g in groups)
                    {
                        if (g.Price > 0 && g.AppliedProductIds != null)
                        {
                            foreach (var pid in g.AppliedProductIds)
                            {
                                if (!string.IsNullOrWhiteSpace(pid))
                                    overrides[pid.Trim()] = g.Price;
                            }
                        }
                    }
                }
                productPriceGroupOverrides = overrides;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading price group overrides: {ex.Message}");
            }

            try
            {
                activePromotions = await PromotionSetupService.GetPromotionsAsync() ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading promotions: {ex.Message}");
            }
        }

        private void SelectCategory(string category)
        {
            selectedCategory = category;
            StateHasChanged();
        }

        private bool HasResultsForCategory(string cat)
        {
            if (!groupedItems.ContainsKey(cat)) return false;
            if (string.IsNullOrWhiteSpace(itemSearchTerm)) return true;
            return groupedItems[cat].Any(item =>
                (item.AccountName ?? "").Contains(itemSearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        private bool isAddingNewItem = false;

        private async Task AddToOrder(InventoryDM objSelectedInventory, Inventory_SKUDM? objSelectedSKU = null, string strEmployeeID = "", string strSalesPersonCode = "", CashSales_Series_UnconsumedItemDM? objUnconsumedItem = null, DateTime? dtStartTime = null, DateTime? dtEndTime = null, string strSeatNo = "", decimal dclOverrideUnitPrice = 0, decimal dclOverrideQuantity = 1, string strOverrideCustomerName = "")
        {
            EmployeeDM objEmp;
            InventoryDM drowInventory;
            Inventory_SKUDM drowSKU;

            if (mobjDoc_CashSales == null) return;

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && string.IsNullOrWhiteSpace(mobjDoc_CashSales.objDoc_CashSales.AccountID))
            {
                ShowNotification("Redemption must have selected customer.");
                return;
            }

            var taxRate = await GstTaxRateService.GetRateForTaxCodeAsync(objSelectedInventory.TaxCodeID);
            var lstEmployee = await EmployeeService.GetActiveEmployeesByBranch(AppState.SelectedBranchID);

            if (objSelectedInventory.InventoryTypeID == 5 && string.IsNullOrWhiteSpace(mobjDoc_CashSales.objDoc_CashSales.AccountID))
            {
                ShowNotification("Please select a customer before adding [Package] item.");
                //set to focus the scan barcode field from here if necessary
                return;
            }

            if (objSelectedInventory.InventoryTypeID == 7 && string.IsNullOrWhiteSpace(mobjDoc_CashSales.objDoc_CashSales.AccountID))
            {
                ShowNotification("Please select a customer before adding [Member Credit] item.");
                //set to focus the scan barcode field from here if necessary
                return;
            }

            if (objSelectedInventory.InventoryTypeID == 11 && string.IsNullOrWhiteSpace(mobjDoc_CashSales.objDoc_CashSales.AccountID))
            {
                ShowNotification("Please select a customer before adding [Deposit] item.");
                //set to focus the scan barcode field from here if necessary
                return;
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && objSelectedInventory.InventoryTypeID == 11)
            {
                ShowNotification("Please switch to [Sales Mode] to receive deposit");
                //set to focus the scan barcode field from here if necessary
                return;
            }

            if (lstEmployee.Any(x => x.MasterAccountID == strEmployeeID))
            {
                objEmp = lstEmployee.First(x => x.MasterAccountID == strEmployeeID);
            }

            //Check if it is adding from Scheduler Interface, if yes, [SchedulerLayoutAppointmentStartTime] will have value
            if (dtStartTime.HasValue == false && dtEndTime.HasValue == false && objSelectedInventory.ServiceMinutes > 0 && mSchedulerLayoutAppointmentStartTime > DateTime.Now)
            {
                mPreSelectedStartTime = mSchedulerLayoutAppointmentStartTime;
                mPreSelectedEndTime = mSchedulerLayoutAppointmentStartTime.AddMinutes(objSelectedInventory.ServiceMinutes);
            }

            drowInventory = objSelectedInventory;

            //selectedOrderItem = new OrderItem
            //{
            //    Id = item.MasterAccountID ?? string.Empty,
            //    Name = item.AccountName ?? "",
            //    Price = item.SalesPrice,
            //    Quantity = 1,
            //    DisplayCode = item.DisplayCode ?? string.Empty,
            //    ImagePath = item.ImagePath,
            //    UnitOfMeasurementID = item.UnitOfMeasureID ?? string.Empty,
            //    TaxCodeID = item.TaxCodeID,
            //    IsTaxInclusive = item.IsTaxInclusive,
            //    TaxRate = taxRate
            //};

            editDiscountAmount = "0.00";
            editDiscountPercent = "0.00";
            editRemarks = "";

            await AddSelectedItemToBill_AfterCheckingforBundle(
                objSelectedInventory,
                objSelectedSKU,
                strEmployeeID,
                strSalesPersonCode,
                objUnconsumedItem,
                dtStartTime,
                dtEndTime,
                strSeatNo,
                dclOverrideUnitPrice,
                dclOverrideQuantity,
                strOverrideCustomerName
            );

            if (mobjDoc_CashSales != null && mobjDoc_CashSales.lstDocumentLine.Count > 0)
            {
                selectedOrderItem = mobjDoc_CashSales.lstDocumentLine[0];
                editRemarks = !string.IsNullOrEmpty(selectedOrderItem.RefCompanyName) ? selectedOrderItem.RefCompanyName : (selectedOrderItem.Memo ?? "");
                editDiscountAmount = selectedOrderItem.Discount.ToString("F2");

                commissionRows.Clear();
                foreach (var comm in selectedOrderItem.lstSalesCommissionByDocumentLine)
                {
                    commissionRows.Add(new StaffCommissionRow
                    {
                        StaffId = comm.EmployeeID,
                        StaffName = comm.EmployeeCode,
                        Amount = comm.AllocationAmount,
                        CommissionType = comm.CommissionDetailTypeID == 1 ? "%" : "MYR"
                    });
                }
                while (commissionRows.Count < 3)
                {
                    commissionRows.Add(new StaffCommissionRow());
                }
            }

            mblnAddingNewItemFlag = true;
            isAddingNewItem = false;
            showEditOrderItemModal = true;
            StateHasChanged();
        }

        private async Task AddSelectedItemToBill_AfterCheckingforBundle(InventoryDM objSelectedInventory, Inventory_SKUDM? objSelectedSKU = null, string strEmployeeID = "",
                                                                                    string strSalesPersonCode = "", CashSales_Series_UnconsumedItemDM? objUnconsumedItem = null, DateTime? dtStartTime = null,
                                                                                    DateTime? dtEndTime = null, string strSeatNo = "", decimal dclOverrideUnitPrice = 0, decimal dclOverrideQuantity = 1,
                                                                                    string strOverrideCustomerName = "", bool blnShowCourseOrServiceSelection = true, string strOverrideSalesDescription = "",
                                                                                    string strBatchNo = "", string strSerialNo = "", string strMatrix = "")
        {
            DocumentLineTableDM? drow = null;
            decimal dclQuantity = dclOverrideQuantity;
            decimal dclDefaultUnitPrice = objSelectedInventory.SalesPrice;

            if (dclOverrideUnitPrice > 0)
            {
                dclDefaultUnitPrice = dclOverrideUnitPrice;
            }
            else
            {
                try
                {
                    var effectivePrice = await PriceGroupService.GetEffectiveProductPriceAsync(objSelectedInventory.MasterAccountID, objSelectedInventory.SalesPrice);
                    if (effectivePrice.HasValue && effectivePrice.Value > 0)
                    {
                        dclDefaultUnitPrice = effectivePrice.Value;
                    }
                }
                catch { }
            }

            if (mobjDoc_CashSales == null)
                return;

            if (objSelectedInventory.AvailableDateFrom > mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date || objSelectedInventory.AvailableDateTo < mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date)
            {
                ShowNotification($"Item is only available for order between Date: {objSelectedInventory.AvailableDateFrom:yyyy-MM-dd} and {objSelectedInventory.AvailableDateTo:yyyy-MM-dd}.");
                return;
            }

            if (objSelectedInventory.AvailableTimeFrom > DateTime.Now.TimeOfDay || objSelectedInventory.AvailableTimeTo < DateTime.Now.TimeOfDay)
            {
                ShowNotification($"Item is only available for order between Time: {objSelectedInventory.AvailableTimeFrom:HH:mm} and {objSelectedInventory.AvailableTimeTo:HH:mm}.");
                return;
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && objSelectedInventory.InventoryTypeID == 7)
            {
                ShowNotification("Unable to add [Member Credit] item in [Redemption Mode]. Please switch to [Sales Mode] to add this item.");
                return;
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && objSelectedInventory.InventoryTypeID == 5)
            {
                ShowNotification("Unable to add [Package] item in [Redemption Mode]. Please switch to [Sales Mode] to add this item.");
                return;
            }

            if (objUnconsumedItem != null && mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52)
            {
                dclQuantity = objUnconsumedItem.CurrentRedeemQuantity;
            }

            drow = new DocumentLineTableDM();
            //drow.DocumentLineID
            drow.LineOrder = mobjDoc_CashSales.lstDocumentLine.Count + 1;
            drow.LineItemID = objSelectedInventory.MasterAccountID;
            if (objSelectedInventory.InventoryTypeID == 1)
            {
                drow.InventoryItemAccountID = string.IsNullOrEmpty(objSelectedInventory.StockDeductionSKUID) ? objSelectedInventory.MasterAccountID : objSelectedInventory.StockDeductionSKUID;
            }
            drow.LineItemDisplayCode = objSelectedInventory.DisplayCode;
            drow.Description = string.IsNullOrEmpty(strOverrideSalesDescription) ? objSelectedInventory.SalesDescription : strOverrideSalesDescription;
            drow.eInvoiceClassificationCode = objSelectedInventory.eInvoiceClassificationCode;
            drow.SerialNo = strSerialNo;
            drow.SeatNo = mobjDoc_CashSales.objDoc_CashSales.SeatNo;
            drow.TriggerWholeBillNoCommission = objSelectedInventory.TriggerWholeBillNoCommission;
            if (objSelectedInventory.IsOpenItem && objSelectedInventory.IsOpenItemPopUpDescription)
            {
                // For open item with popup description, use the description from popup input instead of default sales description
                // provide a pop up screen for user to input the description, and then assign the input description to drow.Description before adding to bill
                // When assign the input description to drow.Description, add ** character infront as indication of user input description.
            }

            if (string.IsNullOrEmpty(strBatchNo) == false)
            {
                drow.BatchNo = strBatchNo; //'fill in directly from QRCode if it contains Batch Info
            }
            else
            {
                if (objSelectedInventory.InventoryTypeID == 1 && objSelectedInventory.IsBatchItem)
                {
                    // provide a pop up screen here for user to input batch number;
                }
            }

            drow.FinancialAccountID = AppState?.objDefaultAccountDM?.DefaultCashSalesFinancialAccountID;
            drow.BranchID = mobjDoc_CashSales.objDoc_CashSales.BranchID;
            drow.EditBranchID = mobjDoc_CashSales.objDoc_CashSales.EditBranchID;
            drow.ExchangeRate = mobjDoc_CashSales.objDoc_CashSales.ExchangeRate;
            drow.CurrencyID = mobjDoc_CashSales.objDoc_CashSales.TransactionCurrencyID;
            drow.DocumentID = mobjDoc_CashSales.objDoc_CashSales.DocumentID;
            drow.DocumentLineTypeID = 1;
            drow.OwnerDocumentTypeID = mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID;
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5 && mobjDoc_CashSales.objDoc_CashSales.FinancialDate >= AppState?.CurrentBranch?.GSTStartDate)
            {
                drow.GSTTaxPointDate = mobjDoc_CashSales.objDoc_CashSales.FinancialDate;
                if (AppState.SelectedCustomer != null && string.IsNullOrEmpty(AppState.SelectedCustomer.MasterAccountID) == false && string.IsNullOrEmpty(AppState.SelectedCustomer.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(AppState.SelectedCustomer.TaxCodeID))
                {
                    drow.TaxCodeID = AppState.SelectedCustomer.TaxCodeID;
                }
                else
                {
                    if (string.IsNullOrEmpty(objSelectedInventory.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(objSelectedInventory.TaxCodeID))
                    {
                        drow.TaxCodeID = objSelectedInventory.TaxCodeID;
                    }
                    else
                    {
                        drow.TaxCodeID = AppState.CurrentBranch.DefaultSalesTaxCodeID;
                    }
                }

                if (string.IsNullOrEmpty(drow.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(drow.TaxCodeID))
                {
                    drow.GSTTypeID = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxTypeID;
                    drow.TaxPercentage = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxRate;
                    drow.TaxFinancialAccountID = AppState.lstGSTTaxCode[drow.TaxCodeID].FinancialAccountID;
                }
                drow.IsTaxInclusive = objSelectedInventory.IsTaxInclusive;
            }
            else
            {
                drow.GSTTypeID = "";
                drow.TaxCodeID = "";
                drow.TaxPercentage = 0;
            }
            drow.ItemTaxGroupID = objSelectedInventory.ItemTaxGroupID;
            drow.Quantity = dclQuantity;

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
                drow.ActivityTypeID = 1;
            else
                drow.ActivityTypeID = 6;

            if (string.IsNullOrEmpty(strSeatNo))
                drow.SeatNo = mobjDoc_CashSales.objDoc_CashSales.SeatNo;
            else
                drow.SeatNo = strSeatNo;

            drow.SKUName = objSelectedInventory.UnitOfMeasureID;

            if (objUnconsumedItem != null && mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52)
            {
                drow.UnitPrice = objUnconsumedItem.UnitPrice;
                drow.OriginalKitPrice = objUnconsumedItem.SourceUnitPrice;
                drow.SourceDocumentLineID = objUnconsumedItem.AutoID;
                drow.UnitActualValue = objUnconsumedItem.UnitActualValue;
                drow.SKUName = "";
                drow.SKUQuantity = 1;
                drow.KitMemberID = objUnconsumedItem.PackageID;
                if (objUnconsumedItem.ActivityTypeID == 1) //'if it is package sales, then set the activitytype to "Redemption"
                    drow.ActivityTypeID = 2;
                else if (objUnconsumedItem.ActivityTypeID == 4) //if it is deferred/Prepaid sales, then set the activitytype to "Prepaid Redemption"
                    drow.ActivityTypeID = 5;
                else
                    drow.ActivityTypeID = 2;
            }
            else
            {
                if (objSelectedSKU != null)
                {
                    drow.SKUName = objSelectedSKU.SKUName;
                    drow.SKUQuantity = (objSelectedSKU.SKUQuantity == 0 ? 1 : objSelectedSKU.SKUQuantity) / (objSelectedInventory.UOMBase == 0 ? 1 : objSelectedInventory.UOMBase);
                    drow.UnitPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, objSelectedSKU.SalesPrice);
                }
                else
                {
                    drow.SKUName = objSelectedInventory.UnitOfMeasureID;
                    drow.SKUQuantity = 1;

                    if (objSelectedInventory.InventoryTypeID != 9) //ie. not Bundle
                    {
                        if (dclOverrideUnitPrice != 0)
                            drow.UnitPrice = dclOverrideUnitPrice;
                        else
                            drow.UnitPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, dclDefaultUnitPrice);
                    }
                }
            }
            drow.InventoryTypeID = objSelectedInventory.InventoryTypeID;
            drow.Discount = 0;

            if (AppState?.objDefaultAccountDM?.IsServiceChargeEnabled == true)
            {
                drow.DiningType = "Dine-In";
            }

            if (objSelectedInventory.ServiceMinutes > 0)
            {
                drow.ServiceMinutes = (int)(dclQuantity * objSelectedInventory.ServiceMinutes);
                drow.ServiceBufferMinutes = objSelectedInventory.BufferMinutes;
                if (mobjDoc_CashSales.objDoc_CashSales.FinancialDate > DateTime.Now.Date) //if yes then it is future booking
                {
                    if (SchedulerLayoutAppointmentStartTime.Year > 2020) //to make sure user input valid date
                        drow.ServiceTimeFrom = SchedulerLayoutAppointmentStartTime;
                    else
                        drow.ServiceTimeFrom = new DateTime(mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date.Year, mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date.Month, mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date.Day, mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date.Hour, mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date.Minute, 0);
                }
                else
                {
                    var dtWorkingHourWithoutSeconds = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);
                    drow.ServiceTimeFrom = GetRoundToFiveMinutesTime(dtWorkingHourWithoutSeconds).AddMinutes(objSelectedInventory.BufferMinutes);
                }
                drow.ServiceTimeTo = drow.ServiceTimeFrom.AddMinutes(objSelectedInventory.ServiceMinutes);
            }
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
            {
                if (objSelectedInventory.MasterAccountID == AppState?.objDefaultAccountDM?.DirectCashTopUpDefaultInventoryID)
                    drow.CashTopUpCredit = drow.UnitPrice;
            }
            drow.ClassID = strEmployeeID;
            drow.ClassName = strSalesPersonCode;

            if (objSelectedInventory.IsMedication)
            {
                drow.IsMedication = objSelectedInventory.IsMedication;
                //fill in the doctor prescription
                drow.objDoctorPrescriptionDM.Dosage = objSelectedInventory.DefaultDosage;
                drow.objDoctorPrescriptionDM.DrugDosageUnit = objSelectedInventory.DrugDosageUnitName;
                drow.objDoctorPrescriptionDM.DrugFrequencyName = objSelectedInventory.DrugFrequencyName;
                drow.objDoctorPrescriptionDM.DrugDurationName = objSelectedInventory.DrugDurationName;
                drow.objDoctorPrescriptionDM.DrugPurposeName = objSelectedInventory.DrugReasonName;
                drow.objDoctorPrescriptionDM.DrugInstructionName = objSelectedInventory.DrugInstructionName;
                drow.objDoctorPrescriptionDM.DrugNotes = objSelectedInventory.DrugNotes;
            }

            if (dtStartTime != null && dtEndTime != null)
            {
                drow.ServiceTimeFrom = new DateTime(dtStartTime.Value.Year, dtStartTime.Value.Month, dtStartTime.Value.Day, dtStartTime.Value.Hour, dtStartTime.Value.Minute, 0);
                drow.ServiceTimeTo = new DateTime(dtEndTime.Value.Year, dtEndTime.Value.Month, dtEndTime.Value.Day, dtEndTime.Value.Hour, dtEndTime.Value.Minute, 0);
            }

            if (objSelectedInventory.InventoryTypeID == 3 && (objSelectedInventory.HasUOM | objSelectedInventory.IsUseNonControlledCourse) && blnShowCourseOrServiceSelection == true)
            {
                // To prepare a pop up screen for user to select whether to proceed as service or make it a deferred package
                //refer row 7991 in main program
            }

            if (string.IsNullOrEmpty(objSelectedInventory.CondimentGroupID) == false && drow.ActivityTypeID != 4)
            {
                // To prepare a pop up screen for user to select condiment
                // refer row 8031 in main program
            }

            if (string.IsNullOrEmpty(objSelectedInventory.MatrixGroupID) == false && drow.ActivityTypeID != 4)
            {
                // To prepare a pop up screen for user to select Matrix
                // refer row 8044 in main program
            }

            if (drow.InventoryTypeID == (int)EnumInventoryType.Bundle)
            {
                //If it is a bundled, loop through each bundle item and add to the drow detail-list
                CashSales_PromotionItemDetailsDM objDM;
                decimal dclPromotionLumpSumPrice = 0;

                foreach (var objPackageItem in objSelectedInventory.lstPackage)
                {
                    objDM = new CashSales_PromotionItemDetailsDM();
                    objDM.ActivityTypeID = 1;
                    objDM.PromotionID = objPackageItem.InventoryID;
                    objDM.PromotionDetailID = objPackageItem.AutoID;
                    objDM.HeaderCaption = objPackageItem.HeaderCaption;
                    objDM.Description = objPackageItem.Description;
                    objDM.InventoryID = objPackageItem.InventoryID;
                    objDM.IsVoided = false;
                    objDM.Quantity = objPackageItem.Quantity;
                    var objBundleItem = await InventoryService.LoadItemAsync(objPackageItem.InventoryID);
                    if (objBundleItem != null)
                    {
                        objDM.PromotionUnitOriginalPrice = objBundleItem.SalesPrice;
                        objDM.PrinterName = objBundleItem.PrinterA;
                        objDM.CondimentGroupID = objBundleItem.CondimentGroupID;
                        objDM.MatrixGroupID = objBundleItem.MatrixGroupID;
                    }
                    objDM.PromotionMethod = objPackageItem.PromotionMethod;
                    objDM.PromotionAmountFactor = objPackageItem.UnitPrice;
                    objDM.Memo = "";
                    objDM.OptionItems = objPackageItem.OptionItems;
                    objDM.OptionGroups = objPackageItem.OptionGroups;
                    objDM.OptionBrands = objPackageItem.OptionBrands;
                    objDM.IsConfirmed = objPackageItem.IsConfirmed;
                    objDM.InventoryTypeID = objPackageItem.InventoryTypeID;
                    if (objDM.InventoryID == objDM.OptionItems)
                        objDM.IsConfirmed = true;

                    drow.lstPackageItems.Add(objDM);
                }
                await CalculateTotals();
                drow.lstSalesCommissionByDocumentLine = await ReviseCommission(drow, objSelectedInventory, strEmployeeID, strSalesPersonCode);
                await UpdatePromotionDetailItems(drow, objSelectedInventory);

                if (drow.lstPackageItems.Count > 0)
                {
                    if (drow.lstPackageItems.Any(x => x.IsConfirmed == false && (string.IsNullOrEmpty(x.OptionItems) == false | string.IsNullOrEmpty(x.OptionGroups) == false | string.IsNullOrEmpty(x.OptionBrands) == false)) |
                        drow.lstPackageItems.Any(x => string.IsNullOrWhiteSpace(x.MatrixGroupID) == false && string.IsNullOrEmpty(x.Matrix)) |
                        drow.lstPackageItems.Any(x => x.InventoryTypeID == 3 && (AppState?.lstAllSalesItems.ContainsKey(x.InventoryID) == true && AppState.lstAllSalesItems[x.InventoryID].lstPackage.Any(y => y.IsConfirmed == false && (string.IsNullOrEmpty(y.OptionBrands) == false | string.IsNullOrEmpty(y.OptionGroups) == false | string.IsNullOrEmpty(y.OptionItems) == false)))))
                    {
                        //Provide a pop up screen for bundle selection
                    }
                    else
                    {
                        //loop through every bundled item and if it is service, deduct its material consumption
                        foreach (var objBundledItem in drow.lstPackageItems)
                        {
                            if (objBundledItem.InventoryTypeID == 3 && AppState?.lstAllSalesItems.ContainsKey(objBundledItem.InventoryID) == true)
                            {
                                var objServiceItem = AppState.lstAllSalesItems[objBundledItem.InventoryID];
                                if (objServiceItem.lstPackage != null && objServiceItem.lstPackage.Count > 0)
                                {
                                    foreach (var objMaterial in objServiceItem.lstPackage)
                                    {
                                        CashSales_PromotionItemDetailsDM materialDrow = new CashSales_PromotionItemDetailsDM()
                                        {
                                            ActivityTypeID = 1,
                                            HeaderCaption = objMaterial.HeaderCaption,
                                            Description = objMaterial.Description,
                                            InventoryID = objMaterial.InventoryID,
                                            IsVoided = false,
                                            Quantity = objMaterial.Quantity,
                                            UnitPrice = objMaterial.UnitPrice,
                                            TotalPrice = objMaterial.TotalPrice,
                                            Memo = "",
                                            OptionItems = objMaterial.OptionItems,
                                            OptionGroups = objMaterial.OptionGroups,
                                            OptionBrands = objMaterial.OptionBrands,
                                            IsConfirmed = objMaterial.IsConfirmed,
                                            InventoryTypeID = objMaterial.InventoryTypeID,
                                            CondimentGroupID = AppState?.lstAllSalesItems[objMaterial.InventoryID]?.CondimentGroupID ?? "",
                                            MatrixGroupID = AppState?.lstAllSalesItems[objMaterial.InventoryID]?.MatrixGroupID ?? ""
                                        };
                                        objBundledItem.lstBundleServiceMaterialConsumption.Add(materialDrow);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    //Show Pop Up form to select condiment if any

                    //Show Pop Up form to select Matrix if any
                }

                //finally determine the final bundle price. If the original settings comes with a SalesPrice, means take that price as final price, else sum up the Details TotalPrice to be the final price.
                if (objSelectedInventory.SalesPrice > 0)
                {
                    dclDefaultUnitPrice = objSelectedInventory.SalesPrice;
                }
                else
                {
                    dclPromotionLumpSumPrice = drow.lstPackageItems.Sum(x => x.TotalPrice);
                    dclDefaultUnitPrice = drow.Quantity == 0 ? 0 : dclPromotionLumpSumPrice / drow.Quantity;
                }

                if (dclOverrideUnitPrice != 0)
                    drow.UnitPrice = dclOverrideUnitPrice;
                else
                    drow.UnitPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, dclDefaultUnitPrice);
            }
            else if (drow.InventoryTypeID == (int)EnumInventoryType.Package)
            {
                var objInventoryDM = AppState?.lstAllSalesItems.ContainsKey(drow.LineItemID) == true ? AppState.lstAllSalesItems[drow.LineItemID] : null;
                if (objInventoryDM != null)
                {
                    objInventoryDM.lstMembershipCredit = ParseMembershipCredit(objInventoryDM.MembershipCredit);
                    if (objInventoryDM.lstPackage.Count > 0)
                    {
                        await AddUnconsumedServicesToRow(drow, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                    }
                    if (objInventoryDM.lstMembershipCredit.Count > 0)
                    {
                        await AddMemberCreditToRow(drow, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                    }
                }
            }
            else if (drow.InventoryTypeID == (int)EnumInventoryType.TopUp)
            {
                var objInventoryDM = AppState?.lstAllSalesItems.ContainsKey(drow.LineItemID) == true ? AppState.lstAllSalesItems[drow.LineItemID] : null;
                if (objInventoryDM != null)
                {
                    objInventoryDM.lstMembershipCredit = ParseMembershipCredit(objInventoryDM.MembershipCredit);
                }
                Console.WriteLine($"[DEBUG] AddSelectedItemToBill_AfterCheckingforBundle: Adding TopUp. objInventoryDM is null? {objInventoryDM == null}. lstMembershipCredit count: {objInventoryDM?.lstMembershipCredit?.Count ?? -1}");
                if (objInventoryDM != null && objInventoryDM.lstMembershipCredit.Count > 0)
                {
                    await AddMemberCreditToRow(drow, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }
            }
            else if (drow.InventoryTypeID == (int)EnumInventoryType.Service && drow.ActivityTypeID != (int)EnumActivityType.Prepaid)
            {
                drow.PrinterName = objSelectedInventory.PrinterA;

                if (AppState?.lstAllSalesItems.ContainsKey(objSelectedInventory.MasterAccountID) == true)
                {
                    foreach (var objMaterialItem in AppState.lstAllSalesItems[objSelectedInventory.MasterAccountID].lstPackage)
                    {
                        if (AppState?.lstAllSalesItems.ContainsKey(objMaterialItem.InventoryID) == false)
                        {
                            ShowNotification($"Bundled item - [ID: {objMaterialItem.InventoryID} - {objMaterialItem.Description}] is not opened for branch [{mobjDoc_CashSales.objDoc_CashSales.BranchID}].{Environment.NewLine}{Environment.NewLine}Please contact HQ Administrator.");
                        }

                        var objDeferredService = new CashSales_PromotionItemDetailsDM()
                        {
                            ActivityTypeID = 1,
                            PromotionID = objMaterialItem.InventoryID,
                            PromotionDetailID = objMaterialItem.AutoID,
                            HeaderCaption = objMaterialItem.HeaderCaption,
                            Description = objMaterialItem.Description,
                            InventoryID = objMaterialItem.InventoryID,
                            IsVoided = false,
                            Quantity = objMaterialItem.Quantity * dclQuantity,
                            UnitPrice = 0,
                            TotalPrice = 0,
                            Memo = "",
                            OptionItems = objMaterialItem.OptionItems,
                            OptionGroups = objMaterialItem.OptionGroups,
                            OptionBrands = objMaterialItem.OptionBrands,
                            IsConfirmed = objMaterialItem.IsConfirmed,
                            InventoryTypeID = objMaterialItem.InventoryTypeID,
                            PrinterName = AppState?.lstAllSalesItems.ContainsKey(objMaterialItem.InventoryID) == false ? "" : AppState?.lstAllSalesItems[objMaterialItem.InventoryID].PrinterA,
                            CondimentGroupID = "",
                            MatrixGroupID = AppState?.lstAllSalesItems.ContainsKey(objMaterialItem.InventoryID) == false ? "" : AppState?.lstAllSalesItems[objMaterialItem.InventoryID].MatrixGroupID
                        };
                        drow.lstPackageItems.Add(objDeferredService);
                    }

                    if (drow.lstPackageItems.Count > 0 && (drow.lstPackageItems.Any(x => x.IsConfirmed == false && (string.IsNullOrWhiteSpace(x.OptionItems) == false | string.IsNullOrWhiteSpace(x.OptionGroups) == false | string.IsNullOrWhiteSpace(x.OptionBrands) == false)) | drow.lstPackageItems.Any(x => string.IsNullOrWhiteSpace(x.MatrixGroupID) == false && string.IsNullOrWhiteSpace(x.Matrix) == true)))
                    {
                        //The material consumption contains OPTION ITEMS
                        //Provide a pop up screen for selection of option items
                    }
                }
            }
            else if (drow.InventoryTypeID == (int)EnumInventoryType.Inventory)
            {
                drow.PrinterName = objSelectedInventory.PrinterA;
            }

            //calculate member price before calculate commission
            if (string.IsNullOrWhiteSpace(strOverrideCustomerName) == false && strOverrideCustomerName.ToUpper() == "BUSINESS") //is internal business usage charge out hence no price
            {
                drow.UnitPrice = 0;
            }
            else
            {
                if (objUnconsumedItem == null)
                {
                    await CalculateMemberPrice(drow, "");
                }
            }


            if (AppState?.objDefaultAccountDM?.IsKeepCommissionRecords == true)
            {
                List<SalesCommission_ByDocumentLineDM> lstTemp = new();
                bool blnIsUseCentralisedPresetAllocationFormula = false;

                if (AppState?.objDefaultAccountDM != null && AppState?.objDefaultAccountCentralisedDM != null)
                {
                    var lstExcludedBranches = AppState.objDefaultAccountCentralisedDM.CommissionFormulaControlExclude.Split(",").ToList();
                    if ((AppState.objDefaultAccountCentralisedDM.CommissionFormulaControl == "Standalone" && string.IsNullOrEmpty(AppState.objDefaultAccountCentralisedDM.CommissionFormulaControlExclude) == true) || (AppState.objDefaultAccountCentralisedDM.CommissionFormulaControl == "Standalone" && string.IsNullOrEmpty(AppState.objDefaultAccountCentralisedDM.CommissionFormulaControlExclude) == false && lstExcludedBranches.Contains(mobjDoc_CashSales.objDoc_CashSales.BranchID) == false))
                        blnIsUseCentralisedPresetAllocationFormula = false;
                    else
                        blnIsUseCentralisedPresetAllocationFormula = true;
                }
                else
                {
                    blnIsUseCentralisedPresetAllocationFormula = true;
                }

                //Compute based on Preset Allocation commission formula, not implemented in Senang
                if (blnIsUseCentralisedPresetAllocationFormula == true)
                {

                }
                else
                {

                }

                if (lstTemp.Count > 0)
                {
                    foreach (var objTempComm in lstTemp)
                        drow.lstSalesCommissionByDocumentLine.Add(objTempComm);
                }
            }

            //Check Whether There is sufficient Credit, if not sufficient remove the member discount
            //decimal dclCreditBalanceForSpecificMemberAccount = 0;
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && string.IsNullOrEmpty(drow.MemberCreditAccountID) == false)
            {
                //This part is for the function in POS, to allow customer to pre-select which credit account to redeem, and
                //directly apply the credit as redemption upon item selection. This feature is not implemented in Senang.
                //In Senang, user is required to apply credit to each individual line upon items added to bill.
            }

            if (string.IsNullOrEmpty(objSelectedInventory.HeritageTax) == false)
            {
                if (objSelectedInventory.HeritageTax.Contains("%"))
                {
                    if (decimal.TryParse(objSelectedInventory.HeritageTax.Substring(0, objSelectedInventory.HeritageTax.Length - 1), out decimal dclDiscountPercentage))
                    {
                        drow.HeritageTax = Math.Round(dclDiscountPercentage / 100 * drow.SubTotalBeforeGST * drow.Quantity, 2);
                    }
                }
                else
                {
                    if (decimal.TryParse(objSelectedInventory.HeritageTax.Trim(), out decimal dclDiscountAmount))
                    {
                        drow.HeritageTax = Math.Round(dclDiscountAmount * drow.Quantity, 2);
                    }
                }
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && drow.ActivityTypeID == 6)
            {
                decimal currentCartTotal = mobjDoc_CashSales.lstDocumentLine
                    .Where(x => x.SaveAction != EntityState.Deleted && x.ActivityTypeID == 6)
                    .Sum(x => x.UnitPrice * x.Quantity);
                decimal newItemCost = drow.UnitPrice * drow.Quantity;
                decimal requiredTotal = currentCartTotal + newItemCost;

                decimal selectedCreditTotal = selectedCreditsToRedeem.Sum(x => x.NetBalanceAfterUtilised);

                if (selectedCreditsToRedeem.Any() && selectedCreditTotal < requiredTotal)
                {
                    OpenMultiCreditModal();
                    ShowNotification($"Selected credit amount is insufficient to cover the expenses. Please select additional credit(s) to cover the total of RM {requiredTotal:F2}.");
                    return;
                }
            }

            if (AppState?.objDefaultAccountDM?.CombineIdenticalItem == true && (drow.InventoryTypeID == 1 || drow.InventoryTypeID == 3))
            {
                if (mobjDoc_CashSales.lstDocumentLine.Any(x => x.LineItemID == drow.LineItemID && x.SKUQuantity == drow.SKUQuantity && x.Memo == drow.Memo && x.Matrix == drow.Matrix && x.ClassID == drow.ClassID && x.UnitPrice == drow.UnitPrice))
                {
                    var SelectedDocumentLine = mobjDoc_CashSales.lstDocumentLine.First(x => x.LineItemID == drow.LineItemID && x.SKUQuantity == drow.SKUQuantity && x.Memo == drow.Memo && x.Matrix == drow.Matrix && x.ClassID == drow.ClassID && x.UnitPrice == drow.UnitPrice);
                    SelectedDocumentLine.Quantity += drow.Quantity;
                    await UpdateLineAfterQuantityChanged(SelectedDocumentLine);
                    //add kitchen printing, because if you update the line which the SaveAction is not "Added", kitching printing cannot be triggered.
                    if (AppState?.objDefaultAccountDM.PrintToKitchen == true)
                    {
                        if (SelectedDocumentLine.SaveAction != EntityState.Added && drow.SaveAction == EntityState.Added)
                        {
                            // Kitchen printing not implemented in Senang
                        }
                    }
                }
                else
                {
                    mobjDoc_CashSales.lstDocumentLine.Insert(0, drow);
                }
            }
            else
            {
                mobjDoc_CashSales.lstDocumentLine.Insert(0, drow);
            }

            ////Output to Pole display (not implemented in Senang yet)
            //eSalonPoleDisplay.OutputToPoleDisplay(SelectedDocumentLine.Description, SelectedDocumentLine.Quantity, SelectedDocumentLine.UnitPrice)

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
            {
                //Detect Promotion (not implemented in Senang yet)
            }

            await CalculateTotals();
        }


        private async Task UpdateLineAfterQuantityChanged(DocumentLineTableDM SelectedDocumentLine)
        {
            if (AppState?.lstAllSalesItems.ContainsKey(SelectedDocumentLine.LineItemID) == true)
            {
                SelectedDocumentLine.ServiceMinutes = (int)SelectedDocumentLine.Quantity * AppState.lstAllSalesItems[SelectedDocumentLine.LineItemID].ServiceMinutes;
                SelectedDocumentLine.ServiceBufferMinutes = AppState.lstAllSalesItems[SelectedDocumentLine.LineItemID].ServiceMinutes;
                if (SelectedDocumentLine.ServiceBufferMinutes > 0 && SelectedDocumentLine.ServiceTimeFrom == DateTime.MinValue)
                {
                    SelectedDocumentLine.ServiceTimeFrom = GetRoundToFiveMinutesTime(DateTime.Now).AddMinutes(SelectedDocumentLine.ServiceBufferMinutes);
                }
            }

            if (AppState?.lstAllSalesItems.ContainsKey(SelectedDocumentLine.LineItemID) == true)
            {
                var objInventoryDM = AppState.lstAllSalesItems[SelectedDocumentLine.LineItemID];
                SelectedDocumentLine.lstSalesCommissionByDocumentLine = await ReviseCommission(SelectedDocumentLine, objInventoryDM, SelectedDocumentLine.ClassID, SelectedDocumentLine.SalesPersonCode);
                await UpdateCustomerUnconsumedServicesUponQuantityChanged(SelectedDocumentLine);
                await UpdatePromotionDetailItems(SelectedDocumentLine, objInventoryDM);
                await CalculateMemberPrice(SelectedDocumentLine, SelectedDocumentLine.MemberCreditAccountID);
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
            {
                //await DetectPromotion();
            }

            await CalculateTotals();
        }

        private async Task UpdateCustomerUnconsumedServicesUponQuantityChanged(DocumentLineTableDM SelectedDocumentLine)
        {
            int iFound = 0;

            if (SelectedDocumentLine == null) return;
            if (SelectedDocumentLine.DocumentLineTypeID != 1) return;
            if (string.IsNullOrWhiteSpace(SelectedDocumentLine.KitMemberID) == false) return;
            if (string.IsNullOrWhiteSpace(SelectedDocumentLine.LineItemID) == true) return;
            if (AppState?.lstAllSalesItems == null) return;
            if (AppState?.lstAllSalesItems.ContainsKey(SelectedDocumentLine.LineItemID) == false) return;

            InventoryDM objInventoryDM = AppState?.lstAllSalesItems[SelectedDocumentLine.LineItemID] ?? new InventoryDM();
            if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.Package || objInventoryDM.InventoryTypeID == (int)EnumInventoryType.TopUp)
            {
                objInventoryDM.lstMembershipCredit = ParseMembershipCredit(objInventoryDM.MembershipCredit);
            }

            if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.Service)
            {
                //check to see if the item has been added earlier, if yes, just modify the quantity, if no, add it.
                if (SelectedDocumentLine.lstCashSales_Series_UnconsumedItem.Count > 0)
                {
                    foreach (var objUnconsumedItem in SelectedDocumentLine.lstCashSales_Series_UnconsumedItem)
                    {
                        objUnconsumedItem.QuantityPurchased = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity;
                    }
                }
            }
            else if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.Package)
            {
                iFound = 0;
                foreach (var drow in SelectedDocumentLine.lstCashSales_Series_UnconsumedItem)
                {
                    foreach (var objPackageItem in objInventoryDM.lstPackage)
                    {
                        if (objPackageItem.InventoryID == drow.InventoryID && objPackageItem.AutoID == drow.PackageItemID)
                        {
                            drow.QuantityPurchased = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity * objPackageItem.Quantity;
                            drow.TotalPrice = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity * objPackageItem.Quantity * drow.UnitPrice;
                            drow.TotalActualValue = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity * objPackageItem.Quantity * drow.UnitActualValue;
                            iFound += 1;
                        }
                    }
                }

                if (iFound == 0)
                {
                    await AddUnconsumedServicesToRow(SelectedDocumentLine, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }


                iFound = 0;
                foreach (var drow in SelectedDocumentLine.lstARAPOutstanding_MemberCredit)
                {
                    foreach (var objPackageItem in objInventoryDM.lstMembershipCredit)
                    {
                        if (objPackageItem.MemberTypeID == drow.MemberTypeID)
                        {
                            drow.TotalAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit;
                            drow.InterOutletAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit * objInventoryDM.MemberCreditSettlementRatio;
                            iFound += 1;
                        }
                    }
                }

                if (iFound == 0)
                {
                    await AddMemberCreditToRow(SelectedDocumentLine, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }
            }
            else if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.TopUp)
            {
                iFound = 0;
                foreach (var drow in SelectedDocumentLine.lstARAPOutstanding_MemberCredit)
                {
                    foreach (var objPackageItem in objInventoryDM.lstMembershipCredit)
                    {
                        if (objPackageItem.MemberTypeID == drow.MemberTypeID)
                        {
                            drow.TotalAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit;
                            drow.InterOutletAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit * objInventoryDM.MemberCreditSettlementRatio;
                            iFound += 1;
                        }
                    }
                }

                if (iFound == 0)
                {
                    await AddMemberCreditToRow(SelectedDocumentLine, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }
            }
        }

        private async Task CalculateMemberPrice(DocumentLineTableDM DocLine, string strMembershipCreditAccountID)
        {
            decimal dclMemberDiscount = 0;
            decimal dclOriginalUnitPrice = DocLine.UnitPrice;
            MembershipType objMemberType;
            InventoryDM? objSelectedInventory;
            ARAPOutstanding_MemberCreditDM objARAPOutstanding_MemberCreditDM = null;

            if (string.IsNullOrEmpty(strMembershipCreditAccountID) == false)
            {
                //obtain the full credit account object "ARAPOutstanding_MemberCredit" from api
                //now not yet implement this function in Senang
            }

            if (objARAPOutstanding_MemberCreditDM != null && string.IsNullOrEmpty(objARAPOutstanding_MemberCreditDM.MemberTypeID) == false && objARAPOutstanding_MemberCreditDM.MemberTypeID != "Total Credit")
            {
                // not implement in Senang
            }
            else
            {
                //check for corporate membership
                if (AppState?.SelectedCustomer != null && string.IsNullOrWhiteSpace(AppState?.SelectedCustomer.MembershipTypeID) == false && (AppState?.SelectedCustomer.MembershipValidFrom == DateTime.MinValue | AppState?.SelectedCustomer.MembershipValidFrom <= DateTime.Now.Date) && (AppState?.SelectedCustomer.MembershipValidTo == DateTime.MinValue | AppState?.SelectedCustomer.MembershipValidTo >= DateTime.Now.Date))
                {
                    objMemberType = await MembershipTypeService.LoadRecord(AppState?.SelectedCustomer.MembershipTypeID ?? "-----");

                    if (DateTime.Now.TimeOfDay >= objMemberType.objMembershipType.DiscountTimeFrom && DateTime.Now.TimeOfDay <= objMemberType.objMembershipType.DiscountTimeTo)
                    {
                        objSelectedInventory = AppState?.lstAllSalesItems.ContainsKey(DocLine.LineItemID) == true ? AppState.lstAllSalesItems[DocLine.LineItemID] : null;
                        if (objSelectedInventory != null)
                        {
                            switch (objSelectedInventory.InventoryTypeID)
                            {
                                case (int)EnumInventoryType.Inventory:
                                    dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnInventory;
                                    break;
                                case (int)EnumInventoryType.Service:
                                    dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnServices;
                                    break;
                                case (int)EnumInventoryType.Bundle:
                                    dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnBundle;
                                    break;
                                case (int)EnumInventoryType.Package:
                                    dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnPackage;
                                    break;
                            }

                            if (objMemberType.lstMembershipTypeDiscount.Any(x => x.lstDiscountBrand.Contains(objSelectedInventory.PreferredVendorAccountID) || x.lstDiscountGroup.Contains(objSelectedInventory.ItemGroupID) || x.lstDiscountItems.Contains(objSelectedInventory.MasterAccountID)))
                            {
                                MembershipType_DiscountDM objMemberDiscount = objMemberType.lstMembershipTypeDiscount.First(x => x.lstDiscountBrand.Contains(objSelectedInventory.PreferredVendorAccountID) || x.lstDiscountGroup.Contains(objSelectedInventory.ItemGroupID) || x.lstDiscountItems.Contains(objSelectedInventory.MasterAccountID));
                                dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberDiscount.DiscountPercentage;
                            }
                        }

                        if (dclMemberDiscount > 0 && objMemberType.objMembershipType.IsDiscountLimitToCreditRedemption == false)
                        {
                            DocLine.MemberTypeID = objMemberType.objMembershipType.MemberTypeID;
                            DocLine.MemberCreditAccountID = "";
                            DocLine.MemberDiscount = dclMemberDiscount;
                        }

                    }
                }
            }

            if (string.IsNullOrWhiteSpace(DocLine.CashDiscountID) == false && DocLine.CashDiscountID != "0")
            {
                CashDiscountDM objCashDiscountDM = await cashDiscountService.LoadRecord(DocLine.CashDiscountID);
                if (objCashDiscountDM != null && objCashDiscountDM.IsUseCost == false)
                {
                    if (objCashDiscountDM.CashDiscountTypeID == (int)EnumCashDiscountType.Percentage)
                    {
                        DocLine.Discount = Math.Round((DocLine.Quantity * DocLine.UnitPrice - DocLine.MemberDiscount) * objCashDiscountDM.CashDiscountPercentage, 2);
                    }
                }
            }
        }

        private static System.Collections.ObjectModel.ObservableCollection<Inventory_MembershipCreditDM> ParseMembershipCredit(string? membershipCreditStr)
        {
            var list = new System.Collections.ObjectModel.ObservableCollection<Inventory_MembershipCreditDM>();
            if (string.IsNullOrWhiteSpace(membershipCreditStr)) return list;
            var rows = membershipCreditStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var row in rows)
            {
                var columns = row.Split(',');
                if (columns.Length >= 2)
                {
                    var memberTypeId = columns[0].Trim();
                    if (decimal.TryParse(columns[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var creditAmount))
                    {
                        list.Add(new Inventory_MembershipCreditDM
                        {
                            MemberTypeID = memberTypeId,
                            MemberCredit = creditAmount,
                            SaveAction = EBI.Enum.EntityState.NotChanged,
                            IsDirty = true
                        });
                    }
                }
            }
            return list;
        }

        private static async Task AddMemberCreditToRow(DocumentLineTableDM DocLine, Doc_CashSalesDM mobjCashSalesDM, InventoryDM objPackageOrTopUp)
        {
            Console.WriteLine($"[DEBUG] AddMemberCreditToRow called for Item: {objPackageOrTopUp.AccountName} ({objPackageOrTopUp.MasterAccountID}), InventoryTypeID: {objPackageOrTopUp.InventoryTypeID}");
            Console.WriteLine($"[DEBUG] objPackageOrTopUp.lstMembershipCredit.Count = {objPackageOrTopUp.lstMembershipCredit?.Count ?? 0}");

            if (objPackageOrTopUp.lstMembershipCredit == null || objPackageOrTopUp.lstMembershipCredit.Count == 0)
            {
                Console.WriteLine("[DEBUG] objPackageOrTopUp.lstMembershipCredit is empty or null! No credits will be added.");
            }

            foreach (var rowPackageItem in objPackageOrTopUp.lstMembershipCredit)
            {
                Console.WriteLine($"[DEBUG] Processing credit for MemberTypeID: {rowPackageItem.MemberTypeID}, Credit: {rowPackageItem.MemberCredit}");
                var objMemberCredit = new ARAPOutstanding_MemberCreditDM()
                {
                    AccountID = mobjCashSalesDM.AccountID,
                    FinancialDate = mobjCashSalesDM.FinancialDate,
                    DueDate = objPackageOrTopUp.MemberExpiryDays > 0 ? mobjCashSalesDM.FinancialDate.Date.AddDays(objPackageOrTopUp.MemberExpiryDays) : new DateTime(2049, 12, 31),
                    DocumentID = mobjCashSalesDM.DocumentID,
                    DisplayCode = mobjCashSalesDM.DisplayCode,
                    DocumentTypeID = mobjCashSalesDM.DocumentTypeID,
                    DocumentTypeName = mobjCashSalesDM.FriendlyDocumentName,
                    DocumentLineID = DocLine.DocumentLineID,
                    ItemDescription = $"{DocLine.Description}{(DocLine.Quantity == 1 ? "" : $"(x {DocLine.Quantity})")}",
                    CurrencyID = mobjCashSalesDM.TransactionCurrencyID,
                    CurrencyName = mobjCashSalesDM.TransactionCurrencyName,
                    ExchangeRate = mobjCashSalesDM.ExchangeRate,
                    InterOutletRatio = objPackageOrTopUp.MemberCreditSettlementRatio,
                    InterOutletAmount = rowPackageItem.MemberCredit * DocLine.Quantity * objPackageOrTopUp.MemberCreditSettlementRatio,
                    MGMTier = "",
                    TotalAmount = rowPackageItem.MemberCredit * DocLine.Quantity,
                    BranchID = mobjCashSalesDM.BranchID,
                    GroupID = mobjCashSalesDM.GroupID,
                    LineItemID = DocLine.LineItemID,
                    MemberTypeID = rowPackageItem.MemberTypeID
                };
                DocLine.lstARAPOutstanding_MemberCredit.Add(objMemberCredit);
                Console.WriteLine($"[DEBUG] Successfully added credit record to DocLine: MemberTypeID = {objMemberCredit.MemberTypeID}, TotalAmount = {objMemberCredit.TotalAmount}");
            }
        }

        private static async Task AddUnconsumedServicesToRow(DocumentLineTableDM DocLine, Doc_CashSalesDM mobjCashSalesDM, InventoryDM objPackage)
        {
            foreach (var rowPackageItem in objPackage.lstPackage)
            {
                if (rowPackageItem.PromotionMethod != (int)EnumPromotionMethod.BundledDiscount)
                {
                    var objUnconsumedService = new CashSales_Series_UnconsumedItemDM()
                    {
                        DocumentID = mobjCashSalesDM.DocumentID,
                        CustomerAccountID = mobjCashSalesDM.AccountID,
                        PackageID = rowPackageItem.PackageID,
                        InventoryID = rowPackageItem.InventoryID,
                        Description = rowPackageItem.Description,
                        QuantityPurchased = DocLine.Quantity * DocLine.SKUQuantity * rowPackageItem.Quantity,
                        QuantityRedeemed = 0,
                        UnitPrice = rowPackageItem.UnitPrice,
                        TotalPrice = DocLine.Quantity * DocLine.SKUQuantity * rowPackageItem.TotalPrice,
                        PackageItemID = rowPackageItem.AutoID,
                        DocumentLineID = DocLine.DocumentLineID,
                        EmployeeID = DocLine.ClassID,
                        ActivityTypeID = DocLine.ActivityTypeID,
                        SourceUnitPrice = rowPackageItem.UnitPrice,
                        UnitActualValue = rowPackageItem.UnitActualValue,
                        TotalActualValue = rowPackageItem.TotalActualValue,
                        SourceUnitActualValue = rowPackageItem.UnitActualValue,
                        OptionItems = rowPackageItem.OptionItems,
                        OptionBrands = rowPackageItem.OptionBrands,
                        OptionGroups = rowPackageItem.OptionGroups,
                        ExpiryDate = objPackage.ValidityDays > 0 ? mobjCashSalesDM.FinancialDate.Date.AddDays(objPackage.ValidityDays).AddHours(23).AddMinutes(59) : new DateTime(2049, 12, 31, 23, 59, 0)
                    };

                    if (rowPackageItem.PackageQuantityTypeID == 0) //Service
                        DocLine.lstCashSales_Series_UnconsumedItem.Add(objUnconsumedService);
                    else if (rowPackageItem.PackageQuantityTypeID == 1) //Time
                        DocLine.lstCashSales_UnconsumedTime.Add(objUnconsumedService);
                }
            }
        }

        private async Task UpdatePromotionDetailItems(DocumentLineTableDM Docline, InventoryDM objInventoryDM)
        {
            if (Docline.InventoryTypeID == 9 && objInventoryDM != null && Docline.lstPackageItems.Count > 0) //ie. bundle
            {
                foreach (var drow in Docline.lstPackageItems)
                {
                    if (drow.SaveAction != EntityState.Deleted)
                    {
                        foreach (var drowPackageItems in objInventoryDM.lstPackage)
                        {
                            if (drowPackageItems.AutoID == drow.PromotionDetailID)
                            {
                                drow.Quantity = Docline.Quantity * drowPackageItems.Quantity;
                                // TotalPrice update is done by DM class itself
                            }
                        }
                    }
                }
            }
        }

        private async Task<ObservableCollection<SalesCommission_ByDocumentLineDM>> ReviseCommission(DocumentLineTableDM objDocline, InventoryDM objSelectedInventory, string strEmployeeID, string strSalesPersonCode)
        {
            int i;
            SalesCommission_ByDocumentLineDM objNewComm;
            SalesCommission_ByDocumentLineDM objNewCommLine;

            if (AppState?.objDefaultAccountDM?.IsKeepCommissionRecords == true)
            {
                ObservableCollection<SalesCommission_ByDocumentLineDM> lstTemp = new();
                lstTemp = await ComputeCommission(objDocline, objSelectedInventory, strEmployeeID, strSalesPersonCode, "");

                if (lstTemp.Count > 0)
                {
                    if (objDocline.lstSalesCommissionByDocumentLine.Count > 0)
                    {
                        for (i = objDocline.lstSalesCommissionByDocumentLine.Count - 1; i >= 0; i--)
                        {
                            //Remove or update the current commission when they still exist in the newly calculated commissions
                            if (lstTemp.Any(x => x.PresetAllocationDetailID == objDocline.lstSalesCommissionByDocumentLine[i].PresetAllocationDetailID && x.PromotionDetailAutoID == objDocline.lstSalesCommissionByDocumentLine[i].PromotionDetailAutoID))
                            {
                                objNewComm = lstTemp.First(x => x.PresetAllocationDetailID == objDocline.lstSalesCommissionByDocumentLine[i].PresetAllocationDetailID && x.PromotionDetailAutoID == objDocline.lstSalesCommissionByDocumentLine[i].PromotionDetailAutoID);
                                objDocline.lstSalesCommissionByDocumentLine[i].AllocatedSalesAmount = objNewComm.AllocatedSalesAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].CommissionDetailTypeID = objNewComm.CommissionDetailTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].PresetAllocationDetailID = objNewComm.PresetAllocationDetailID;
                                objDocline.lstSalesCommissionByDocumentLine[i].CommissionSharingPercentage = objNewComm.CommissionSharingPercentage;
                                objDocline.lstSalesCommissionByDocumentLine[i].Notes = objNewComm.Notes;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsAskFor = objNewComm.IsAskFor;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsRepairJob = objNewComm.IsRepairJob;
                                objDocline.lstSalesCommissionByDocumentLine[i].AllocationTypeID = objNewComm.AllocationTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].AllocationAmount = objNewComm.AllocationAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsBalance = objNewComm.IsBalance;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountID = objNewComm.DiscountID;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountAmount = objNewComm.DiscountAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountCommissionEntitlementTypeID = objNewComm.DiscountCommissionEntitlementTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountCommissionEntitlement = objNewComm.DiscountCommissionEntitlement;
                                objDocline.lstSalesCommissionByDocumentLine[i].RepairJobCommissionDeduction = objNewComm.RepairJobCommissionDeduction;
                                objDocline.lstSalesCommissionByDocumentLine[i].DefaultXRange = objNewComm.DefaultXRange;
                                objDocline.lstSalesCommissionByDocumentLine[i].OriginalAmount = objNewComm.OriginalAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].CommissionAutoAllocationGroupID = objNewComm.CommissionAutoAllocationGroupID;
                                objDocline.lstSalesCommissionByDocumentLine[i].InventoryID = objNewComm.InventoryID;
                                objDocline.lstSalesCommissionByDocumentLine[i].ActivityTypeID = objNewComm.ActivityTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].SalesValue = objNewComm.SalesValue;
                                objDocline.lstSalesCommissionByDocumentLine[i].PromotionDetailAutoID = objNewComm.PromotionDetailAutoID;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsNotForEmployee = objNewComm.IsNotForEmployee;
                                objDocline.lstSalesCommissionByDocumentLine[i].LineQuantity = objNewComm.LineQuantity;
                            }
                            else
                            {
                                objDocline.lstSalesCommissionByDocumentLine.Remove(objDocline.lstSalesCommissionByDocumentLine[i]);
                            }
                        }

                        foreach (var obj in lstTemp)
                        {
                            if (objDocline.lstSalesCommissionByDocumentLine.Any(x => x.PresetAllocationDetailID == obj.PresetAllocationDetailID && x.PromotionDetailAutoID == obj.PromotionDetailAutoID) == false)
                            {
                                //if found non-existent new line, add it the the list
                                objNewCommLine = new SalesCommission_ByDocumentLineDM()
                                {
                                    DocumentID = objDocline.DocumentID,
                                    DocumentLineID = objDocline.DocumentLineID,
                                    AllocatedSalesAmount = obj.AllocatedSalesAmount,
                                    CommissionDetailTypeID = obj.CommissionDetailTypeID,
                                    PresetAllocationDetailID = obj.PresetAllocationDetailID,
                                    CommissionSharingPercentage = obj.CommissionSharingPercentage,
                                    Notes = obj.Notes,
                                    IsAskFor = obj.IsAskFor,
                                    IsRepairJob = obj.IsRepairJob,
                                    AllocationTypeID = obj.AllocationTypeID,
                                    AllocationAmount = obj.AllocationAmount,
                                    IsBalance = obj.IsBalance,
                                    DiscountID = obj.DiscountID,
                                    DiscountAmount = obj.DiscountAmount,
                                    DiscountCommissionEntitlementTypeID = obj.DiscountCommissionEntitlementTypeID,
                                    DiscountCommissionEntitlement = obj.DiscountCommissionEntitlement,
                                    RepairJobCommissionDeduction = obj.RepairJobCommissionDeduction,
                                    BranchID = obj.BranchID,
                                    EmployeeCode = obj.EmployeeCode,
                                    DefaultXRange = obj.DefaultXRange,
                                    OriginalAmount = obj.OriginalAmount,
                                    CommissionAutoAllocationGroupID = obj.CommissionAutoAllocationGroupID,
                                    InventoryID = obj.InventoryID,
                                    ActivityTypeID = obj.ActivityTypeID,
                                    SalesValue = obj.SalesValue,
                                    PromotionDetailAutoID = obj.PromotionDetailAutoID,
                                    IsNotForEmployee = obj.IsNotForEmployee,
                                    LineQuantity = obj.LineQuantity
                                };
                                objDocline.lstSalesCommissionByDocumentLine.Add(objNewCommLine);
                            }
                        }
                    }
                }
                else
                {
                    objDocline.lstSalesCommissionByDocumentLine.Clear();
                }
            }

            return objDocline.lstSalesCommissionByDocumentLine;
        }

        private async Task<ObservableCollection<SalesCommission_ByDocumentLineDM>> ComputeCommission(DocumentLineTableDM Docline, InventoryDM objInventoryDM, string strEmployeeID, string strSalesPersonCode, string strPromotionDetailAutoID, string strBranchCommissionGroupID = "")
        {
            //Note: For Senang don't need to handle "Bundle" commission

            ObservableCollection<SalesCommission_ByDocumentLineDM> lst = new();
            decimal dclSalesAmount = Docline.SubTotalBeforeGST;
            SalesCommission_ByDocumentLineDM? objCommLine;

            if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionACourse) == false | string.IsNullOrEmpty(objInventoryDM.StaffCommissionA) == false | string.IsNullOrEmpty(objInventoryDM.StaffCommissionB) == false | string.IsNullOrEmpty(objInventoryDM.StaffCommissionC) == false | string.IsNullOrEmpty(objInventoryDM.StaffCommissionD) == false | string.IsNullOrEmpty(objInventoryDM.CashierCommission) == false)
            {
                if (Docline.ActivityTypeID == 4) //Selling Course
                {
                    //Staff A Commission for Course
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionACourse) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionACourse, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffPackage", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null)
                        {
                            lst.Add(objCommLine);
                        }
                    }
                }
                else if (Docline.ActivityTypeID == 1 | Docline.ActivityTypeID == 2 | Docline.ActivityTypeID == 3 | Docline.ActivityTypeID == 4 | Docline.ActivityTypeID == 6)
                {
                    //Staff A Commission
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionA) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionA, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffA", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null)
                        {
                            lst.Add(objCommLine);
                        }
                    }

                    //Staff B Commission
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionB) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionB, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffB", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null)
                        {
                            lst.Add(objCommLine);
                        }
                    }

                    //Staff C Commission
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionC) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionC, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffC", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null)
                        {
                            lst.Add(objCommLine);
                        }
                    }

                    //Staff D Commission
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionD) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionD, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffD", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null)
                        {
                            lst.Add(objCommLine);
                        }
                    }
                }
            }

            return lst;
        }

        private SalesCommission_ByDocumentLineDM? GetCommissionLineBySimpleFormula(string strSimpleCommissionFormula, int intActivityTypeID, string strInventoryID, decimal dclQuantity, decimal dclSKUQuantity, decimal dclSalesAmount, string strEmployeeID, string strSalesPersonCode, string strPresetAllocationDetailID, string strPromotionDetailAutoID, string strBranchCommissionGroupID = "")
        {
            //Simple Commission Structure
            //example: 10%|CommGroup1:8%|CommGroup2:20%
            //The above will be populated to Dictionary values as below
            // ALL:10%
            //CommGroup1:8%
            //CommGroup2:20%
            //Taking A:10% as example
            //with T infront, ie. T10%, represent Target based commission
            //with F infront, ie. F10%, represent Fixed based commission
            //with A at the back, ie. 10%A, means will lookup the commission based on employee level at a sub-table. This is not implemented in Senang App but just ensure the structure is ready and consistent

            string strCommissionFormulaFromInventory;
            string strStaticCommission;
            string strCommissionAutoAllocationGroupID = ""; //From objEmployeeDM.CommissionAutoAllocationGroupID, but currently not implement the auto allocation function based on employee level in Senang App, so directly set it as empty string and let the system treat it as no "A" scenario for now

            if (string.IsNullOrEmpty(strSimpleCommissionFormula) == false)
            {
                List<string> lstComm = strSimpleCommissionFormula.Split('|').ToList();
                Dictionary<string, string> lstDict = new();

                foreach (var obj in lstComm)
                {
                    if (obj.Contains(":"))
                    {
                        if (lstDict.ContainsKey(obj.Split(":").ToList()[0]) == false)
                        {
                            var lstStr = obj.Split(":");
                            lstDict.Add(lstStr[0], lstStr[1]);
                        }
                    }
                    else
                    {
                        if (lstDict.ContainsKey("ALL") == false)
                        {
                            lstDict.Add("ALL", obj);
                        }
                    }
                }

                if (lstDict.ContainsKey(strBranchCommissionGroupID))
                {
                    strCommissionFormulaFromInventory = lstDict[strBranchCommissionGroupID];
                }
                else if (lstDict.ContainsKey("ALL"))
                {
                    strCommissionFormulaFromInventory = lstDict["ALL"];
                }
                else
                {
                    strCommissionFormulaFromInventory = "";
                }

                if (string.IsNullOrEmpty(strCommissionFormulaFromInventory) == false)
                {
                    strStaticCommission = strCommissionFormulaFromInventory.Replace("T", "").Replace("F", "").Replace("A", "");

                    var drowCommission = new SalesCommission_ByDocumentLineDM();
                    drowCommission.EmployeeID = strEmployeeID;
                    drowCommission.EmployeeCode = strSalesPersonCode;
                    drowCommission.ActivityTypeID = intActivityTypeID;
                    if (strCommissionFormulaFromInventory.StartsWith("T"))
                        drowCommission.CommissionDetailTypeID = 1;
                    else if (strCommissionFormulaFromInventory.StartsWith("F"))
                        drowCommission.CommissionDetailTypeID = 2;
                    else
                        drowCommission.CommissionDetailTypeID = 2;

                    if (strCommissionFormulaFromInventory.EndsWith("A") && string.IsNullOrEmpty(strCommissionAutoAllocationGroupID) == false)
                    {
                        //Insert here the process to handle "A" scenario in future
                        //now directly assume no "A
                        ShowNotification("Commission based on employee level is not supported");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(strStaticCommission) == false)
                        {
                            if (strStaticCommission.EndsWith("%"))
                                drowCommission.AllocatedSalesAmount = Math.Round(dclSalesAmount * Convert.ToDecimal(strStaticCommission.Replace("%", "")) / 100, 2, MidpointRounding.AwayFromZero);
                            else
                                drowCommission.AllocatedSalesAmount = Math.Round(Convert.ToDecimal(strStaticCommission) * dclQuantity * dclSKUQuantity, 2, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            drowCommission.AllocatedSalesAmount = dclSalesAmount;
                        }
                    }
                    drowCommission.CommissionSharingPercentage = 1;
                    drowCommission.IsAskFor = 0;
                    drowCommission.IsRepairJob = false;
                    drowCommission.CommissionAutoAllocationGroupID = "01";
                    drowCommission.PresetAllocationDetailID = strPresetAllocationDetailID;
                    drowCommission.InventoryID = strInventoryID;
                    drowCommission.PromotionDetailAutoID = strPromotionDetailAutoID;
                    drowCommission.IsNotForEmployee = false;

                    return drowCommission;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private void DistributeCreditOffsets()
        {
            if (mobjDoc_CashSales == null) return;

            foreach (var line in mobjDoc_CashSales.lstDocumentLine)
            {
                if (line.ActivityTypeID == 6) // CreditRedemption
                {
                    line.MembershipCredit = "";
                    line.MemberCreditAccountID = "";
                    line.MemberTypeID = "";
                    line.InventoryItemAccountID = "";
                }
            }

            if (!selectedCreditsToRedeem.Any()) return;

            var creditPool = selectedCreditsToRedeem.Select(c => new
            {
                Credit = c,
                RemainingBalance = c.NetBalanceAfterUtilised
            }).ToList();

            var activeRedemptionLines = mobjDoc_CashSales.lstDocumentLine
                .Where(x => x.SaveAction != EntityState.Deleted && x.ActivityTypeID == 6)
                .OrderBy(x => x.LineOrder)
                .ToList();

            foreach (var line in activeRedemptionLines)
            {
                decimal neededAmount = line.UnitPrice * line.Quantity;
                decimal totalAppliedForLine = 0;
                var appliedCreditsForLine = new List<(ARAPOutstanding_MemberCreditDM Credit, decimal AppliedAmount)>();

                for (int i = 0; i < creditPool.Count; i++)
                {
                    var poolItem = creditPool[i];
                    if (poolItem.RemainingBalance > 0 && neededAmount > 0)
                    {
                        decimal applyAmt = Math.Min(neededAmount, poolItem.RemainingBalance);
                        totalAppliedForLine += applyAmt;
                        neededAmount -= applyAmt;

                        appliedCreditsForLine.Add((poolItem.Credit, applyAmt));

                        creditPool[i] = new
                        {
                            Credit = poolItem.Credit,
                            RemainingBalance = poolItem.RemainingBalance - applyAmt
                        };
                    }
                }

                if (appliedCreditsForLine.Count == 1)
                {
                    var applied = appliedCreditsForLine.First();
                    line.MembershipCredit = "";
                    line.MemberCreditAccountID = applied.Credit.ARAPOutstandingID;
                    line.MemberTypeID = applied.Credit.MemberTypeID;
                    line.InventoryItemAccountID = line.LineItemID;
                }
                else if (appliedCreditsForLine.Count > 1)
                {
                    string strAppliedLines = string.Join("|", appliedCreditsForLine.Select(x => $"{x.Credit.MemberTypeID},{x.AppliedAmount}"));
                    line.MemberCreditAccountID = "";
                    line.MembershipCredit = strAppliedLines;
                    line.MemberTypeID = appliedCreditsForLine.First().Credit.MemberTypeID;
                    line.InventoryItemAccountID = line.LineItemID;
                }
            }
        }

        private void EvaluatePromotionsForCart()
        {
            if (mobjDoc_CashSales == null || activePromotions == null || !activePromotions.Any()) return;
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID != 5) return; // Only apply in Sales mode

            var today = DateTime.Today;
            var validPromos = activePromotions
                .Where(p => p.IsActive &&
                            (!p.StartDate.HasValue || p.StartDate.Value.Date <= today) &&
                            (!p.EndDate.HasValue || p.EndDate.Value.Date >= today))
                .ToList();

            foreach (var line in mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted))
            {
                var matchedPromo = validPromos.FirstOrDefault(p =>
                    p.AppliedProductIds != null &&
                    p.AppliedProductIds.Any(id => string.Equals(id?.Trim(), line.LineItemID?.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                    line.Quantity >= Math.Max(1, p.MinQuantity)
                );

                if (matchedPromo != null)
                {
                    decimal discountableQty = line.Quantity;
                    if (matchedPromo.MaxLimitPerOrder > 0)
                    {
                        discountableQty = Math.Min(line.Quantity, (decimal)matchedPromo.MaxLimitPerOrder);
                    }

                    decimal lineDiscount = 0;
                    var method = (matchedPromo.PromoMethod ?? "").Trim();
                    var type = (matchedPromo.PromoType ?? "").Trim();

                    if (method.Equals("Discount %", StringComparison.OrdinalIgnoreCase) ||
                        method.Equals("Percentage", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
                    {
                        lineDiscount = Math.Round((line.UnitPrice * discountableQty) * (matchedPromo.DiscountValue / 100m), 2);
                    }
                    else if (method.Equals("Discount Amt", StringComparison.OrdinalIgnoreCase) ||
                             method.Equals("Discount Amount", StringComparison.OrdinalIgnoreCase) ||
                             type.Equals("Discount Amount", StringComparison.OrdinalIgnoreCase))
                    {
                        lineDiscount = Math.Min(line.UnitPrice * discountableQty, Math.Round(matchedPromo.DiscountValue * discountableQty, 2));
                    }
                    else if (method.Equals("Fixed Unit Price", StringComparison.OrdinalIgnoreCase) ||
                             method.Equals("Fixed Price", StringComparison.OrdinalIgnoreCase) ||
                             type.Equals("Fixed Price", StringComparison.OrdinalIgnoreCase))
                    {
                        if (line.UnitPrice > matchedPromo.DiscountValue)
                        {
                            lineDiscount = Math.Round((line.UnitPrice - matchedPromo.DiscountValue) * discountableQty, 2);
                        }
                    }

                    line.Discount = lineDiscount;
                    line.Memo = matchedPromo.Name;
                }
                else
                {
                    // Clear auto-promo discount if item no longer qualifies
                    if (!string.IsNullOrEmpty(line.Memo) && validPromos.Any(p => p.Name == line.Memo))
                    {
                        line.Discount = 0;
                        line.Memo = "";
                    }
                }

                line.SubTotalBeforeGST = Math.Max(0, (line.UnitPrice * line.Quantity) - line.Discount);
            }
        }

        private async Task CalculateTotals()
        {
            EvaluatePromotionsForCart();

            if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52)
            {
                DistributeCreditOffsets();
                mobjDoc_CashSales.Recalculate();
            }

            List<string> lstDiningType = new();
            InventoryDM objServiceChargeItem = new();
            decimal ServiceCharges = 0;
            DocumentLineTableDM drow;

            if (AppState?.objDefaultAccountDM?.IsServiceChargeEnabled == true && string.IsNullOrEmpty(AppState.objDefaultAccountDM.ServiceChargeAccountID) == false && string.IsNullOrEmpty(AppState.objDefaultAccountDM.ServiceChargeDiningType) == false && AppState.objDefaultAccountDM.ServiceChargePercentage > 0)
            {
                lstDiningType = new List<string>(AppState.objDefaultAccountDM.ServiceChargeDiningType.Split(","));
                ServiceCharges = Math.Round(mobjDoc_CashSales?.lstDocumentLine.Where(c => c.SaveAction != EntityState.Deleted && lstDiningType.Contains(c.DiningType) && c.InventoryTypeID != 7 && c.InventoryTypeID != 5).Sum(x => x.SubTotalBeforeGST) ?? 0, 2);
                if (string.IsNullOrEmpty(mobjDoc_CashSales?.objDoc_CashSales.SeatNo) == false)
                {
                    //load the SeatDM info, and if the seat is marked as [No Service Charge], then set who bill service charge to 0
                }

                if (mobjDoc_CashSales?.lstDocumentLine.Any(x => x.LineItemID == AppState.objDefaultAccountDM.ServiceChargeAccountID) ?? false == true)
                {
                    mobjDoc_CashSales.lstDocumentLine.First(x => x.LineItemID == AppState.objDefaultAccountDM.ServiceChargeAccountID).UnitPrice = ServiceCharges;
                }
                else
                {
                    if (ServiceCharges != 0)
                    {
                        if (AppState.objServiceChargeItem == null)
                        {
                            ShowNotification($"[Service Charge] item with ID of: {AppState.objDefaultAccountDM.ServiceChargeAccountID} not found in system.");
                        }
                        else
                        {
                            drow = new DocumentLineTableDM();
                            drow.DocumentLineID = GetNewDocumentLineID();
                            drow.LineOrder = mobjDoc_CashSales.lstDocumentLine.Count + 1;
                            drow.LineItemID = AppState.objServiceChargeItem.MasterAccountID;
                            drow.LineItemDisplayCode = AppState.objServiceChargeItem.DisplayCode;
                            drow.Description = AppState.objServiceChargeItem.SalesDescription;
                            drow.eInvoiceClassificationCode = AppState.objServiceChargeItem.eInvoiceClassificationCode;
                            drow.FinancialAccountID = AppState.objDefaultAccountDM.DefaultCashSalesFinancialAccountID;
                            drow.BranchID = mobjDoc_CashSales.objDoc_CashSales.BranchID;
                            drow.EditBranchID = mobjDoc_CashSales.objDoc_CashSales.EditBranchID;
                            drow.ExchangeRate = mobjDoc_CashSales.objDoc_CashSales.ExchangeRate;
                            drow.CurrencyID = mobjDoc_CashSales.objDoc_CashSales.TransactionCurrencyID;
                            drow.DocumentID = mobjDoc_CashSales.objDoc_CashSales.DocumentID;
                            drow.DocumentLineTypeID = 1;
                            drow.OwnerDocumentTypeID = mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID;

                            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5 && mobjDoc_CashSales.objDoc_CashSales.FinancialDate >= AppState?.CurrentBranch?.GSTStartDate)
                            {
                                drow.GSTTaxPointDate = mobjDoc_CashSales.objDoc_CashSales.FinancialDate;
                                if (AppState.SelectedCustomer != null && string.IsNullOrEmpty(AppState.SelectedCustomer.MasterAccountID) == false && string.IsNullOrEmpty(AppState.SelectedCustomer.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(AppState.SelectedCustomer.TaxCodeID))
                                {
                                    drow.TaxCodeID = AppState.SelectedCustomer.TaxCodeID;
                                }
                                else
                                {
                                    if (string.IsNullOrEmpty(AppState.objServiceChargeItem.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(AppState.objServiceChargeItem.TaxCodeID))
                                        drow.TaxCodeID = AppState.objServiceChargeItem.TaxCodeID;
                                    else
                                        drow.TaxCodeID = AppState.CurrentBranch.DefaultSalesTaxCodeID;
                                }

                                if (string.IsNullOrEmpty(drow.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(drow.TaxCodeID))
                                {
                                    drow.GSTTypeID = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxTypeID;
                                    drow.TaxPercentage = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxRate;
                                    drow.TaxFinancialAccountID = AppState.lstGSTTaxCode[drow.TaxCodeID].FinancialAccountID;
                                }
                                drow.IsTaxInclusive = AppState.objServiceChargeItem.IsTaxInclusive;
                            }
                            else
                            {
                                drow.TaxCodeID = "";
                                drow.GSTTypeID = "";
                                drow.TaxPercentage = 0;
                            }
                            drow.ItemTaxGroupID = AppState?.objServiceChargeItem.ItemTaxGroupID;
                            drow.Quantity = 1;
                            drow.SKUName = "";
                            drow.SKUQuantity = 1;
                            drow.UnitPrice = ServiceCharges;
                            drow.InventoryTypeID = AppState?.objServiceChargeItem?.InventoryTypeID ?? 3;
                            drow.Discount = 0;
                            drow.ActivityTypeID = 1;
                            drow.GSTTaxPointDate = mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 ? default : mobjDoc_CashSales.objDoc_CashSales.FinancialDate;
                            drow.DiningType = "";

                            mobjDoc_CashSales.lstDocumentLine.Add(drow);
                        }

                    }
                }

            }

            mobjDoc_CashSales?.objDoc_CashSales.TotalBeforeTax_NonServiceCharge = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && x.LineItemID != AppState?.objDefaultAccountDM?.ServiceChargeAccountID).Sum(y => y.SubTotalBeforeGST);
            mobjDoc_CashSales?.objDoc_CashSales.TotalBeforeTax_ServiceCharge = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && x.LineItemID == AppState?.objDefaultAccountDM?.ServiceChargeAccountID).Sum(y => y.SubTotalBeforeGST);
            mobjDoc_CashSales?.objDoc_CashSales.TotalBeforeTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.SubTotalBeforeGST);
            mobjDoc_CashSales?.objDoc_CashSales.HeritageTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.HeritageTax);
            mobjDoc_CashSales?.objDoc_CashSales.TaxAmount = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.TaxAmount);
            if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 5) //ie. Sales
            {
                if (mblnUseRounding == false)
                    mobjDoc_CashSales.objDoc_CashSales.RoundingAmount = 0;
                else
                    mobjDoc_CashSales.objDoc_CashSales.RoundingAmount = GetRoundingAmount_RoundToCent(mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax + mobjDoc_CashSales.objDoc_CashSales.TaxAmount);
            }
            else if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52) //ie. Redemption
            {
                mobjDoc_CashSales.objDoc_CashSales.RoundingAmount = 0;
            }
            mobjDoc_CashSales?.objDoc_CashSales.TotalAfterTax = mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax + mobjDoc_CashSales.objDoc_CashSales.TaxAmount + mobjDoc_CashSales.objDoc_CashSales.TourismTax + mobjDoc_CashSales.objDoc_CashSales.HeritageTax + mobjDoc_CashSales.objDoc_CashSales.RoundingAmount;
            mobjDoc_CashSales?.objDoc_CashSales.LocalTotalBeforeTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedSubTotalBeforeGST);
            mobjDoc_CashSales?.objDoc_CashSales.LocalTaxAmount = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedTaxAmount);
            mobjDoc_CashSales?.objDoc_CashSales.LocalRoundingAmount = Math.Round(mobjDoc_CashSales.objDoc_CashSales.RoundingAmount * mobjDoc_CashSales.objDoc_CashSales.ExchangeRate, 2);
            mobjDoc_CashSales?.objDoc_CashSales.LocalTotalAfterTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedAmount) + mobjDoc_CashSales.objDoc_CashSales.LocalRoundingAmount;
            mobjDoc_CashSales?.objDoc_CashSales.LocalTaxableAmount = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedTaxableAmount);

            if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52) //ie. Redemption
            {
                AddSeriesRedemptionPayment();
                AddTimeRedemptionPayment();
            }

            StateHasChanged();
        }

        private void AddTimeRedemptionPayment()
        {
            decimal dclTimeRedemptionAmount = 0;
            Doc_CashSales_POSReceiptLinesDM objDoc_Redemption_POSReceiptLinesDM;

            dclTimeRedemptionAmount = mobjDoc_CashSales?.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && string.IsNullOrWhiteSpace(x.RedeemedMinutes) == false).Sum(y => y.SubTotalBeforeGST) ?? 0;

            if (dclTimeRedemptionAmount > 0)
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Any(x => x.POSPaymentTypeID == -4) ?? false == true)
                {
                    objDoc_Redemption_POSReceiptLinesDM = mobjDoc_CashSales?.lstReceiptLines.Where(x => x.POSPaymentTypeID == -4).First() ?? new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclTimeRedemptionAmount;
                }
                else
                {
                    objDoc_Redemption_POSReceiptLinesDM = new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.AccountID = mobjDoc_CashSales?.objDoc_CashSales.AccountID;
                    objDoc_Redemption_POSReceiptLinesDM.AccountTypeID = 3;
                    objDoc_Redemption_POSReceiptLinesDM.POSPaymentTypeID = -4; //ie. Time Redemption, refer eSolution.UC Doc_CashSales_POSPaymentLineType for details
                    objDoc_Redemption_POSReceiptLinesDM.Description = "Time Redemption";
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyID = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyID;
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyName = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyName;
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclTimeRedemptionAmount;
                    objDoc_Redemption_POSReceiptLinesDM.ExchangeRate = 1;

                    mobjDoc_CashSales?.lstReceiptLines.Add(objDoc_Redemption_POSReceiptLinesDM);
                }
            }
            else
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Count > 0)
                {
                    for (int i = mobjDoc_CashSales.lstReceiptLines.Count - 1; i >= 0; i += -1)
                    {
                        if (mobjDoc_CashSales.lstReceiptLines[i].POSPaymentTypeID == -4)
                        {
                            mobjDoc_CashSales.lstReceiptLines.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private void AddSeriesRedemptionPayment()
        {
            decimal dclSeriesRedemptionAmount = 0;
            Doc_CashSales_POSReceiptLinesDM objDoc_Redemption_POSReceiptLinesDM;

            dclSeriesRedemptionAmount = mobjDoc_CashSales?.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && string.IsNullOrWhiteSpace(x.KitMemberID) == false).Sum(y => y.SubTotal) ?? 0;

            if (dclSeriesRedemptionAmount > 0)
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Any(x => x.POSPaymentTypeID == -5) ?? false == true)
                {
                    objDoc_Redemption_POSReceiptLinesDM = mobjDoc_CashSales?.lstReceiptLines.Where(x => x.POSPaymentTypeID == -5).First() ?? new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclSeriesRedemptionAmount;
                }
                else
                {
                    objDoc_Redemption_POSReceiptLinesDM = new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.AccountID = mobjDoc_CashSales?.objDoc_CashSales.AccountID;
                    objDoc_Redemption_POSReceiptLinesDM.AccountTypeID = 3;
                    objDoc_Redemption_POSReceiptLinesDM.POSPaymentTypeID = -5; //ie. Series Redemption, refer eSolution.UC Doc_CashSales_POSPaymentLineType for details
                    objDoc_Redemption_POSReceiptLinesDM.Description = "Series Redemption";
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyID = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyID;
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyName = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyName;
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclSeriesRedemptionAmount;
                    objDoc_Redemption_POSReceiptLinesDM.ExchangeRate = 1;

                    mobjDoc_CashSales?.lstReceiptLines.Add(objDoc_Redemption_POSReceiptLinesDM);
                }
            }
            else
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Count > 0)
                {
                    for (int i = mobjDoc_CashSales.lstReceiptLines.Count - 1; i >= 0; i += -1)
                    {
                        if (mobjDoc_CashSales.lstReceiptLines[i].POSPaymentTypeID == -5)
                        {
                            mobjDoc_CashSales.lstReceiptLines.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private decimal GetRoundingAmount_RoundToCent(decimal dclSalesAmount)
        {
            decimal RoundingAdjustment = 0;
            decimal RoundedAmount = 0;

            switch (AppState?.objDefaultAccountCentralisedDM?.RoundingMethod)
            {
                case "10-Cent":
                    RoundedAmount = Math.Round(dclSalesAmount, 1, MidpointRounding.AwayFromZero);
                    break;
                case "Dollar":
                    RoundedAmount = Math.Round(dclSalesAmount, 0, MidpointRounding.AwayFromZero);
                    break;
                default:  //ie. 5 cents rounding
                    RoundedAmount = Math.Round(dclSalesAmount * 20, MidpointRounding.AwayFromZero) / 20;
                    break;
            }
            RoundingAdjustment = RoundedAmount - Math.Round(dclSalesAmount, 2);
            return RoundingAdjustment;
        }


        private string GetNewDocumentLineID()
        {
            string mstr = mintNewDocumentLineID.ToString("D2");
            mintNewDocumentLineID += 1;
            return mstr;
        }

        private DateTime GetRoundToFiveMinutesTime(DateTime dtDate)
        {
            DateTime dt;
            int intMinutes = 0;

            dt = new DateTime(dtDate.Year, dtDate.Month, dtDate.Day, dtDate.Hour, dtDate.Minute, 0);
            intMinutes = dtDate.Minute % 5;

            switch (intMinutes)
            {
                case 1:
                    dt = dtDate.AddMinutes(4);
                    break;
                case 2:
                    dt = dtDate.AddMinutes(3);
                    break;
                case 3:
                    dt = dtDate.AddMinutes(2);
                    break;
                case 4:
                    dt = dtDate.AddMinutes(1);
                    break;
            }
            return dt;
        }

        private async Task<decimal> GetPriceAfterPriceGroup(string strInventoryID, decimal dclUnitPrice)
        {
            InventoryDM? objSelectedInventory;
            Inventory_PriceGroupDM drowSelectedInventoryPriceGroup;
            decimal dclUnitPriceAfterPriceGroup;

            dclUnitPriceAfterPriceGroup = dclUnitPrice;

            // Check Price Group service for setup override
            try
            {
                var assignedPg = await PriceGroupService.GetPriceGroupByProductIdAsync(strInventoryID);
                if (assignedPg != null && assignedPg.Price > 0)
                {
                    dclUnitPriceAfterPriceGroup = assignedPg.Price;
                }
            }
            catch { }

            if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52 && AppState?.objDefaultAccountCentralisedDM?.ApplyPriceGroupOnRedemption == false)
            {
                return dclUnitPriceAfterPriceGroup;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(mobjDoc_CashSales?.objDoc_CashSales.AccountID) == false && mobjDoc_CashSales.objDoc_CashSales.AccountID != "0")
                {
                    objSelectedInventory = await InventoryService.LoadItemAsync(strInventoryID);
                    if (objSelectedInventory == null)
                        return dclUnitPriceAfterPriceGroup;

                    var strPriceGroupID = AppState?.SelectedCustomer?.PriceGroupID;
                    if (string.IsNullOrEmpty(strPriceGroupID) || strPriceGroupID == "0")
                        return dclUnitPriceAfterPriceGroup;

                    if (objSelectedInventory.lstPriceGroup.ContainsKey(strPriceGroupID))
                    {
                        drowSelectedInventoryPriceGroup = objSelectedInventory.lstPriceGroup[strPriceGroupID];
                        dclUnitPriceAfterPriceGroup = dclUnitPriceAfterPriceGroup * (1 - drowSelectedInventoryPriceGroup.PreDiscount);

                        if (drowSelectedInventoryPriceGroup.FixedPrice > 0)
                        {
                            dclUnitPriceAfterPriceGroup = drowSelectedInventoryPriceGroup.FixedPrice;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(drowSelectedInventoryPriceGroup.BasisID))
                                return dclUnitPriceAfterPriceGroup;

                            switch (drowSelectedInventoryPriceGroup.BasisID)
                            {
                                case "RP":
                                    dclUnitPriceAfterPriceGroup = Math.Round(dclUnitPriceAfterPriceGroup - (objSelectedInventory.SalesPrice * drowSelectedInventoryPriceGroup.DiscountFactor) + drowSelectedInventoryPriceGroup.PriceFactor, 2, MidpointRounding.AwayFromZero);
                                    break;
                                case "NET":
                                    dclUnitPriceAfterPriceGroup = Math.Round(dclUnitPriceAfterPriceGroup * (1 - drowSelectedInventoryPriceGroup.DiscountFactor) + drowSelectedInventoryPriceGroup.PriceFactor, 2, MidpointRounding.AwayFromZero);
                                    break;
                                case "PV":
                                    dclUnitPriceAfterPriceGroup = Math.Round(dclUnitPriceAfterPriceGroup - (objSelectedInventory.PVValue * drowSelectedInventoryPriceGroup.DiscountFactor) + drowSelectedInventoryPriceGroup.PriceFactor, 2, MidpointRounding.AwayFromZero);
                                    break;
                            }
                        }
                    }
                }
            }
            return dclUnitPriceAfterPriceGroup;
        }

        //private async Task AddToOrder(InventoryDM item)
        //{
        //    if (currentOrder == null) return;

        //    if (item.InventoryTypeID == 5 || item.InventoryTypeID == 7)
        //    {
        //        if (currentOrder.Customer == null || string.IsNullOrWhiteSpace(currentOrder.Customer.Id))
        //        {
        //            var typeName = item.InventoryTypeID == 5 ? "Package" : "Top Up";
        //            ShowNotification($"Please select a customer first before adding a {typeName} to the cart.");
        //            return;
        //        }
        //    }

        //    var taxRate = await GstTaxRateService.GetRateForTaxCodeAsync(item.TaxCodeID);

        //    var newItem = new OrderItem
        //    {
        //        Id = item.MasterAccountID ?? string.Empty,
        //        Name = item.AccountName ?? "",
        //        InventoryTypeID = item.InventoryTypeID,
        //        Price = item.SalesPrice,
        //        Quantity = 1m,
        //        DisplayCode = item.DisplayCode ?? string.Empty,
        //        ImagePath = item.ImagePath,
        //        UnitOfMeasurementID = item.UnitOfMeasureID ?? string.Empty,
        //        TaxCodeID = item.TaxCodeID,
        //        IsTaxInclusive = item.IsTaxInclusive,
        //        TaxRate = taxRate,
        //        IsRedemption = false
        //    };

        //    isAddingNewItem = true;

        //    OpenEditOrderItemModal(newItem, isNew: true);
        //    StateHasChanged();
        //}

        private Doc_CashSales? currentOrder;
        private int currentSection = 1;
        private bool showOrdersModal = false;
        private int? selectedOrderId = null;
        private bool isBarcodePopupOpen = false;
        private bool isScanning = false;
        private string _barcode = "";

        private bool showBalanceModal = false;
        private bool showVoucherModal = false;
        private bool showAddMemberModal = false;
        private bool showFormSettingsModal = false;
        private string newName = "", newPhone = "", newEmail = "", newIC = "", newEthnic = "", newMarital = "", newSource = "", newTier = "", newTags = "";
        private DateTime? newBirthday = null;
        private string newGender = "";
        private string remarks = "";

        // Edit Order Item properties
        private bool showEditOrderItemModal = false;
        private DocumentLineTableDM? selectedOrderItem = null;
        private bool mblnAddingNewItemFlag = false;
        private string editDiscountPercent = "0.00";
        private string editDiscountAmount = "0.00";
        private string editRemarks = "";

        private bool showNumpadModal = false;
        private string currentEditField = "";
        private string currentEditValue = "";
        private bool isFreshInput;

        private class FormFieldSettings
        {
            public bool ShowMobile { get; set; } = true;
            public bool ShowName { get; set; } = true;
            public bool ShowBirthday { get; set; } = true;
            public bool ShowGender { get; set; } = false;
            public bool ShowEmail { get; set; } = false;
            public bool ShowIC { get; set; } = false;
            public bool ShowEthnic { get; set; } = false;
            public bool ShowMarital { get; set; } = false;
            public bool ShowSource { get; set; } = false;
            public bool ShowTier { get; set; } = false;
            public bool ShowTag { get; set; } = false;
        }

        private FormFieldSettings formSettings = new();
        private FormFieldSettings tempSettings = new();

        private void GoBack()
        {
            var returnUrl = Nav.Uri.Contains("returnTo=admin") ? "/admin" : "/home";
            Nav.NavigateTo(returnUrl);
        }

        private List<Doc_CashSales> heldOrders = new();
        private string? activeOrderId = null; // To track if we are editing an existing held bill

        // Logic to retrieve and Continue a held bill
        private void ResumeOrder(Doc_CashSales order)
        {
            currentOrder = order;
            mobjDoc_CashSales = currentOrder;
            activeOrderId = order.objDoc_CashSales.DocumentID;
            isSalesMode = order.objDoc_CashSales.DocumentTypeID == 5;

            if (!string.IsNullOrEmpty(order.objDoc_CashSales.AccountID))
            {
                AppState.SelectedCustomer = new CustomerDM
                {
                    MasterAccountID = order.objDoc_CashSales.AccountID,
                    AccountName = order.objDoc_CashSales.AccountName,
                    Phone = order.objDoc_CashSales.Phone
                };
                _ = LoadCustomerPackageBalance();
                _ = LoadCustomerWalletDetails();
                _ = LoadSummaryBalances();
            }
            else
            {
                AppState.SelectedCustomer = null;
                customerPackages.Clear();
                customerCreditDetails.Clear();
                balanceSummary = null;
                otherBalanceSummary = null;
            }
            _ = JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", order.objDoc_CashSales.DocumentID);
            showOrdersModal = false;
            StateHasChanged();
        }

        private const string StorageKey = "held_bills_cache";

        private async Task LoadHeldBillsFromStorage()
        {
            try
            {
                var json = await JS.InvokeAsync<string>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrEmpty(json))
                {
                    heldOrders = JsonSerializer.Deserialize<List<Doc_CashSales>>(json) ?? new List<Doc_CashSales>();
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException)
            {
                // Ignored when circuit is disconnecting
            }
            catch (Exception ex) { Console.WriteLine($"Error loading held bills: {ex.Message}"); }
        }

        private async Task SaveHeldBillsToStorage()
        {
            try
            {
                var json = JsonSerializer.Serialize(heldOrders);
                await JS.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException)
            {
                // Ignored when circuit is disconnecting
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving held bills: {ex.Message}");
            }
        }

        private void OpenBalanceModal(bool isPoints)
        {
            stagedCreditToRedeem = selectedCreditToRedeem;
            selectedCreditArapID = selectedCreditToRedeem?.ARAPOutstandingID;
            if (selectedCreditToRedeem != null)
            {
                _ = FetchCreditHistory(selectedCreditToRedeem.ARAPOutstandingID);
            }
            else
            {
                creditRedemptionHistory.Clear();
            }
            showBalanceModal = true;
        }

        private void OpenPackageModal()
        {
            showVoucherModal = true;
        }

        private string CleanErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "Unknown error";
            
            int jsonStartIndex = message.IndexOf('{');
            if (jsonStartIndex >= 0)
            {
                string jsonPart = message[jsonStartIndex..];
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonPart);
                    if (doc.RootElement.TryGetProperty("Title", out var titleProp))
                    {
                        return titleProp.GetString() ?? message;
                    }
                    if (doc.RootElement.TryGetProperty("title", out var titlePropLower))
                    {
                        return titlePropLower.GetString() ?? message;
                    }
                }
                catch
                {
                    // Fall-safe: return original message
                }
            }
            return message;
        }

        private void ShowNotification(string message)
        {
            ShowAlertDialog("Notification", CleanErrorMessage(message));
        }

        private void OpenAddMemberModal()
        {
            newName = newPhone = newEmail = newIC = newEthnic = newMarital = newSource = newTier = newTags = "";
            newBirthday = null;
            newGender = "";
            showAddMemberModal = true;
            showSelectCustomerModal = false;
        }

        private void CloseAddMemberModal() => showAddMemberModal = false;

        private void OpenFormSettings()
        {
            tempSettings = new FormFieldSettings
            {
                ShowMobile = formSettings.ShowMobile,
                ShowName = formSettings.ShowName,
                ShowBirthday = formSettings.ShowBirthday,
                ShowGender = formSettings.ShowGender,
                ShowEmail = formSettings.ShowEmail,
                ShowIC = formSettings.ShowIC,
                ShowEthnic = formSettings.ShowEthnic,
                ShowMarital = formSettings.ShowMarital,
                ShowSource = formSettings.ShowSource,
                ShowTier = formSettings.ShowTier,
                ShowTag = formSettings.ShowTag
            };

            showFormSettingsModal = true;
        }

        private async Task CloseFormSettingsModal()
        {
            showFormSettingsModal = false;
        }

        private async Task SaveFormSettings()
        {
            formSettings = tempSettings;

            await SaveSettingsToStorage();

            showFormSettingsModal = false;
            StateHasChanged();
        }

        private async Task SaveSettingsToStorage()
        {
            try
            {
                var json = JsonSerializer.Serialize(formSettings);
                await JS.InvokeVoidAsync("localStorage.setItem", SettingsKey, json);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private string SettingsKey = "memberFormSettings";

        private bool isSaving = false;
        private async Task SaveNewMember()
        {
            if (string.IsNullOrWhiteSpace(newName)) { ShowNotification("Name is required."); return; }
            if (string.IsNullOrWhiteSpace(newPhone)) { ShowNotification("Mobile is required."); return; }

            if (newBirthday.HasValue && newBirthday.Value.Date > DateTime.Today)
            {
                ShowNotification("Birthdate cannot be a future date.");
                return;
            }

            if (newPhone.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                ShowNotification("Phone number cannot contain English letters.");
                return;
            }

            isSaving = true;

            var request = new CustomerDM
            {
                MasterAccountID = "string",
                //VisibleToBranch = "ALL",
                // UI FIELDS
                AccountName = newName?.Trim() ?? "",
                Phone = newPhone?.Trim() ?? "",
                BirthdayDay = newBirthday?.Day ?? 0,
                BirthdayMonth = newBirthday?.Month ?? 0,
                BirthdayYear = newBirthday?.Year ?? 0,
                Gender = newGender,
                Email = newEmail?.Trim() ?? "",
                NRIC = newIC?.Trim() ?? "",
                RaceName = newEthnic?.Trim() ?? "",
                MaritalStatus = newMarital ?? "",
                CustomerSourceName = newSource?.Trim() ?? "",
                MembershipTypeName = newTier
            };

            // 3. Call Service
            var response = await _customerService.CreateCustomer(request);

            if (response != null && response.statusCode == 200)
            {
                //AppState.SelectedCustomer = request;
                //currentOrder ??= new Order { Items = new List<OrderItem>() };
                //currentOrder.Customer = new OrderCustomer
                //{
                //    Id = request.MasterAccountID,
                //    Name = request.AccountName,
                //    Phone = request.Phone
                //};
                // SUCCESS
                ShowNotification("Customer Created Successfully");
                NotificationSvc.Add(new AppNotification
                {
                    Icon = "👤",
                    TitleKey = "NotifNewMemberTitle",
                    MessageKey = "NotifNewMemberMsg",
                    MessageParam = request.AccountName
                });
                await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
                CloseAddMemberModal();
                ResetForm();
            }
            else
            {
                var resultMsg = response?.message?.Trim().Replace(" ", "").ToLower() ?? "";

                var nameInError = response?.message?.Split("with").Last().Trim() ?? "";

                if (resultMsg.Contains("phonenumber"))
                {
                    ShowNotification($"Phone Number [{newPhone}] has already been registered.");
                }
                else if (resultMsg.Contains("nric"))
                {
                    ShowNotification($"Duplicate NRIC detected with {nameInError}");
                }
                else
                {
                    ShowNotification(response?.message ?? "Error creating customer");
                }
            }

            isSaving = false;
        }
        private void ResetForm()
        {
            newName = "";
            newPhone = "";
            newEmail = "";
            newIC = "";
            newBirthday = null;
            newGender = "";
            newMarital = "";
            newSource = "";
            newTier = "";
        }

        private decimal amountToPay = 0;
        private string selectedAmountType = "fixed";

        private async Task ProceedToPayment()
        {
            if (currentOrder == null || currentOrder.lstDocumentLine.Count == 0) return;

            await CalculateTotals();

            if (currentOrder.objDoc_CashSales.DocumentTypeID == 52)
            {
                decimal totalCartPrice = currentOrder.lstDocumentLine
                    .Where(x => x.SaveAction != EntityState.Deleted && x.ActivityTypeID == 6)
                    .Sum(x => x.UnitPrice * x.Quantity);

                if (totalCartPrice > 0)
                {
                    decimal selectedCreditTotal = selectedCreditsToRedeem.Sum(x => x.NetBalanceAfterUtilised);

                    if (selectedCreditTotal < totalCartPrice)
                    {
                        OpenMultiCreditModal();
                        ShowNotification($"Selected credit amount is insufficient to cover the total expenses. Please select additional credit(s) to cover the total of RM {totalCartPrice:F2} before proceeding.");
                        return;
                    }
                }
            }

            AppState.IsOutstandingPaymentMode = false;
            AppState.OutstandingPaymentAmount = 0m;
            AppState.CurrentOrder = currentOrder;
            await AutoHoldSync();
            Nav.NavigateTo("/cartpayment");
        }

        private async Task CleanupProcessedOrder(string orderId)
        {
            try
            {
                await JS.InvokeVoidAsync("localStorage.removeItem", "active_order_id");
                var json = await JS.InvokeAsync<string>("localStorage.getItem", "held_bills_cache");
                if (!string.IsNullOrEmpty(json))
                {
                    var heldOrdersList = JsonSerializer.Deserialize<List<Doc_CashSales>>(json);
                    if (heldOrdersList != null)
                    {
                        heldOrdersList.RemoveAll(o => o.objDoc_CashSales.DocumentID == orderId);
                        await JS.InvokeVoidAsync("localStorage.setItem", "held_bills_cache", JsonSerializer.Serialize(heldOrdersList));
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private string GetRoundingDisplay(Doc_CashSales? order)
        {
            if (order?.objDoc_CashSales == null) return "RM 0.00";
            var r = order.objDoc_CashSales.RoundingAmount;
            if (r == 0) return "RM 0.00";
            var sign = r < 0 ? "- " : "+ ";
            return $"{sign}RM {Math.Abs(r):F2}";
        }

        private async Task DeleteHeldBill(Doc_CashSales order)
        {
            heldOrders.Remove(order);
            await SaveHeldBillsToStorage();

            if (activeOrderId == order.objDoc_CashSales.DocumentID)
            {
                StartNewSale();
            }
            StateHasChanged();
        }

        private bool showDeleteConfirmModal = false;
        private Doc_CashSales? orderToDelete = null;

        private void ConfirmDeleteHeldBill(Doc_CashSales order)
        {
            orderToDelete = order;
            showDeleteConfirmModal = true;
        }

        private bool showAlertDialogModal = false;
        private string alertDialogTitle = "";
        private string alertDialogMessage = "";

        private void ShowAlertDialog(string title, string message)
        {
            alertDialogTitle = title;
            alertDialogMessage = message;
            showAlertDialogModal = true;
            StateHasChanged();
        }

        private void CancelDeleteHeldBill()
        {
            orderToDelete = null;
            showDeleteConfirmModal = false;
        }

        private async Task ExecuteDeleteHeldBill()
        {
            if (orderToDelete != null)
            {
                await DeleteHeldBill(orderToDelete);
                orderToDelete = null;
            }
            showDeleteConfirmModal = false;
        }

        // Helper to sync the current cart into the held list and local storage
        private async Task AutoHoldSync()
        {
            try
            {
                if (currentOrder == null || !currentOrder.lstDocumentLine.Any()) return;

                var index = heldOrders.FindIndex(o => o.objDoc_CashSales.DocumentID == currentOrder.objDoc_CashSales.DocumentID);

                if (index != -1)
                    heldOrders[index] = currentOrder;
                else
                    heldOrders.Add(currentOrder);

                await SaveHeldBillsToStorage();
                await JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", currentOrder.objDoc_CashSales.DocumentID);
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException)
            {
                // Ignored when circuit is disconnecting
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            try
            {
                if (firstRender)
                {
                    await LoadHeldBillsFromStorage();

                    Doc_CashSales? resumed = null;

                    if (AppState.SelectedCustomer != null)
                    {
                        resumed = heldOrders.FirstOrDefault(o => o.objDoc_CashSales.AccountID == AppState.SelectedCustomer.MasterAccountID);
                        if (resumed == null)
                        {
                            resumed = new Doc_CashSales(5, Guid.NewGuid().ToString());
                            resumed.objDoc_CashSales.AccountID = AppState.SelectedCustomer.MasterAccountID ?? string.Empty;
                            heldOrders.Add(resumed);
                        }
                        resumed.objDoc_CashSales.AccountName = AppState.SelectedCustomer.AccountName ?? "";
                        resumed.objDoc_CashSales.Phone = AppState.SelectedCustomer.Phone ?? "";
                        await SaveHeldBillsToStorage();
                    }
                    else if (AppState.CurrentOrder != null)
                    {
                        resumed = AppState.CurrentOrder;
                        AppState.CurrentOrder = null; // Consume it
                        await JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", resumed.objDoc_CashSales.DocumentID);
                        if (!heldOrders.Any(o => o.objDoc_CashSales.DocumentID == resumed.objDoc_CashSales.DocumentID))
                        {
                            heldOrders.Add(resumed);
                            await SaveHeldBillsToStorage();
                        }
                    }
                    else
                    {
                        var lastOrderId = await JS.InvokeAsync<string?>("localStorage.getItem", "active_order_id");
                        if (!string.IsNullOrEmpty(lastOrderId))
                        {
                            resumed = heldOrders.FirstOrDefault(o => o.objDoc_CashSales.DocumentID == lastOrderId);
                        }
                    }

                    if (resumed != null)
                    {
                        currentOrder = resumed;
                        mobjDoc_CashSales = currentOrder;
                        activeOrderId = resumed.objDoc_CashSales.DocumentID;
                        isSalesMode = resumed.objDoc_CashSales.DocumentTypeID == 5;

                        await JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", resumed.objDoc_CashSales.DocumentID);

                        if (!string.IsNullOrEmpty(resumed.objDoc_CashSales.AccountID))
                        {
                            try
                            {
                                var fullCustomer = await _customerService.GetSingleCustomer(resumed.objDoc_CashSales.AccountID);
                                if (fullCustomer != null && !string.IsNullOrEmpty(fullCustomer.MasterAccountID))
                                {
                                    AppState.SelectedCustomer = fullCustomer;
                                    resumed.objDoc_CashSales.AccountName = fullCustomer.AccountName ?? "";
                                    resumed.objDoc_CashSales.Phone = fullCustomer.Phone ?? "";
                                }
                                else
                                {
                                    AppState.SelectedCustomer = new CustomerDM
                                    {
                                        MasterAccountID = resumed.objDoc_CashSales.AccountID,
                                        AccountName = resumed.objDoc_CashSales.AccountName,
                                        Phone = resumed.objDoc_CashSales.Phone
                                    };
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error loading customer in OnAfterRenderAsync: {ex.Message}");
                                AppState.SelectedCustomer = new CustomerDM
                                {
                                    MasterAccountID = resumed.objDoc_CashSales.AccountID,
                                    AccountName = resumed.objDoc_CashSales.AccountName,
                                    Phone = resumed.objDoc_CashSales.Phone
                                };
                            }
                        }
                        else
                        {
                            AppState.SelectedCustomer = null;
                            customerPackages.Clear();
                            customerCreditDetails.Clear();
                            balanceSummary = null;
                            otherBalanceSummary = null;
                            selectedCreditToRedeem = null;
                            stagedCreditToRedeem = null;
                            selectedCreditsToRedeem.Clear();
                        }
                    }
                    else if (currentOrder == null || !currentOrder.lstDocumentLine.Any())
                    {
                        StartNewSale();
                    }

                    if (currentOrder?.objDoc_CashSales != null && !string.IsNullOrEmpty(currentOrder.objDoc_CashSales.AccountID))
                    {
                        await LoadCustomerPackageBalance();
                        await LoadCustomerWalletDetails();
                        await LoadSummaryBalances();
                    }
                    StateHasChanged();
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException)
            {
                // Ignored when circuit is disconnecting
            }
        }

        private void RemoveItem(DocumentLineTableDM item)
        {
            if (item != null && currentOrder?.lstDocumentLine.Contains(item) == true)
            {
                currentOrder.lstDocumentLine.Remove(item);
                currentOrder.Recalculate();
                StateHasChanged();
            }
        }

        private void StartNewSale()
        {
            currentOrder = new Doc_CashSales(5, Guid.NewGuid().ToString());
            mobjDoc_CashSales = currentOrder;
            isSalesMode = true;
            AppState.SelectedCustomer = null;
            activeOrderId = null;
            balanceSummary = null;
            otherBalanceSummary = null;
            selectedCreditToRedeem = null;
            stagedCreditToRedeem = null;
            selectedCreditsToRedeem.Clear();
            customerPackages.Clear();
            customerCreditDetails.Clear();

            _ = JS.InvokeVoidAsync("localStorage.removeItem", "active_order_id");
            StateHasChanged();
        }

        // Edit Order Item Methods
        private bool editOpenedFromMobileCart = false;

        private void OpenEditOrderItemModal(DocumentLineTableDM item, bool fromMobileCart = false, bool isNew = false)
        {
            editOpenedFromMobileCart = fromMobileCart;
            selectedOrderItem = item;
            isAddingNewItem = isNew;

            editDiscountAmount = item.Discount > 0 ? item.Discount.ToString("F2", CultureInfo.InvariantCulture) : "0.00";
            editDiscountPercent = "0.00";
            editRemarks = !string.IsNullOrEmpty(item.RefCompanyName) ? item.RefCompanyName : (item.Memo ?? "");
            
            // 2. Map structural commission type tracking (fallback to default "%")
            commissionType = isNew ? "" : "%";

            // 3. Map the live ObservableCollection from the database to UI table rows
            if (item.lstSalesCommissionByDocumentLine != null && item.lstSalesCommissionByDocumentLine.Any())
            {
                commissionRows = item.lstSalesCommissionByDocumentLine.Select(c => new StaffCommissionRow
                {
                    StaffId = c.EmployeeID,
                    StaffName = c.EmployeeCode,
                    Amount = c.AllocationAmount,
                    CommissionType = c.CommissionDetailTypeID == 1 ? "%" : "MYR"
                }).ToList();

                // Ensure there are always 3 structural slots visible on screen
                while (commissionRows.Count < 3)
                {
                    commissionRows.Add(new StaffCommissionRow());
                }
            }
            else
            {
                commissionRows = new List<StaffCommissionRow>
        {
            new(), new(), new()
        };
            }

            bool isRedemptionModeLine = (item.ActivityTypeID == 2 || item.ActivityTypeID == 5);
            if (isRedemptionModeLine && !string.IsNullOrEmpty(item.SourceDocumentLineID))
            {
                selectedPackageToRedeem = customerPackages.FirstOrDefault(p => p.DocumentLineID == item.SourceDocumentLineID || p.AutoID == item.SourceDocumentLineID);
            }

            showEditOrderItemModal = true;
            StateHasChanged();
        }

        private void CloseEditOrderItemModal()
        {
            if (mblnAddingNewItemFlag && selectedOrderItem != null && mobjDoc_CashSales != null)
            {
                mobjDoc_CashSales.lstDocumentLine.Remove(selectedOrderItem);
                _ = CalculateTotals();
            }
            mblnAddingNewItemFlag = false;
            showEditOrderItemModal = false;
            selectedOrderItem = null;
            if (editOpenedFromMobileCart)
            {
                showMobileCart = true;
                editOpenedFromMobileCart = false;
            }
        }

        private decimal SafeParseDecimal(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            string cleanInput = input.Replace(",", ".").Trim();
            if (decimal.TryParse(cleanInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return val;
            }
            return 0;
        }

        //Line discount
        private decimal ComputeDiscountForSelectedLine()
        {
            if (selectedOrderItem == null) return 0;

            decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
            if (subtotal <= 0) return 0;

            decimal discountAmount = SafeParseDecimal(editDiscountAmount);
            decimal discountPercent = SafeParseDecimal(editDiscountPercent);

            if (discountAmount > 0)
                return Math.Min(discountAmount, subtotal);

            if (discountPercent > 0)
                return Math.Min(Math.Round(subtotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero), subtotal);

            return 0;
        }

        private decimal CalculateItemTotal()
        {
            if (selectedOrderItem == null) return 0;

            decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
            return Math.Max(0, subtotal - ComputeDiscountForSelectedLine());
        }

        private void OpenOutstandingPaymentCalculator()
        {
            if (otherBalanceSummary != null && otherBalanceSummary.Outstanding > 0)
            {
                OpenNumpadForField("OutstandingPayment", otherBalanceSummary.Outstanding.ToString("F2"));
            }
        }

        private void OpenNumpadForField(string field, string currentValue)
        {
            currentEditField = field;

            if (decimal.TryParse(currentValue, out var val))
                currentEditValue = val == 0 ? "0" : val.ToString("0.##");
            else
                currentEditValue = "0";
            showNumpadModal = true;
            isFreshInput = true;
        }

        private void CloseNumpadModal()
        {
            showNumpadModal = false;
            currentEditField = "";
            currentEditValue = "";
            isFreshInput = false;
        }

        private string GetFriendlyFieldName(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field switch
            {
                "DiscountAmount" => "Discount Amount",
                "DiscountPercent" => "Discount Percent",
                "StaffCommission" => "Staff Commission",
                "PaymentAmount" => "Payment Amount",
                "OutstandingPayment" => "Outstanding Payment",
                _ => field
            };
        }

        private void HandleNumpadEdit(string input)
        {
            if (isFreshInput)
            {
                isFreshInput = false;
                if (input != "⌫" && input != ".")
                {
                    currentEditValue = "0";
                }
            }

            if (input == "⌫")
            {
                currentEditValue = currentEditValue.Length > 1 ? currentEditValue[..^1] : "0";
            }
            else if (input == ".")
            {
                if (!currentEditValue.Contains("."))
                {
                    currentEditValue += ".";
                }
            }
            else if (char.IsDigit(input[0]))
            {
                if (currentEditValue == "0.00" || currentEditValue == "0")
                {
                    currentEditValue = input;
                }
                else
                {
                    currentEditValue += input;
                }
            }
        }

        private void SaveNumpadValue()
        {
            if (string.IsNullOrEmpty(currentEditValue))
            {
                CloseNumpadModal();
                return;
            }

            if (currentEditField == "PaymentAmount")
            {
                if (decimal.TryParse(currentEditValue, out var customAmt))
                {
                    amountToPay = customAmt;
                }
                CloseNumpadModal();
                return;
            }

            if (currentEditField == "OutstandingPayment")
            {
                if (decimal.TryParse(currentEditValue, out var customAmt) && customAmt > 0)
                {
                    AppState.IsOutstandingPaymentMode = true;
                    AppState.OutstandingPaymentAmount = customAmt;

                    var dummyOrder = new Doc_CashSales(5, Guid.NewGuid().ToString());
                    dummyOrder.objDoc_CashSales.AccountID = currentOrder?.objDoc_CashSales?.AccountID ?? "";
                    dummyOrder.objDoc_CashSales.AccountName = currentOrder?.objDoc_CashSales?.AccountName ?? "";
                    dummyOrder.objDoc_CashSales.Phone = currentOrder?.objDoc_CashSales?.Phone ?? "";
                    dummyOrder.objDoc_CashSales.TotalBeforeTax = customAmt;
                    dummyOrder.objDoc_CashSales.TaxAmount = 0m;
                    dummyOrder.objDoc_CashSales.RoundingAmount = 0m;
                    dummyOrder.objDoc_CashSales.TotalAfterTax = customAmt;

                    var dummyLine = new DocumentLineTableDM
                    {
                        Description = "Outstanding Payment",
                        UnitPrice = customAmt,
                        Quantity = 1,
                        Discount = 0,
                        SaveAction = EntityState.Added
                    };
                    dummyOrder.lstDocumentLine.Add(dummyLine);

                    AppState.CurrentOrder = dummyOrder;

                    CloseNumpadModal();
                    Nav.NavigateTo("/cartpayment");
                }
                else
                {
                    CloseNumpadModal();
                }
                return;
            }

            switch (currentEditField)
            {
                case "Price":
                    if (selectedOrderItem != null && decimal.TryParse(currentEditValue, out var newPrice))
                    {
                        selectedOrderItem.UnitPrice = newPrice;
                        decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
                        if (selectedOrderItem.Discount > subtotal)
                        {
                            selectedOrderItem.Discount = subtotal;
                        }
                    }
                    break;

                case "Quantity":
                    if (selectedOrderItem != null)
                    {
                        if (decimal.TryParse(currentEditValue, out var parsedVal))
                        {
                            if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52 && selectedOrderItem.ActivityTypeID == 6)
                            {
                                decimal oldQty = selectedOrderItem.Quantity;
                                decimal newQty = parsedVal;
                                decimal oldCost = oldQty * selectedOrderItem.UnitPrice;
                                decimal newCost = newQty * selectedOrderItem.UnitPrice;
                                decimal deltaCost = newCost - oldCost;

                                if (deltaCost > 0)
                                {
                                    decimal currentCartTotal = mobjDoc_CashSales.lstDocumentLine
                                        .Where(x => x.SaveAction != EntityState.Deleted && x.ActivityTypeID == 6)
                                        .Sum(x => x.UnitPrice * x.Quantity);
                                    decimal requiredTotal = currentCartTotal + deltaCost;

                                    decimal selectedCreditTotal = selectedCreditsToRedeem.Sum(x => x.NetBalanceAfterUtilised);

                                    if (selectedCreditsToRedeem.Any() && selectedCreditTotal < requiredTotal)
                                    {
                                        OpenMultiCreditModal();
                                        ShowNotification($"Selected credit amount is insufficient to cover the expenses. Please select additional credit(s) to cover the total of RM {requiredTotal:F2}.");
                                        CloseNumpadModal();
                                        return;
                                    }
                                }
                            }

                            bool isRedemptionLine = selectedOrderItem != null && (selectedOrderItem.ActivityTypeID == 2 || selectedOrderItem.ActivityTypeID == 5);
                            decimal maxAllowed = 999999m;
                            if (isRedemptionLine)
                            {
                                parsedVal = Math.Floor(parsedVal); // Remove any decimals typed

                                // Check balance limit
                                var pkg = customerPackages.FirstOrDefault(p => p.DocumentLineID == selectedOrderItem.SourceDocumentLineID || p.AutoID == selectedOrderItem.SourceDocumentLineID);
                                if (pkg != null)
                                {
                                    decimal otherLinesQty = currentOrder?.lstDocumentLine
                                        .Where(line => line != selectedOrderItem && 
                                                       line.SaveAction != EntityState.Deleted && 
                                                       (line.ActivityTypeID == 2 || line.ActivityTypeID == 5) && 
                                                       (line.SourceDocumentLineID == selectedOrderItem.SourceDocumentLineID))
                                        .Sum(line => line.Quantity) ?? 0;

                                    maxAllowed = pkg.NetBalanceAfterUtilised - otherLinesQty;
                                    if (maxAllowed < 0) maxAllowed = 0;

                                    if (parsedVal > maxAllowed)
                                    {
                                        ShowAlertDialog("Limit Exceeded", $"Quantity cannot be more than the available balance of <strong>{maxAllowed}</strong>.");
                                        parsedVal = maxAllowed; // Cap it
                                    }
                                }
                            }

                            if (parsedVal < 0.01m) // Minimum safety
                            {
                                selectedOrderItem.Quantity = isRedemptionLine && maxAllowed == 0 ? 0 : 1;
                                ShowNotification(isRedemptionLine && maxAllowed == 0 ? "Quantity set to 0." : "Quantity set to 1.");
                            }
                            else
                            {
                                selectedOrderItem.Quantity = parsedVal;
                            }
                            decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
                            if (selectedOrderItem.Discount > subtotal)
                            {
                                selectedOrderItem.Discount = subtotal;
                            }
                        }
                    }
                    break;

                case "DiscountPercent":
                    editDiscountPercent = currentEditValue;
                    editDiscountAmount = "0.00";
                    break;

                case "DiscountAmount":
                    editDiscountAmount = currentEditValue;
                    editDiscountPercent = "0.00";
                    break;

                case "StaffCommission":
                    if (decimal.TryParse(currentEditValue, out var commAmt))
                    {
                        var row = commissionRows.FirstOrDefault(r => r.id == currentCommissionRowId);
                        if (row != null) row.Amount = commAmt;
                    }
                    break;
            }

            CloseNumpadModal();
            StateHasChanged();
        }

        private async Task SaveOrderItemChanges()
        {
            if (selectedOrderItem != null)
            {
                // 1. Write properties back to the dynamic record
                selectedOrderItem.RefCompanyName = editRemarks;
                selectedOrderItem.Memo = editRemarks;

                selectedOrderItem.Discount = ComputeDiscountForSelectedLine();

                // 2. Map chosen UI staff assignments back into the live structural collection
                selectedOrderItem.lstSalesCommissionByDocumentLine.Clear();
                decimal lineNetTotal = Math.Max(0, (selectedOrderItem.UnitPrice * selectedOrderItem.Quantity) - selectedOrderItem.Discount);

                foreach (var row in commissionRows.Where(r => !string.IsNullOrEmpty(r.StaffId)))
                {
                    bool isPercentage = row.CommissionType == "%";
                    decimal allocatedSales = isPercentage
                        ? Math.Round(lineNetTotal * (row.Amount / 100m), 2)
                        : Math.Round(row.Amount * selectedOrderItem.Quantity, 2);

                    var newCommissionLine = new SalesCommission_ByDocumentLineDM
                    {
                        DocumentID = selectedOrderItem.DocumentID,
                        DocumentLineID = selectedOrderItem.DocumentLineID,
                        EmployeeID = row.StaffId,
                        EmployeeCode = staffList.FirstOrDefault(s => s.MasterAccountID == row.StaffId)?.AccountName ?? row.StaffName,
                        AllocationAmount = row.Amount,
                        CommissionDetailTypeID = isPercentage ? 1 : 2,
                        CommissionSharingPercentage = isPercentage ? row.Amount : 100m,
                        AllocatedSalesAmount = allocatedSales,
                        SalesValue = lineNetTotal,
                        LineQuantity = selectedOrderItem.Quantity,
                        InventoryID = selectedOrderItem.LineItemID,
                        ActivityTypeID = selectedOrderItem.ActivityTypeID,
                        SaveAction = isAddingNewItem ? EntityState.Added : EntityState.Changed
                    };
                    selectedOrderItem.lstSalesCommissionByDocumentLine.Add(newCommissionLine);
                }

                // 3. Append to lines array cache if this is a newly initialized item row
                if (isAddingNewItem && mobjDoc_CashSales != null)
                {
                    mobjDoc_CashSales.lstDocumentLine.Insert(0, selectedOrderItem);
                }

                // 4. Trigger total recalculations across the bill using official formulas
                if (mobjDoc_CashSales != null)
                {
                    mobjDoc_CashSales.Recalculate();
                    await CalculateTotals();
                    await AutoHoldSync();
                }
            }

            mblnAddingNewItemFlag = false;
            isAddingNewItem = false;
            CloseEditOrderItemModal();
        }

        // Public classes
        public class OrderCustomer
        {
            public string Id { get; set; } = string.Empty;
            public string Avatar { get; set; } = "";
            public string Name { get; set; } = "";
            public string Phone { get; set; } = "";
            public string Email { get; set; } = "";
            public string MemberId { get; set; } = "";
            public int BirthdayYear { get; set; }
            public int BirthdayMonth { get; set; }
            public int BirthdayDay { get; set; }

            public decimal Credit { get; set; }
            public decimal Points { get; set; }
            public decimal Package { get; set; }
        }

        public class Order
        {
            public string OrderId { get; set; } = Guid.NewGuid().ToString(); // Track held status
            public OrderCustomer? Customer { get; set; }
            public List<OrderItem> Items { get; set; } = new();
            public bool IsSalesMode { get; set; } = true;
            public decimal TotalDiscount => Items.Sum(i => i.Discount);
            public decimal SubtotalBeforeTax => Items.Sum(i => i.LineSubtotalBeforeTax);

            public decimal TotalTax => Items.Sum(i => i.LineTaxAmount);

            public decimal TotalAfterTaxBeforeRounding => SubtotalBeforeTax + TotalTax;

            public decimal GrandTotal => Math.Round(TotalAfterTaxBeforeRounding * 20, MidpointRounding.AwayFromZero) / 20m;

            public decimal RoundingAmount => GrandTotal - TotalAfterTaxBeforeRounding;

            public decimal Subtotal => Items.Sum(i => i.LineTotal);

            public DateTime DateCreated { get; set; } = DateTime.Now;
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }

        public class OrderItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public int InventoryTypeID { get; set; }
            public string DisplayCode { get; set; } = "";
            public decimal Price { get; set; }
            public decimal Quantity { get; set; } = 1;
            public decimal Discount { get; set; }
            public string? ImagePath { get; set; }
            public string Remarks { get; set; } = "";
            public string UnitOfMeasurementID { get; set; } = string.Empty;

            public decimal LineTotal => Math.Max(0, Price * Quantity - Discount);

            public string? TaxCodeID { get; set; }

            public bool IsTaxInclusive { get; set; }

            public decimal TaxRate { get; set; }

            [JsonIgnore]
            public decimal LineSubtotalBeforeTax => OrderLineTaxCalculator.ComputeSubtotalBeforeTax(Price, Quantity, Discount, TaxRate, IsTaxInclusive);

            [JsonIgnore]
            public decimal LineTaxAmount => OrderLineTaxCalculator.ComputeTaxAmount(Price, Quantity, Discount, TaxRate, IsTaxInclusive);

            [JsonIgnore]
            public decimal LineTotalAfterTax => LineSubtotalBeforeTax + LineTaxAmount;
            public string? RedemptionSourceID { get; set; }
            public bool IsRedemption { get; set; } = false;
            public List<LstCashSalesSeriesUnconsumedItem> PackageUnconsumedItems { get; set; } = new();
            public string CommissionType { get; set; } = "%";
            public List<StaffCommissionRow> StaffCommissions { get; set; } = new();
        }

        private async Task DeleteCurrentOrder()
        {
            if (currentOrder != null)
            {
                heldOrders.RemoveAll(o => o.objDoc_CashSales.DocumentID == currentOrder.objDoc_CashSales.DocumentID);
                StartNewSale();
                await SaveHeldBillsToStorage();
                ShowNotification("Order deleted.");
            }
        }

        private string itemSearchTerm = "";

        private bool IsSearching => !string.IsNullOrWhiteSpace(itemSearchTerm);

        // Redeem Package
        private CashSales_Series_UnconsumedItemDM? selectedPackageToRedeem;
        private bool isRedeemingPackage = false;

        private void SelectPackageForRedemption(CashSales_Series_UnconsumedItemDM pkg)
        {
            selectedPackageAutoID = pkg.AutoID;
            selectedPackageToRedeem = pkg;

            _ = FetchHistory(pkg.AutoID);
        }

        // Redeem Package, cant work, leave it first
        private async Task ApplyPackageRedemption()
        {
            if (isSalesMode)
            {
                ShowNotification("Please switch to 'Redemption' mode at the top bar before redeeming packages.");
                isRedeemingPackage = false;
                StateHasChanged();
                return;
            }

            if (selectedPackageToRedeem == null)
            {
                ShowNotification("Please select a package item from the list first.");
                isRedeemingPackage = false;
                StateHasChanged();
                return;
            }

            decimal currentCartQty = currentOrder?.lstDocumentLine
                .Where(line => line.SaveAction != EntityState.Deleted && 
                               (line.ActivityTypeID == 2 || line.ActivityTypeID == 5) && 
                               (line.SourceDocumentLineID == selectedPackageToRedeem.DocumentLineID || line.SourceDocumentLineID == selectedPackageToRedeem.AutoID))
                .Sum(line => line.Quantity) ?? 0;

            if (currentCartQty + 1 > selectedPackageToRedeem.NetBalanceAfterUtilised)
            {
                ShowAlertDialog("Redemption Limit Reached", $"Cannot redeem. Total quantity in cart (<strong>{currentCartQty}</strong>) plus new redemption exceeds the available balance of <strong>{selectedPackageToRedeem.NetBalanceAfterUtilised}</strong>.");
                isRedeemingPackage = false;
                StateHasChanged();
                return;
            }

            try
            {
                if (currentOrder != null)
                {
                    // Load target Inventory detail mapped directly into line parameters items
                    var invItem = await InventoryService.LoadItemAsync(selectedPackageToRedeem.InventoryID);
                    if (invItem != null)
                    {
                        var unconsumedMock = new CashSales_Series_UnconsumedItemDM
                        {
                            AutoID = selectedPackageToRedeem.AutoID,
                            PackageID = selectedPackageToRedeem.PackageID,
                            InventoryID = selectedPackageToRedeem.InventoryID,
                            CurrentRedeemQuantity = 1,
                            UnitPrice = selectedPackageToRedeem.UnitPrice
                        };

                        await AddSelectedItemToBill_AfterCheckingforBundle(invItem, objUnconsumedItem: unconsumedMock, strOverrideSalesDescription: selectedPackageToRedeem.Description);
                        await AutoHoldSync();

                        var addedLine = mobjDoc_CashSales.lstDocumentLine.FirstOrDefault(x => x.SourceDocumentLineID == unconsumedMock.AutoID);
                        if (addedLine != null)
                        {
                            mblnAddingNewItemFlag = true;
                            OpenEditOrderItemModal(addedLine, isNew: false);
                        }
                    }
                    showVoucherModal = false;
                    selectedPackageAutoID = null;
                }
            }
            finally
            {
                isRedeemingPackage = false;
                StateHasChanged();
            }
        }

        // Profile edit modal
        private bool showProfileEditModal = false;
        private bool isSavingProfile = false;
        private bool isLoadingProfile = false;
        private string activeProfileTab = "Info";

        private bool isNumpadOpen;
        private string numpadInitialValue = "";
        private string numpadTarget = "";

        private void OpenNewPhoneNumpad()
        {
            numpadTarget = "newPhone";
            numpadInitialValue = newPhone;
            isNumpadOpen = true;
        }

        private void OpenEditPhoneNumpad()
        {
            numpadTarget = "editPhone";
            numpadInitialValue = editPhone;
            isNumpadOpen = true;
        }

        private void OnNumpadSaved(string val)
        {
            if (numpadTarget == "newPhone")
            {
                newPhone = val;
            }
            else if (numpadTarget == "editPhone")
            {
                editPhone = val;
            }
        }

        // Edit form fields 
        private string editName = "";
        private string editPhone = "";
        private string editIC = "";
        private DateTime? editBirthday = null;
        private string editEthnic = "";
        private string editSource = "";
        private string editEmail = "";
        private string editMarital = "";
        private string editGender = "";
        private string editMemberTier = "";
        private string editProfileRemarks = "";
        private string editAddress1 = "";
        private string editAddress2 = "";
        private string editZipCode = "";
        private string editCity = "";
        private string editCountryState = "";
        private string editCountry = "";

        private async Task OpenProfileEditModal()
        {
            if (currentOrder?.objDoc_CashSales == null) return;

            editName = currentOrder.objDoc_CashSales.AccountName;
            editPhone = currentOrder.objDoc_CashSales.Phone;
            editEmail = AppState.SelectedCustomer?.Email ?? "";
            editIC = ""; editEthnic = ""; editSource = ""; editMarital = ""; editGender = ""; editMemberTier = ""; editProfileRemarks = ""; editAddress1 = ""; editAddress2 = ""; editZipCode = ""; editCity = ""; editCountryState = ""; editCountry = ""; editBirthday = null;

            activeProfileTab = "Info";
            isLoadingProfile = true;
            showProfileEditModal = true;
            StateHasChanged();

            try
            {
                var fullCustomer = await _customerService.GetSingleCustomer(currentOrder.objDoc_CashSales.AccountID);

                if (fullCustomer != null && !string.IsNullOrEmpty(fullCustomer.MasterAccountID))
                {
                    editName = fullCustomer.AccountName ?? editName;
                    editPhone = fullCustomer.Phone ?? editPhone;
                    editEmail = fullCustomer.Email ?? "";
                    editIC = fullCustomer.NRIC ?? "";
                    editEthnic = fullCustomer.RaceName ?? "";
                    editSource = fullCustomer.CustomerSourceName ?? "";
                    editMarital = fullCustomer.MaritalStatus ?? "";
                    editGender = fullCustomer.Gender ?? "";
                    editMemberTier = fullCustomer.MembershipTypeName ?? "";
                    editProfileRemarks = fullCustomer.Comment ?? "";
                    editAddress1 = fullCustomer.Address1 ?? "";
                    editAddress2 = fullCustomer.Address2 ?? "";
                    editZipCode = fullCustomer.ZipCode ?? "";
                    editCity = fullCustomer.City ?? "";
                    editCountryState = fullCustomer.CountryState ?? "";
                    editCountry = fullCustomer.Country ?? "";

                    if (fullCustomer.BirthdayYear > 0 && fullCustomer.BirthdayMonth > 0 && fullCustomer.BirthdayDay > 0)
                    {
                        try { editBirthday = new DateTime(fullCustomer.BirthdayYear, fullCustomer.BirthdayMonth, fullCustomer.BirthdayDay); }
                        catch { editBirthday = null; }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[PROFILE LOAD ERROR]: {ex.Message}"); }
            finally
            {
                isLoadingProfile = false;
                StateHasChanged();
            }
        }

        private async Task SaveProfileChanges()
        {
            if (string.IsNullOrWhiteSpace(editName)) { ShowNotification("Name is required."); return; }
            if (string.IsNullOrWhiteSpace(editPhone)) { ShowNotification("Contact is required."); return; }

            if (editBirthday.HasValue && editBirthday.Value.Date > DateTime.Today)
            {
                ShowNotification("Birthdate cannot be a future date.");
                return;
            }

            if (editPhone.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                ShowNotification("Phone number cannot contain English letters.");
                return;
            }

            if (!editPhone.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == '(' || c == ')' || char.IsWhiteSpace(c)))
            {
                ShowNotification("Phone number can only accept numeric input.");
                return;
            }

            isSavingProfile = true;
            try
            {
                var updateDto = new CustomerDM
                {
                    MasterAccountID = currentOrder!.objDoc_CashSales.AccountID,
                    AccountName = editName.Trim(),
                    Phone = editPhone.Trim(),
                    Email = editEmail?.Trim() ?? "",
                    NRIC = editIC?.Trim() ?? "",
                    BirthdayDay = editBirthday?.Day ?? 0,
                    BirthdayMonth = editBirthday?.Month ?? 0,
                    BirthdayYear = editBirthday?.Year ?? 0,
                    RaceName = editEthnic?.Trim() ?? "",
                    CustomerSourceName = editSource?.Trim() ?? "",
                    MaritalStatus = editMarital,
                    Gender = editGender,
                    MembershipTypeName = editMemberTier,
                    Comment = editProfileRemarks,
                    Address1 = editAddress1,
                    Address2 = editAddress2,
                    ZipCode = editZipCode,
                    City = editCity,
                    CountryState = editCountryState,
                    Country = editCountry,
                    SaveAction = EntityState.Changed,
                    IsLoading = false
                };

                var response = await _customerService.UpdateCustomer(updateDto);

                if (response != null && response.statusCode == 200)
                {
                    currentOrder.objDoc_CashSales.AccountName = editName.Trim();
                    currentOrder.objDoc_CashSales.Phone = editPhone.Trim();

                    if (AppState.SelectedCustomer != null)
                    {
                        AppState.SelectedCustomer.AccountName = editName.Trim();
                        AppState.SelectedCustomer.Phone = editPhone.Trim();
                        AppState.SelectedCustomer.Email = editEmail?.Trim() ?? "";
                        AppState.SelectedCustomer.NRIC = editIC?.Trim() ?? "";
                        AppState.SelectedCustomer.BirthdayDay = editBirthday?.Day ?? 0;
                        AppState.SelectedCustomer.BirthdayMonth = editBirthday?.Month ?? 0;
                        AppState.SelectedCustomer.BirthdayYear = editBirthday?.Year ?? 0;
                        AppState.SelectedCustomer.RaceName = editEthnic?.Trim() ?? "";
                        AppState.SelectedCustomer.CustomerSourceName = editSource?.Trim() ?? "";
                        AppState.SelectedCustomer.MaritalStatus = editMarital;
                        AppState.SelectedCustomer.Gender = editGender;
                        AppState.SelectedCustomer.MembershipTypeName = editMemberTier;
                        AppState.SelectedCustomer.Comment = editProfileRemarks;
                        AppState.SelectedCustomer.Address1 = editAddress1;
                        AppState.SelectedCustomer.Address2 = editAddress2;
                        AppState.SelectedCustomer.ZipCode = editZipCode;
                        AppState.SelectedCustomer.City = editCity;
                        AppState.SelectedCustomer.CountryState = editCountryState;
                        AppState.SelectedCustomer.Country = editCountry;
                    }

                    await AutoHoldSync();
                    ShowNotification("Profile updated successfully.");
                    showProfileEditModal = false;
                    StateHasChanged();
                }
                else
                {
                    ShowNotification($"Update failed: {response?.message ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("An error occurred while saving.");
                Console.WriteLine($"[PROFILE SAVE ERROR]: {ex.Message}");
            }
            finally { isSavingProfile = false; }
        }
    }
}