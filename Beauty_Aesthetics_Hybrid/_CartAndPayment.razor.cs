using EBI.UC;
using EBI.DM;
using EBI.Enum;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.PaymentService;
using SenangRetails.Shared.Services.CustomerService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using static SenangRetails.Shared.Pages.Orders;

namespace SenangRetails.Shared.Pages
{
    public partial class CartAndPayment
    {
        [Inject] private NavigationManager Nav { get; set; } = default!;
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IPaymentService PaymentService { get; set; } = default!;
        [Inject] private ICashSalesService CashSalesService { get; set; } = default!;
        [Inject] private ICustomerService CustomerService { get; set; } = default!;
        [Inject] private ProductCacheService ProductCacheService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.ARReceiptService.IARReceiptService ARReceiptService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.ThemeService.IThemeService ThemeService { get; set; } = default!;

        [Inject] private IJSRuntime JS { get; set; } = default!;
        private Doc_CashSales? currentOrder;
        private const string StorageKey = "held_bills_cache";
        private List<Doc_CashSales_POSPaymentLineTypeDM> availableMethods = new();
        private List<PaymentLineItem> appliedPayments = new();
        private bool isProcessing = false;
        private bool isSelected = false;
        private bool isRedemptionMode = false;
        private bool IsChangeDue() => GetRemainingBalance() < 0;
        private decimal GetChangeAmount() => Math.Abs(GetRemainingBalance());

        private bool showChangeModal = false;
        private decimal changeDueAmount = 0m;
        private decimal totalBillAmount = 0m;
        private decimal totalTenderedAmount = 0m;

        private void ApplyQuickCash(PaymentLineItem pay, decimal amount)
        {
            pay.InputAmount = amount.ToString("F2");
            StateHasChanged();
        }

        private IEnumerable<decimal> GetQuickCashDenominations(decimal grandTotal)
        {
            var denoms = new List<decimal> { 10m, 20m, 50m, 100m, 200m, 500m };
            var higher = denoms.Where(d => d > grandTotal).Take(4).ToList();
            if (!higher.Any())
            {
                higher.Add(Math.Ceiling(grandTotal / 50m) * 50m + 50m);
            }
            return higher;
        }

        private void CloseChangeModalAndFinish()
        {
            showChangeModal = false;
            Nav.NavigateTo("/orders");
        }

        public class PaymentLineItem
        {
            public int PaymentTypeId { get; set; }
            public string PaymentName { get; set; } = "";
            public string FinancialAccountId { get; set; } = "";
            public string BankName { get; set; } = "";
            public string InputAmount { get; set; } = "0.00";
            public decimal Amount => decimal.TryParse(InputAmount, out var val) ? val : 0;
        }

        protected override async Task OnInitializedAsync()
        {
            currentOrder = AppState.CurrentOrder;

            if (AppState.IsOutstandingPaymentMode)
            {
                if (currentOrder == null || 
                    currentOrder.lstDocumentLine == null || 
                    !currentOrder.lstDocumentLine.Any() || 
                    currentOrder.lstDocumentLine.Any(line => line.Description != "Outstanding Payment"))
                {
                    AppState.IsOutstandingPaymentMode = false;
                    AppState.OutstandingPaymentAmount = 0m;
                }
            }

            if (currentOrder?.objDoc_CashSales != null)
            {
                // ── MIGRATED: 52 represents Redemption mode, 5 represents Sales mode ──
                isRedemptionMode = currentOrder.objDoc_CashSales.DocumentTypeID == 52;
            }

            var branchId = AppState.SelectedBranchID;
            var groupId = AppState.SelectedBranchGroupID;

            // ── MIGRATED: Access AccountID directly from the core DM properties block ──
            var customerId = currentOrder?.objDoc_CashSales?.AccountID ?? "";
            availableMethods = await PaymentService.GetPaymentMethodsAsync(branchId, groupId, customerId) ?? new();
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                if (!availableMethods.Any(m => m.POSPaymentTypeName.Equals("Point Balance", StringComparison.OrdinalIgnoreCase)))
                {
                    availableMethods.Add(new Doc_CashSales_POSPaymentLineTypeDM
                    {
                        POSPaymentTypeID = -99,
                        POSPaymentTypeName = "Point Balance",
                        Active = true
                    });
                }
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try { await ThemeService.InitializeThemeAsync(); } catch { }

                if (currentOrder == null)
                {
                    await ReloadOrderFromStorage();
                }
                else if (availableMethods == null || availableMethods.Count == 0)
                {
                    await LoadPaymentMethodsAsync();
                    StateHasChanged();
                }
            }
        }

