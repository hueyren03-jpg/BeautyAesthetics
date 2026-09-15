using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersPackage;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class MembersPackageAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public MembersPackageAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<List<CashSales_Series_UnconsumedItemDM>>?> GetPackageBalanceDetails(GetPackageBalanceRequest request)
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;
            CreateBearerAuthAsync(token);

            return await PostAsync<GetPackageBalanceRequest, ApiResponseRoot<List<CashSales_Series_UnconsumedItemDM>>>(
                "api/Customer/GetPackageBalanceDetails",
                request);
        }

        public async Task<ApiResponseRoot<List<CashSales_Series_UnconsumedItemDM>>?> GetRedemptionHistory(RedemptionHistoryRequest request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<RedemptionHistoryRequest, ApiResponseRoot<List<CashSales_Series_UnconsumedItemDM>>>(
                "api/CashSales_Series_UnconsumedItem/GetRedemptionHistory",
                request);
        }
    }
}
