using System;
using System.Collections.Generic;

namespace SenangRetails.Shared.Models.DTOs
{
    public class PriceGroupModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0.00m;
        public decimal MlmCommRate { get; set; }
        public decimal MlmSalesTarget { get; set; }
        public List<string> VisibleBranchIds { get; set; } = new();
        public List<string> AppliedProductIds { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
