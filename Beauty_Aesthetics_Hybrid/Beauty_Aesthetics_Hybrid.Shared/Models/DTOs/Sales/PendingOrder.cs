using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Tax;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class PendingOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsHeld { get; set; }
    public bool IsRedemptionMode { get; set; }

    public string AccountId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerContact { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string MembershipType { get; set; } = string.Empty;

    public string BranchId { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;

    public List<TransactionItem> Items { get; set; } = new();
    public List<TransactionPayment> Payments { get; set; } = new();
    public string Notes { get; set; } = string.Empty;

    public decimal Subtotal => Items.Sum(x => Math.Max(1, x.Quantity) * x.UnitPrice);
    public decimal Discount => Items.Sum(x => Math.Clamp(x.Discount, 0, Math.Max(1, x.Quantity) * x.UnitPrice));
    public decimal SubtotalBeforeTax => Items.Sum(x => CalculateLine(x).BeforeTax);
    public decimal Tax => Items.Sum(x => CalculateLine(x).Tax);
    public decimal Total => Math.Max(0m, Items.Sum(x =>
    {
        var line = CalculateLine(x);
        return line.BeforeTax + line.Tax;
    }));
    public int ItemCount => Items.Sum(x => Math.Max(1, x.Quantity));
    public decimal PaidAmount => Payments.Sum(x => x.Amount);
    public decimal Remaining => Math.Max(0, Total - PaidAmount);

    private static (decimal BeforeTax, decimal Tax) CalculateLine(TransactionItem item)
    {
        OrderLineTaxCalculator.ComputeLineAmounts(
            item.UnitPrice,
            Math.Max(1, item.Quantity),
            item.Discount,
            item.TaxPercentage,
            item.IsTaxInclusive,
            out var beforeTax,
            out var tax);
        return (beforeTax, tax);
    }

    public PendingOrder Clone()
    {
        return new PendingOrder
        {
            Id = Id,
            OrderNumber = OrderNumber,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTime.Now,
            IsHeld = IsHeld,
            IsRedemptionMode = IsRedemptionMode,
            AccountId = AccountId,
            CustomerName = CustomerName,
            CustomerContact = CustomerContact,
            CustomerEmail = CustomerEmail,
            MembershipType = MembershipType,
            BranchId = BranchId,
            BranchName = BranchName,
            Notes = Notes,
            Items = Items.Select(i => new TransactionItem
            {
                Id = i.Id,
                InventoryId = i.InventoryId,
                Name = i.Name,
                Sku = i.Sku,
                Description = i.Description,
                ImageUrl = i.ImageUrl,
                Category = i.Category,
                InventoryTypeId = i.InventoryTypeId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Discount = i.Discount,
                CashDiscountId = i.CashDiscountId,
                DiscountMemo = i.DiscountMemo,
                TotalPrice = i.TotalPrice,
                UnitOfMeasureId = i.UnitOfMeasureId,
                TaxCodeId = i.TaxCodeId,
                TaxPercentage = i.TaxPercentage,
                TaxAmount = i.TaxAmount,
                IsTaxInclusive = i.IsTaxInclusive,
                ActivityTypeId = i.ActivityTypeId,
                MemberCreditAccountId = i.MemberCreditAccountId,
                MemberTypeId = i.MemberTypeId,
                MembershipCredit = i.MembershipCredit,
                MemberCreditAllocations = i.MemberCreditAllocations
                    .Select(allocation => new MemberCreditAllocation
                    {
                        MemberCreditAccountId = allocation.MemberCreditAccountId,
                        MemberTypeId = allocation.MemberTypeId,
                        Amount = allocation.Amount
                    })
                    .ToList()
            }).ToList(),
            Payments = Payments.Select(p => new TransactionPayment
            {
                ReceiptLineId = p.ReceiptLineId,
                PaymentTypeId = p.PaymentTypeId,
                PaymentMethod = p.PaymentMethod,
                SourceDocumentLineId = p.SourceDocumentLineId,
                Amount = p.Amount,
                ChangeAmount = p.ChangeAmount,
                FinancialAccountId = p.FinancialAccountId,
                BankName = p.BankName
            }).ToList()
        };
    }
}
