using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs.MembersCredit
{
    public class CreditRedemptionHistoryResult
    {
        public string ARAPOutstandingID { get; set; } = string.Empty;
        public DateTime FinancialDate { get; set; }
        public string DisplayCode { get; set; } = string.Empty;
        public string SourceARAPOutstandingID { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; } 
        public string BranchID { get; set; } = string.Empty;
    }
}
