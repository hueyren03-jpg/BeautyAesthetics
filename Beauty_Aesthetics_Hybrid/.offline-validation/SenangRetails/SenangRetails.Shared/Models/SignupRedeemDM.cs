using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class SignupRedeemDM
    {
        public string Name { get; set; } = " ";
        public DateTime DateTime { get; set; } = DateTime.Now;
        public MemberDetailsDM Member { get; set; } = new MemberDetailsDM();
        public string Category { get; set; } = " "; // Credit, Voucher, Point
        public decimal Amount { get; set; }

    }

    public class SignupDM : SignupRedeemDM
    {
    }

    public class RedeemDM : SignupRedeemDM
    {
    }
}
