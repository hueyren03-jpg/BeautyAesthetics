using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersCredit;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.MembersCreditService
{
    public interface IMembersCreditService
    {
        Task<List<ARAPOutstanding_MemberCreditDM>> GetCustomerCreditDetailsAsync(string customerId);
        Task<List<ARAPOutstandingDM>> GetCreditRedemptionHistoryAsync(string arapId);
        Task<Dictionary<string, CustomerDetailsDto>> GetOutstandingBalanceAsync(string id, DateTime startDate, string balanceType);
    }
}