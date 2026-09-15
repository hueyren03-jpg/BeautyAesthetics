using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class MembershipTypeRequestDTO
    {
        //public bool isLoading { get; set; }
        public string memberTypeID { get; set; }
        public string memberTypeName { get; set; }
        //public int? membershipValidDays { get; set; }
        //public int? memberDiscountOnInventory { get; set; }
        //public int? memberDiscountOnServices { get; set; }
        //public int? memberDiscountOnPackage { get; set; }
        //public int? memberDiscountOnBundle { get; set; }
        //public string applicableToItems { get; set; }
        //public string applicableToItemGroup { get; set; }
        //public string applicableToItemBrand { get; set; }
        public bool active { get; set; }
        //public bool? isDiscountLimitToCreditRedemption { get; set; }
        //public string? discountTimeFrom { get; set; }
        //public string discountTimeTo { get; set; }
        public string saveAction { get; set; }
    }
}
