using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class MembershipTypeResponseDTO
    {
        public bool IsLoading { get; set; }
        public string MemberTypeID { get; set; } = string.Empty;
        public string MemberTypeName { get; set; } = string.Empty;
        public int MembershipValidDays { get; set; }
        public double MemberDiscountOnInventory { get; set; }
        public double MemberDiscountOnServices { get; set; }
        public double MemberDiscountOnPackage { get; set; }
        public double MemberDiscountOnBundle { get; set; }
        public string ApplicableToItems { get; set; }
        public string ApplicableToItemGroup { get; set; }
        public string ApplicableToItemBrand { get; set; }
        public bool Active { get; set; }
        public bool IsDiscountLimitToCreditRedemption { get; set; }
        public string DiscountTimeFrom { get; set; }
        public string DiscountTimeTo { get; set; }
        public int SaveAction { get; set; }
        public bool IsDirty { get; set; }
        public List<string> lstBrand { get; set; }
        public List<string> lstGroup { get; set; }
        public List<string> lstItem { get; set; }
    }
}
