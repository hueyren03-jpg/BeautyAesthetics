using System;

namespace SenangRetails.Shared.Models.DTOs.MembersBalanceSummary
{
    public class MemberOtherBalanceSummaryRequest
    {
        public string id { get; set; } = string.Empty;
        public DateTime cutOffDate { get; set; }
    }
}
