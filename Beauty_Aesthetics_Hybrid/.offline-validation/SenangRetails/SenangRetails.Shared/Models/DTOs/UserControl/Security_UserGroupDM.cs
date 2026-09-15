using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs.UserControl
{
    public class Security_UserGroupDM
    {
        public bool IsLoading { get; set; }
        public string? UserGroupID { get; set; } 
        public string? UserGroupName { get; set; }
        public int PackageFirstExpiryExtensionDays { get; set; }
        public int PackageSecondExpiryExtensionDays { get; set; }
        public int CreditFirstExpiryExtensionDays { get; set; }
        public int CreditSecondExpiryExtensionDays { get; set; }
        public bool AllowBeyondSecondExtension { get; set; }
        public int MaxEditDayPOS { get; set; }
        public int VoucherFirstExpiryExtensionDays { get; set; }
        public int VoucherSecondExpiryExtensionDays { get; set; }
        public string? ExtensionDayBasis { get; set; }
        public int MaxEditDayCommission { get; set; }
        public string? VisibleToBranchGroup { get; set; } = string.Empty;
        public string? PointAdjustmentRight { get; set; } = string.Empty;
        public int SaveAction { get; set; }
        public bool IsDirty { get; set; }
    }
}
