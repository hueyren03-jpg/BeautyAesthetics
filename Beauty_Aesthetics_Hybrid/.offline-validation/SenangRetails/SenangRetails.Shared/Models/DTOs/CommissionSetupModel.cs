using System;
using System.Collections.Generic;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CommissionSchemeModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<CommissionLineItemModel> Lines { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class CommissionLineItemModel
    {
        public string LineId { get; set; } = Guid.NewGuid().ToString();
        public string InEvent { get; set; } = "Sales"; // Sales, Service, Redemption
        public string Type { get; set; } = "Percentage"; // Percentage, Fixed Amount, By Range
        public string Description { get; set; } = string.Empty;
        public string AllocationType { get; set; } = "Standard";
        public bool UseRetailPrice { get; set; } = false;
        public decimal AllocationAmt { get; set; } = 0.00m;
        public string ByRangeDetails { get; set; } = "Default Range";
        public decimal SharingPercent { get; set; } = 0.00m;
        public bool IsBalance { get; set; } = false;
        public bool IsPaid { get; set; } = true;
        public bool NotForStaff { get; set; } = false;
        public string DeductionType { get; set; } = "None";
        public string XRange { get; set; } = string.Empty;
        public decimal DeductionAmt { get; set; } = 0.00m;
        public string AllocationGroup { get; set; } = string.Empty;
        public string DiscountSetting { get; set; } = "Standard Discount";
        public bool IsSelected { get; set; } = false; // Row selection UI state
    }
}
