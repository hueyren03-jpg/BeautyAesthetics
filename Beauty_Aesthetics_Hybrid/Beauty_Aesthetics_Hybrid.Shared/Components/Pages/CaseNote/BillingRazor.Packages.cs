using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Sales;
using Beauty_Aesthetics_WebPos.Components.Services.Tax;
using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.CaseNote;

public partial class BillingRazor
{
    [Inject] private IPendingOrderService PackagePendingOrders { get; set; } = default!;
    private readonly List<TransactionItem> BillingPackageItems = [];
    private readonly Dictionary<TransactionItem, (List<BillingItem> Products, List<BillingService> Services)> packageReplacements = new();
    private bool showCustomerPackageDialog;
    private string billingPackageView = "owned";
    private TransactionItem? pendingPackageRedemption;
    private bool HasBillingPackageRedemption => BillingPackageItems.Any(item => item.IsPackageRedemption);
    private bool CanBuyBillingPackage => !IsAppointmentPaymentLocked && !_isProcessingPayment && !_isOutstandingPaymentMode && !HasBillingPackageRedemption;
    private bool CanRedeemBillingPackage => !IsAppointmentPaymentLocked && !_isProcessingPayment && !_isOutstandingPaymentMode &&
        !BillingPackageItems.Any(item => item.InventoryTypeId == 5) && MemberCreditItems.Count == 0 && !HasBillingPointRedemption;
    private decimal BillingPackageCoveredAmount => BillingPackageItems.Where(item => item.IsPackageRedemption).Sum(PackageLineTotal);
    private decimal BillingAmountDue => Math.Max(0m, TotalPayable - BillingPackageCoveredAmount);
    private bool CanCompleteBillingWithoutTender => IsFullyPointRedeemedBillingSale ||
        (!_isOutstandingPaymentMode && HasBillingPackageRedemption && BillingAmountDue <= 0.009m);
    private int MatchingBillingQuantity(TransactionItem item) => item.InventoryTypeId == 1
        ? PrescriptionItems.Where(line => line.SourceId == item.InventoryId).Sum(line => line.Quantity)
        : ServiceItems.Where(line => line.SourceId == item.InventoryId).Sum(line => line.Quantity);

