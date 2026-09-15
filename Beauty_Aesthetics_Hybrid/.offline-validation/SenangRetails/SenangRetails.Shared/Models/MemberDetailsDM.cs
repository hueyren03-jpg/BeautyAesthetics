using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class MemberDetailsDM
    {
        public string MemberId { get; set; } = "-";

        public string Name { get; set; } = "-";

        public string PhoneNumber { get; set; } = "-";

        public string Sex { get; set; } = "-";

        public DateTime DateRegistered { get; set; } = DateTime.Now;

        public string PhotoUrl { get; set; } = "";

        public decimal Credit { get; set; } = 0.00m;

        public int Point { get; set; } = 0;
    }
}
