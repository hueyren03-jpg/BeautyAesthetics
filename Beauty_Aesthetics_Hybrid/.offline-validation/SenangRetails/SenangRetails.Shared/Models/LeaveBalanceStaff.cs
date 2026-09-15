using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class LeaveBalanceStaff
    {
        public string Name { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public string EmploymentPeriod { get; set; } = string.Empty;
        public int Entitled { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Balance { get; set; }
    }
}
