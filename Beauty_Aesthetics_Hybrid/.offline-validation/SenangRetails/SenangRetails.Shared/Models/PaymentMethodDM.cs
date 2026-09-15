using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class PaymentMethodDM
    {
        public string MethodName { get; set; } = "";

        public decimal Amount { get; set; } = 0.00m;

        public string ReferenceNo { get; set; } = "";

    }
}
