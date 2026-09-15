using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs.MembersPackage
{
    public class RedemptionHistoryResult
    {
        public string AutoID { get; set; } = string.Empty;
        public DateTime FinancialDate { get; set; }
        public string DocumentID { get; set; } = string.Empty;
        public string DisplayCode { get; set; } = string.Empty; 
        public string SourceDocumentLineID { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal QuantityRedeemed { get; set; }
        public decimal UnitActualValue { get; set; }
        public decimal TotalActualValue { get; set; }
        public string BranchID { get; set; } = string.Empty;
    }
}
