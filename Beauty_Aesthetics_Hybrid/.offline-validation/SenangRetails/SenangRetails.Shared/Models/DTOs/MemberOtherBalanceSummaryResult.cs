using System;

namespace SenangRetails.Shared.Models.DTOs.MembersBalanceSummary
{
    public class MemberOtherBalanceSummaryResult
    {
        public string CustomerID { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal Outstanding { get; set; }
        public decimal Point { get; set; }
        public decimal PointRebate { get; set; }
        public decimal MGM { get; set; }
        public decimal Deposit { get; set; }
    }
}
