using System;
using System.Collections.Generic;

namespace SenangRetails.Shared.Models.DTOs
{
    public class PurchaseOrderItemModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitCost { get; set; }
        public decimal TotalAmount => Quantity * UnitCost;
    }

    public class PurchaseOrderModel
    {
        public string PoNo { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Today;
        public string Branch { get; set; } = "HQ";
        public string Vendor { get; set; } = string.Empty;
        public string PoType { get; set; } = "Standard"; // Standard, Express, Consignment, Blanket
        public string PaymentTerm { get; set; } = "Net 30"; // Net 30, Net 60, COD, Immediate
        public DateTime ExpectedDelivery { get; set; } = DateTime.Today.AddDays(7);
        public string Status { get; set; } = "Issued"; // Draft, Issued, Pending Delivery, Partially Received, Completed, Cancelled
        public string Remarks { get; set; } = string.Empty;
        public List<PurchaseOrderItemModel> Items { get; set; } = new();

        public decimal TotalAmount
        {
            get
            {
                decimal sum = 0;
                if (Items != null)
                {
                    foreach (var item in Items)
                        sum += item.TotalAmount;
                }
                return sum;
            }
        }
    }
}
