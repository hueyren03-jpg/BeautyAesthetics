using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class TransactionRecordDto
    {
        [JsonPropertyName("DocumentID")]
        public string DocumentID { get; set; }

        [JsonPropertyName("FinancialDate")]
        public DateTime FinancialDate { get; set; }

        [JsonPropertyName("BillNo")]
        public string BillNo { get; set; }

        // Using decimal is best practice for financial amounts and balances
        [JsonPropertyName("Amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("Balance")]
        public decimal Balance { get; set; }
    }
}
