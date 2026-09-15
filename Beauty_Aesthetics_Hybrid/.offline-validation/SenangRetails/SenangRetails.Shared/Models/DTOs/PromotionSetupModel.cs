using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class PromotionSetupModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PromoType { get; set; } = "Percentage"; // "Percentage" or "Fixed Price"
        public string PromoMethod { get; set; } = "Discount %";
        public string Rounding { get; set; } = "No Rounding";
        public string PromoCondition { get; set; } = "Total Price After Discount, Disc To All Items";
        public decimal DiscountValue { get; set; } = 0.00m;
        public DateTime? StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; } = DateTime.Today.AddMonths(1);
        public int MinQuantity { get; set; } = 1;
        public int MaxLimitPerOrder { get; set; } = 0; // 0 = Unlimited
        public bool IsActive { get; set; } = true;
        public string Remarks { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> AppliedProductIds { get; set; } = new();
    }
}