        private async Task LoadPaymentMethodsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(AppState.SelectedBranchID))
                {
                    var savedId = await JS.InvokeAsync<string?>("localStorage.getItem", "currentBranch");
                    if (!string.IsNullOrEmpty(savedId))
                    {
                        AppState.SelectedBranchID = savedId;
                    }
                }

                if (string.IsNullOrEmpty(AppState.SelectedBranchGroupID))
                {
                    var savedDetails = await JS.InvokeAsync<string?>("localStorage.getItem", "currentBranchDetails");
                    if (!string.IsNullOrEmpty(savedDetails))
                    {
                        var branch = JsonSerializer.Deserialize<BranchItem>(savedDetails);
                        if (branch != null)
                        {
                            AppState.CurrentBranch = branch;
                            AppState.SelectedBranchGroupID = branch.BranchGroupID ?? "";
                        }
                    }
                }

                var branchId = AppState.SelectedBranchID;
                var groupId = AppState.SelectedBranchGroupID;
                var customerId = currentOrder?.objDoc_CashSales?.AccountID ?? "";

                availableMethods = await PaymentService.GetPaymentMethodsAsync(branchId, groupId, customerId) ?? new();
                if (!string.IsNullOrWhiteSpace(customerId))
                {
                    if (!availableMethods.Any(m => m.POSPaymentTypeName.Equals("Point Balance", StringComparison.OrdinalIgnoreCase)))
                    {
                        availableMethods.Add(new Doc_CashSales_POSPaymentLineTypeDM
                        {
                            POSPaymentTypeID = -99,
                            POSPaymentTypeName = "Point Balance",
                            Active = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading payment methods: {ex.Message}");
            }
        }

        private async Task ReloadOrderFromStorage()
        {
            try
            {
                var activeOrderId = await JS.InvokeAsync<string?>("localStorage.getItem", "active_order_id");
                var json = await JS.InvokeAsync<string>("localStorage.getItem", StorageKey);

                if (!string.IsNullOrEmpty(activeOrderId) && !string.IsNullOrEmpty(json))
                {
                    // ── MIGRATED: Deserialize into the live Doc_CashSales collection assembly layout ──
                    var heldOrders = JsonSerializer.Deserialize<List<Doc_CashSales>>(json);

                    // Find the order matching the unique sequential DocumentID key descriptor
                    var resumedOrder = heldOrders?.FirstOrDefault(o => o.objDoc_CashSales.DocumentID == activeOrderId);

                    if (resumedOrder != null)
                    {
                        currentOrder = resumedOrder;
                        AppState.CurrentOrder = resumedOrder;
                        isRedemptionMode = currentOrder.objDoc_CashSales.DocumentTypeID == 52;
                        
                        await LoadPaymentMethodsAsync();
                        
                        StateHasChanged();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reloading order on payment page: {ex.Message}");
            }

            if (currentOrder == null)
            {
                Nav.NavigateTo("/orders");
            }
        }

        private bool showAlertDialogModal = false;
        private string alertDialogTitle = "Notification";
        private string alertDialogMessage = "";

        private void ShowAlertDialog(string title, string message)
        {
            alertDialogTitle = title;
            alertDialogMessage = message;
            showAlertDialogModal = true;
            StateHasChanged();
        }

        private void ShowNotification(string message)
        {
            ShowAlertDialog("Notification", message);
        }

        private void GoBack()
        {
            Nav.NavigateTo("/orders");
        }

        // for the order summary
        private string GetItemTypeName(int typeId) => typeId switch
        {
            1 => "Product",
            3 => "Service",
            5 => "Package",
            7 => "TopUp",
            _ => "Product"
        };

        private static string GetRoundingDisplay(Doc_CashSales? order)
        {
            if (order?.objDoc_CashSales == null) return "RM 0.00";
            var r = order.objDoc_CashSales.RoundingAmount;
            if (r == 0) return "RM 0.00";
            var sign = r < 0 ? "- " : "+ ";
            return $"{sign}RM {Math.Abs(r):F2}";
        }

        // to toggle the payment method
        private async Task TogglePaymentMethod(Doc_CashSales_POSPaymentLineTypeDM method)
        {
            var existing = appliedPayments.FirstOrDefault(p => p.PaymentTypeId == method.POSPaymentTypeID);

            if (existing != null)
            {
                appliedPayments.Remove(existing);
            }
            else
            {
                if (AppState.IsOutstandingPaymentMode && appliedPayments.Any())
                {
                    ShowNotification("Only one payment method is allowed for outstanding payments.");
                    return;
                }

                if (GetRemainingBalance() <= 0)
                {
                    ShowNotification("Remaining balance is already fully paid.");
                    return;
                }

                if (method.POSPaymentTypeName.Equals("Point Balance", StringComparison.OrdinalIgnoreCase))
                {
                    var customerId = currentOrder?.objDoc_CashSales?.AccountID ?? "";
                    var customerName = currentOrder?.objDoc_CashSales?.AccountName ?? "Unknown";
                    decimal pointBalance = 0;

                    if (!string.IsNullOrEmpty(customerId))
                    {
                        var summary = await CustomerService.GetMemberBalanceSummaryAsync(customerId);
                        if (summary != null)
                        {
                            pointBalance = summary.PointBalance;
                        }
                    }

                    decimal requiredPoints = GetTotalPointsRequired();
                    if (pointBalance < requiredPoints)
                    {
                        ShowNotification($"Point balance of {customerName} is {pointBalance.ToString("0.##")}, insufficient for redemption");
                        return;
                    }
                }

                decimal remainingBalance = GetRemainingBalance();
                appliedPayments.Add(new PaymentLineItem
                {
                    PaymentTypeId = method.POSPaymentTypeID,
                    PaymentName = method.POSPaymentTypeName,
                    FinancialAccountId = method.FinancialAccountID ?? "",
                    BankName = method.FinancialAccountName ?? "",
                    InputAmount = (remainingBalance > 0 ? remainingBalance : 0).ToString("F2")
                });
            }
            StateHasChanged();
        }

        private decimal GetRemainingBalance()
        {
            if (currentOrder?.objDoc_CashSales == null) return 0;
            decimal totalPaid = appliedPayments.Sum(p => p.Amount);
            return currentOrder.objDoc_CashSales.TotalAfterTax - totalPaid;
        }

        private decimal GetTotalPointsRequired()
        {
            decimal totalPoints = 0;
            if (currentOrder?.lstDocumentLine != null)
            {
                foreach (var line in currentOrder.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted))
                {
                    InventoryDM? invItem = null;
                    if (AppState?.lstAllSalesItems != null && AppState.lstAllSalesItems.TryGetValue(line.LineItemID, out invItem))
                    {
                        // Found in AppState dictionary
                    }
                    else if (ProductCacheService?.Items != null)
                    {
                        invItem = ProductCacheService.Items.FirstOrDefault(x => x.MasterAccountID == line.LineItemID);
                    }

                    if (invItem != null && decimal.TryParse(invItem.PointToRedeem, out var pts))
                    {
                        totalPoints += pts * line.Quantity;
                    }
                }
            }
            return totalPoints;
        }

        private void RemovePaymentLine(PaymentLineItem line)
        {
            appliedPayments.Remove(line);
        }

        private async Task HandleCompletePayment()
        {
            if (currentOrder?.objDoc_CashSales == null) return;
            if (!isRedemptionMode && !appliedPayments.Any()) return;

            if (!isRedemptionMode)
            {
                if (appliedPayments.Any(p => string.IsNullOrWhiteSpace(p.InputAmount)))
                {
                    ShowNotification("Please enter an amount for all selected payment methods.");
                    return;
                }

                decimal calculatedRunningTotal = 0;
                foreach (var pay in appliedPayments)
                {
                    bool isCashMethod = pay.PaymentName.Equals("Cash", StringComparison.OrdinalIgnoreCase);

                    if (!isCashMethod && pay.Amount > currentOrder.objDoc_CashSales.TotalAfterTax)
                    {
                        ShowNotification($"The amount for {pay.PaymentName} cannot exceed the Grand Total of RM {currentOrder.objDoc_CashSales.TotalAfterTax:F2}.");
                        return;
                    }

                    if (pay.PaymentName.Equals("Point Balance", StringComparison.OrdinalIgnoreCase))
                    {
                        var customerId = currentOrder?.objDoc_CashSales?.AccountID ?? "";
                        var customerName = currentOrder?.objDoc_CashSales?.AccountName ?? "Unknown";
                        decimal pointBalance = 0;

                        if (!string.IsNullOrEmpty(customerId))
                        {
                            var summary = await CustomerService.GetMemberBalanceSummaryAsync(customerId);
                            if (summary != null)
                            {
                                pointBalance = summary.PointBalance;
                            }
                        }

                        decimal requiredPoints = GetTotalPointsRequired();
                        if (pointBalance < requiredPoints)
                        {
                            ShowNotification($"Point balance of {customerName} is {pointBalance.ToString("0.##")}, insufficient for redemption");
                            return;
                        }
                    }

                    calculatedRunningTotal += pay.Amount;
                }

                decimal remaining = GetRemainingBalance();
                bool hasCashPayment = appliedPayments.Any(p => p.PaymentName.Equals("Cash", StringComparison.OrdinalIgnoreCase));
                if (remaining > 0)
                {
                    ShowNotification($"Balance of RM {remaining:F2} still outstanding.");
                    return;
                }
                if (remaining < 0 && !hasCashPayment)
                {
                    ShowNotification("Overpayment is only allowed when using the Cash payment method.");
                    return;
                }
            }

            isProcessing = true;
            StateHasChanged();

            if (AppState.IsOutstandingPaymentMode)
            {
                try
                {
                    if (appliedPayments.Count > 1)
                    {
                        ShowNotification("Only one payment method is allowed for outstanding payments.");
                        isProcessing = false;
                        StateHasChanged();
                        return;
                    }

                    var firstPayment = appliedPayments.FirstOrDefault();
                    if (firstPayment == null)
                    {
                        ShowNotification("Please select a payment method.");
                        isProcessing = false;
                        StateHasChanged();
                        return;
                    }

                    string bankAccountId = string.IsNullOrWhiteSpace(firstPayment.FinancialAccountId) ? "000000000000005" : firstPayment.FinancialAccountId;
                    string bankAccountName = string.IsNullOrWhiteSpace(firstPayment.BankName) ? "Bank Account" : firstPayment.BankName;
                    string currencyID = AppState.CurrentBranch?.CurrencyID;
                    string currencyName = AppState.CurrentBranch?.CurrencyName;
                    decimal totalPaid = appliedPayments.Sum(x => x.Amount);

                    var retrievedLines = await ARReceiptService.RetrieveSettlementLinesAsync(
                        currentOrder.objDoc_CashSales.AccountID,
                        AppState.SelectedBranchGroupID);

                    var sortedLines = retrievedLines
                        .Where(x => x.Outstanding > 0)
                        .OrderBy(x => x.FinancialDate)
                        .ToList();

                    decimal remainingPayment = totalPaid;
                    var offsetLines = new System.Collections.ObjectModel.ObservableCollection<EBI.DM.ud_ARAPPaymentOffSetLineDM>();

                    foreach (var line in sortedLines)
                    {
                        if (remainingPayment <= 0)
                            break;

                        decimal outstanding = line.Outstanding;
                        decimal allocated = Math.Min(remainingPayment, outstanding);

                        line.AllocatedAmount = allocated;
                        line.SaveAction = EBI.Enum.EntityState.Added;
                        line.IsDirty = true;

                        if (string.IsNullOrEmpty(line.CurrencyID))
                        {
                            line.CurrencyID = currencyID;
                            line.CurrencyName = currencyName;
                        }

                        offsetLines.Add(line);
                        remainingPayment -= allocated;
                    }

                    var docReceipt = new Doc_ARReceipt();
                    var header = new Doc_ARReceiptDM
                    {
                        BranchID = AppState.SelectedBranchID,
                        AccountID = currentOrder.objDoc_CashSales.AccountID,
                        AccountName = currentOrder.objDoc_CashSales.AccountName,
                        ReferenceNumber = "",
                        LocalCurrencyID = currencyID,
                        LocalCurrencyName = currencyName,
                        ExchangeRate = 1m,
                        Remarks = "",
                        GroupID = AppState.SelectedBranchGroupID,
                        BankExchangeRate = 1m,
                        BankAccountID = bankAccountId,
                        BankAccountName = bankAccountName,
                        BankCharges = 0m,
                        TotalAllocatedAmount = totalPaid,
                        SaveAction = EBI.Enum.EntityState.Added,
                        FinancialAccountID = bankAccountId,
                        IsDirty = true,
                        blnIsPeriodClosed = true,
                        blnIsBankReconciliationDone = true
                    };
                    docReceipt.objDoc_ARReceipt = header;
                    docReceipt.lstud_ARAPPaymentOffSetLineDM = offsetLines;

                    var result = await ARReceiptService.CreateARReceiptRecordAsync(docReceipt);

                    if (result != null && result.statusCode == 200)
                    {
                        AppState.LastSaleDocumentId = result.result?.ToString() ?? "outstanding";
                        AppState.LastReceiptNo = result.result?.ToString() ?? "";
                        AppState.LastPaidAmount = totalPaid;
                        AppState.LastCompletedOrder = null;

                        await JS.InvokeVoidAsync("localStorage.removeItem", "active_order_id");
                        Nav.NavigateTo("/complete-sales");
                    }
                    else
                    {
                        ShowNotification(result?.message ?? "Failed to save outstanding payment.");
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification($"Error: {ex.Message}");
                }
                finally
                {
                    isProcessing = false;
                    StateHasChanged();
                }
                return;
            }

            var servicePayments = isRedemptionMode ? new List<PaymentLine>() : appliedPayments.Select(p => new PaymentLine
            {
                PaymentTypeId = p.PaymentTypeId,
                PaymentName = p.PaymentName,
                Amount = p.Amount,
                FinancialAccountId = p.FinancialAccountId,
                BankName = p.BankName
            }).ToList();

            var cashSalesResult = await CashSalesService.CompletePaymentAsync(currentOrder, servicePayments);

            if (cashSalesResult.Success)
            {
                AppState.LastSaleDocumentId = cashSalesResult.DocumentId ?? "";
                AppState.LastReceiptNo = cashSalesResult.DisplayCode ?? "";
                AppState.LastPaidAmount = isRedemptionMode ? 0 : appliedPayments.Sum(x => x.Amount);
                AppState.LastCompletedOrder = currentOrder;

                await CleanupProcessedOrder(currentOrder?.objDoc_CashSales?.DocumentID ?? "");
                Nav.NavigateTo("/complete-sales");
            }
            else
            {
                ShowNotification(cashSalesResult.Message);
            }

            isProcessing = false;
            StateHasChanged();
        }

        private async Task CleanupProcessedOrder(string orderId)
        {
            try
            {
                await JS.InvokeVoidAsync("localStorage.removeItem", "active_order_id");

                var json = await JS.InvokeAsync<string>("localStorage.getItem", "held_bills_cache");
                if (!string.IsNullOrEmpty(json))
                {
                    var heldOrders = JsonSerializer.Deserialize<List<Doc_CashSales>>(json);
                    if (heldOrders != null)
                    {
                        heldOrders.RemoveAll(o => o.objDoc_CashSales?.DocumentID == orderId);
                        var newJson = JsonSerializer.Serialize(heldOrders);
                        await JS.InvokeVoidAsync("localStorage.setItem", "held_bills_cache", newJson);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up storage: {ex.Message}");
            }
        }

    }
}
