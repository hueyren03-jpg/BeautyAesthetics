using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SenangRetails.Shared.ApiClient
{
    public class ARReceiptAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public ARReceiptAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<List<ud_ARAPPaymentOffSetLineDM>>?> RetrieveSettlementLinesAsync(string customerId, string branchGroupId)
        {
            if (!await SetBearerToken()) return null;

            var request = new
            {
                customerID = customerId,
                documentID = "all",
                branchGroupID = branchGroupId
            };

            return await PostAsync<object, ApiResponseRoot<List<ud_ARAPPaymentOffSetLineDM>>>(
                "api/Doc_ARReceipt/RetrieveSettlementLines",
                request);
        }

        public async Task<ApiResponseRoot<object>?> CreateARReceiptRecordAsync(Doc_ARReceipt request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<Doc_ARReceipt, ApiResponseRoot<object>>(
                "api/Doc_ARReceipt/CreateRecord",
                request);
        }
    }
}
