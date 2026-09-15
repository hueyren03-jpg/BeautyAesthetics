using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class POSPaymentLineTypeItem
    {

        [JsonPropertyName("POSPaymentTypeID")]
        public int POSPaymentTypeID { get; set; }

        [JsonPropertyName("POSPaymentTypeName")]
        public string POSPaymentTypeName { get; set; } = string.Empty;

        [JsonPropertyName("Grouping")]
        public string? Grouping { get; set; }

        [JsonPropertyName("FinancialAccountID")]
        public string? FinancialAccountID { get; set; }

        [JsonPropertyName("ARAPOutstandingTypeID")]
        public int ARAPOutstandingTypeID { get; set; }

        [JsonPropertyName("VisibleInModules")]
        public string? VisibleInModules { get; set; }

        [JsonPropertyName("VisibleInBranch")]
        public string? VisibleInBranch { get; set; }

        [JsonPropertyName("VisibleInGroup")]
        public string? VisibleInGroup { get; set; }

        [JsonPropertyName("Active")]
        public bool Active { get; set; }

        [JsonPropertyName("FinancialAccountName")]
        public string? FinancialAccountName { get; set; }

        [JsonPropertyName("PicturePath")]
        public string? PicturePath { get; set; }

        [JsonPropertyName("Balance")]
        public decimal? Balance { get; set; }

        [JsonPropertyName("ShowBalance")]
        public bool ShowBalance { get; set; }

        [JsonPropertyName("MerchantCode")]
        public string? MerchantCode { get; set; }

        [JsonPropertyName("MerchantKey")]
        public string? MerchantKey { get; set; }

        [JsonPropertyName("TerminalID")]
        public string? TerminalID { get; set; }

        [JsonPropertyName("PaymentIntegrationType")]
        public int PaymentIntegrationType { get; set; }

    }

    public class POSPaymentLineTypeRequest
    {
        public string? ID { get; set; }
        public string? BranchID { get; set; }
        public string? GroupID { get; set; }
        //public string? BranchIDs { get; set; } = null;
        //public string? GroupIDs { get; set; } = null;
        //public DateTime EndDate { get; set; } = DateTime.MinValue;
        //public DateTime FinancialDate { get; set; } = DateTime.MinValue;
        //public DateTime NewExpiryDate { get; set; } = DateTime.MinValue;
        //public DateTime RedemptionEndDate { get; set; } = DateTime.MinValue;
        //public int MinQuantityBalanceToShow { get; set; } = 0;
    }
}
