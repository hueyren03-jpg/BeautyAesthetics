using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersCredit;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.MembersCreditService
{
    public class MembersCreditService : IMembersCreditService
    {
        private readonly MembersCreditAC _ac;

        public MembersCreditService(MembersCreditAC ac)
        {
            _ac = ac;
        }
        public async Task<List<ARAPOutstanding_MemberCreditDM>> GetCustomerCreditDetailsAsync(string customerId)
        {
            var request = new GetCreditBalanceDetailsRequest { id = customerId };
            var response = await _ac.GetCreditBalanceDetails(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result
                    .Where(x => x.DueDate.Year < 2000 || x.DueDate >= DateTime.Today)
                    .OrderBy(x => x.ARAPOutstandingID)
                    .ToList();
            }

            return new List<ARAPOutstanding_MemberCreditDM>();
        }

        public async Task<List<ARAPOutstandingDM>> GetCreditRedemptionHistoryAsync(string arapId)
        {
            var request = new CreditRedemptionHistoryRequest { id = arapId };
            var response = await _ac.GetCreditRedemptionHistory(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return new List<ARAPOutstandingDM>();
        }

        public async Task<Dictionary<string, CustomerDetailsDto>> GetOutstandingBalanceAsync(string id, DateTime startDate, string balanceType)
        {
            var request = new RequestOutstandingBalanceDto
            {
                id = id,
                startDate = startDate,
                balanceType = balanceType
            };
            var response = await _ac.GetOutstandingBalance(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return new Dictionary<string, CustomerDetailsDto>();
        }
    }
}