    private IReadOnlyDictionary<string, decimal> BillingReservedPackageQuantities => BillingPackageItems.Concat(
        PackagePendingOrders.GetAll().Where(order => string.Equals(order.AccountId, _billingCustomerId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(order => order.Items)).Where(item => item.IsPackageRedemption)
        .GroupBy(item => item.SourceDocumentLineId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => (decimal)group.Sum(item => item.Quantity), StringComparer.OrdinalIgnoreCase);

    private void OpenCustomerPackages()
    {
        if (string.IsNullOrWhiteSpace(_billingCustomerId)) { Feedback.Warning("Load the appointment patient first.", "Patient required"); return; }
        billingPackageView = "owned";
        _isPatientDrawerOpen = false;
        showCustomerPackageDialog = true;
    }

    private void OpenBillingPackagePurchase()
    {
        if (!CanBuyBillingPackage) return;
        if (string.IsNullOrWhiteSpace(_billingCustomerId)) { Feedback.Warning("Load the appointment patient first.", "Patient required"); return; }
        billingPackageView = "buy";
        _isPatientDrawerOpen = false;
        showCustomerPackageDialog = true;
    }

    private void CloseCustomerPackages() => showCustomerPackageDialog = false;

    private void AddBillingPackagePurchase(TransactionItem item)
    {
        if (!CanBuyBillingPackage || item.InventoryTypeId != 5) return;
        BillingPackageItems.Add(item);
        CloseCustomerPackages();
        ResetPackagePaymentState();
    }

    private void QueueBillingPackageRedemption(TransactionItem item)
    {
        if (!CanRedeemBillingPackage || !item.IsPackageRedemption) return;
        CloseCustomerPackages();
        if (MatchingBillingQuantity(item) > 0) pendingPackageRedemption = item;
        else ApplyBillingPackageRedemption(item);
    }

    private void ConfirmBillingPackageRedemption()
    {
        if (pendingPackageRedemption is null || !CanRedeemBillingPackage) return;
        var item = pendingPackageRedemption;
        pendingPackageRedemption = null;
        ApplyBillingPackageRedemption(item);
    }

    private void ApplyBillingPackageRedemption(TransactionItem item)
    {
        var products = new List<BillingItem>();
        var services = new List<BillingService>();
        var remaining = item.Quantity;
        if (item.InventoryTypeId == 1)
        {
            foreach (var line in PrescriptionItems.Where(line => line.SourceId == item.InventoryId).ToList())
            {
                if (remaining <= 0) break;
                var covered = Math.Min(remaining, line.Quantity);
                var discount = Math.Round(line.Discount * covered / Math.Max(1, line.Quantity), 2, MidpointRounding.AwayFromZero);
                products.Add(line with { Quantity = covered, Discount = discount });
                var index = PrescriptionItems.IndexOf(line);
                if (covered == line.Quantity) PrescriptionItems.RemoveAt(index);
                else PrescriptionItems[index] = line with { Quantity = line.Quantity - covered, Discount = line.Discount - discount };
                remaining -= covered;
            }
            if (products.Count > 0) item.Description = string.Join(" | ", products.Select(BuildPrescriptionLineDescription).Distinct());
        }
        else
        {
            foreach (var line in ServiceItems.Where(line => line.SourceId == item.InventoryId).ToList())
            {
                if (remaining <= 0) break;
                var covered = Math.Min(remaining, line.Quantity);
                var discount = Math.Round(line.Discount * covered / Math.Max(1, line.Quantity), 2, MidpointRounding.AwayFromZero);
                services.Add(line with { Quantity = covered, Discount = discount });
                var index = ServiceItems.IndexOf(line);
                if (covered == line.Quantity) ServiceItems.RemoveAt(index);
                else ServiceItems[index] = line with { Quantity = line.Quantity - covered, Discount = line.Discount - discount };
                remaining -= covered;
            }
            item.Remarks = string.Join(", ", services.Select(line => line.Staff).Where(staff => !string.IsNullOrWhiteSpace(staff)).Distinct());
        }
        packageReplacements[item] = (products, services);
        BillingPackageItems.Add(item);
        ResetPackagePaymentState();
    }

    private void RemoveBillingPackageItem(TransactionItem item)
    {
        if (IsAppointmentPaymentLocked || _isProcessingPayment) return;
        if (packageReplacements.Remove(item, out var replaced))
        {
            PrescriptionItems.AddRange(replaced.Products);
            ServiceItems.AddRange(replaced.Services);
        }
        BillingPackageItems.Remove(item);
        ResetPackagePaymentState();
    }

    private void ChangeBillingPackageQuantity(TransactionItem item, int delta)
    {
        if (IsAppointmentPaymentLocked || _isProcessingPayment || item.IsPackageRedemption || item.Quantity + delta < 1) return;
        item.Quantity += delta;
        ResetPackagePaymentState();
    }

    private void ResetPackagePaymentState()
    {
        BillDiscount = Math.Min(BillDiscount, Math.Max(0m,
            PrescriptionItems.Sum(item => item.LineTotal) + ServiceItems.Sum(item => item.LineTotal) + MemberCreditItems.Sum(item => item.LineTotal) +
            BillingPackageItems.Where(item => !item.IsPackageRedemption).Sum(item => item.UnitPrice * item.Quantity)));
        BillingPayments.Clear();
        _showConfirmPaymentModal = false;
        _showInsufficientAmountModal = false;
        _billingCalculatorMethod = null;
        _paymentValidationError = null;
        SynchronizeBillingPaymentTotals();
    }

    private void ClearBillingPackages()
    {
        BillingPackageItems.Clear(); packageReplacements.Clear(); pendingPackageRedemption = null;
        CloseCustomerPackages();
    }

    private static decimal PackageLineTotal(TransactionItem item)
    {
        OrderLineTaxCalculator.ComputeLineAmounts(item.UnitPrice, item.Quantity, item.Discount, item.TaxPercentage, item.IsTaxInclusive, out var beforeTax, out var tax);
        return beforeTax + tax;
    }

    private static TransactionItem CopyBillingPackageItem(TransactionItem item) => new()
    {
        InventoryId = item.InventoryId, Name = item.Name, Description = item.Description, Sku = item.Sku,
        Category = item.Category, InventoryTypeId = item.InventoryTypeId, ImageUrl = item.ImageUrl,
        Quantity = item.Quantity, UnitPrice = item.UnitPrice, Discount = item.Discount,
        TotalPrice = PackageLineTotal(item), UnitOfMeasureId = item.UnitOfMeasureId,
        TaxCodeId = item.TaxCodeId, TaxPercentage = item.TaxPercentage, IsTaxInclusive = item.IsTaxInclusive,
        TaxAmount = CalculateBillingLineAmounts(item.UnitPrice, item.Quantity, item.Discount, item.TaxPercentage, item.IsTaxInclusive).Tax,
        SourceDocumentLineId = item.SourceDocumentLineId, KitMemberId = item.KitMemberId,
        OriginalKitPrice = item.OriginalKitPrice, UnitActualValue = item.UnitActualValue,
        ActivityTypeId = item.ActivityTypeId, Remarks = item.Remarks
    };
}
