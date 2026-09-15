using Beauty_Aesthetics_WebPos.Components.Models;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class PendingOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

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
    public decimal Total => Math.Max(0, Subtotal - Discount);
    public int ItemCount => Items.Sum(x => Math.Max(1, x.Quantity));
    public decimal PaidAmount => Payments.Sum(x => x.Amount);
    public decimal Remaining => Math.Max(0, Total - PaidAmount);

    public PendingOrder Clone()
    {
        return new PendingOrder
        {
            Id = Id,
            OrderNumber = OrderNumber,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTime.Now,
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
                Description = i.Description,
                ImageUrl = i.ImageUrl,
                Category = i.Category,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Discount = i.Discount,
                TotalPrice = i.TotalPrice
            }).ToList(),
            Payments = Payments.Select(p => new TransactionPayment
            {
                ReceiptLineId = p.ReceiptLineId,
                PaymentTypeId = p.PaymentTypeId,
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount
            }).ToList()
        };
    }
}
