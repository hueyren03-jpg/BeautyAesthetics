using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersCredit;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class MembersCreditAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public MembersCreditAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }
        public async Task<ApiResponseRoot<List<ARAPOutstanding_MemberCreditDM>>?> GetCreditBalanceDetails(GetCreditBalanceDetailsRequest request)
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;
            CreateBearerAuthAsync(token);

            return await PostAsync<GetCreditBalanceDetailsRequest, ApiResponseRoot<List<ARAPOutstanding_MemberCreditDM>>>(
                "api/Customer/GetCreditBalanceDetails",
                request);
        }

        public async Task<ApiResponseRoot<List<ARAPOutstandingDM>>?> GetCreditRedemptionHistory(CreditRedemptionHistoryRequest request)
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;
            CreateBearerAuthAsync(token);

            return await PostAsync<CreditRedemptionHistoryRequest, ApiResponseRoot<List<ARAPOutstandingDM>>>(
                "api/ARAPOutstanding_MemberCredit/GetRedemptionHistory",
                request);
        }

        public async Task<ApiResponseRoot<Dictionary<string, CustomerDetailsDto>>> GetOutstandingBalance(RequestOutstandingBalanceDto request)
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;
            CreateBearerAuthAsync(token);
            var result = await PostAsync<RequestOutstandingBalanceDto, ApiResponseRoot<Dictionary<string, CustomerDetailsDto>>>(
                "api/WebDashboard/GetMemberOtherBalanceDetail",
                request);
            if (result != null && result.statusCode == 200)
            {
                return result;
            }
            result = new();
            return result;
        }

        public async Task<string?> LoadProxyByParentIDRawAsync(string parentId)
        {
            if (!await SetBearerToken()) return null;
            var req = new GetCreditBalanceDetailsRequest { id = parentId };
            await PostAsync<GetCreditBalanceDetailsRequest, object>("api/ARAPOutstanding_MemberCredit/LoadProxyByParentID", req);
            return LastPostResponseBody;
        }

        public async Task<ApiResponseRoot<object>?> SaveMemberCreditAsync(SaveMemberCreditRequest request)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<SaveMemberCreditRequest, ApiResponseRoot<object>>(
                "api/ARAPOutstanding_MemberCredit/Save", request);
        }
    }
}