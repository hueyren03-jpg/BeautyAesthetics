using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CustomerDetailsDto
    {
        [JsonPropertyName("CustomerID")]
        public string CustomerID { get; set; }

        [JsonPropertyName("CustomerCode")]
        public string CustomerCode { get; set; }

        [JsonPropertyName("CustomerName")]
        public string CustomerName { get; set; }

        [JsonPropertyName("Phone")]
        public string Phone { get; set; }

        [JsonPropertyName("lstTransaction")]
        public List<TransactionRecordDto> Transactions { get; set; }
    }
}
