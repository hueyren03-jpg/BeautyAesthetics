using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CustomerServiceRecordRequest
    {
        public string customerId { get; set; } = string.Empty;
        public int year { get; set; }
        public int month { get; set; }
    }
}
