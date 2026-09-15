namespace Beauty_Aesthetics_WebPos.Components.Models.Voucher
{
    public class Voucher
    {
        // =============================================================================
        //                              Voucher Page
        // =============================================================================
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalQuantity { get; set; }
        public int ExpiryDays { get; set; }
        public string DiscountRange { get; set; } = string.Empty;
        public string AmountRange { get; set; } = string.Empty;


        // =============================================================================
        //                              Voucher Details Page
        // =============================================================================
        public int Available { get; set; }
        public int Sold { get; set; }
        public int Redeemed { get; set; }
        public int Expired { get; set; }


        // =============================================================================
        //                      Edit Voucher Page (Generated Vouchers)
        // =============================================================================

        public string VoucherNo { get; set; } = string.Empty;
        public decimal VoucherAmount { get; set; }
        public decimal SalesAmount { get; set; }
        public string DiscountType { get; set; } = string.Empty ;
        public int DiscountValue { get; set; }
        public bool ImmediateRedeemable { get; set; }
        public bool IsBlock { get; set; }
        public string CanSellBranch { get; set; } = "ALL";
        public string CanRedeemBranch { get; set; } = "ALL";
        public string SalesOutlet { get; set; } = string.Empty;
        public string RedeemOutlet { get; set; } = string.Empty;

        public DateTime? SalesDate { get; set; }
        public DateTime? RedeemDate { get; set; }

        public decimal? SalesValue { get; set; }
        public decimal? RedeemValue { get; set; }

        public string SalesDocNo { get; set; } = string.Empty;
        public string RedeemDocNo { get; set; } = string.Empty;
    }
}
