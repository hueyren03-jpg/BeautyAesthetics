using System;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CashDrawerLogModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = "Cash In"; // "Cash In" or "Cash Out"
        public decimal Amount { get; set; }
        public string Reason { get; set; } = "Starting Float";
        public string Notes { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = "Staff";
        public string Branch { get; set; } = "HQ";
        public string Counter { get; set; } = "Counter 1";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class CashDrawerSummaryModel
    {
        public decimal OpeningFloat { get; set; } = 300.00m;
        public decimal TotalCashInToday { get; set; }
        public decimal TotalCashOutToday { get; set; }
        public decimal CurrentDrawerBalance => OpeningFloat + TotalCashInToday - TotalCashOutToday;
    }
}
