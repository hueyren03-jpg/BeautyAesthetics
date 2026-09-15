using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs.MembersPackage
{
    public class PackageBalanceResult
    {
        public string AutoID { get; set; } = string.Empty;
        public DateTime FinancialDate { get; set; }
        public string DocumentID { get; set; } = string.Empty;
        public string DisplayCode { get; set; } = string.Empty;
        public string DocumentLineID { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InventoryID { get; set; } = string.Empty;
        public decimal QuantityPurchased { get; set; }
        public decimal QuantityRedeemed { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string PackageCode { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal QuantityAvailable { get; set; }
        public decimal NetBalanceAfterUtilised { get; set; } // actual balance
        public decimal UnitActualValue { get; set; }
        public decimal BalanceAVValue { get; set; }
        public string BranchID { get; set; } = string.Empty;
        public bool IsRedeemable { get; set; }
    }
}
