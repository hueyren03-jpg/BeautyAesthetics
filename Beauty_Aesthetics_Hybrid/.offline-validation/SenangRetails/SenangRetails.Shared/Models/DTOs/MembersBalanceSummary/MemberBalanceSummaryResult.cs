using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs.MembersBalanceSummary
{
    public class MemberBalanceSummaryResult
    {
        public decimal PackageBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public decimal PointBalance { get; set; }
        public decimal PointRebateBalance { get; set; }
        public decimal VoucherBalance { get; set; }
    }
}
