using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class RequestOutstandingBalanceDto
    {
        public string id { get; set; } = string.Empty;
        public DateTime startDate { get; set; }
        public string balanceType { get; set; } = string.Empty;
    }
}
