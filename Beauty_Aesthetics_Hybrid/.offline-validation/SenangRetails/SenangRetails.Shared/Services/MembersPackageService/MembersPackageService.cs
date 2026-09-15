using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs.MembersPackage;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.MembersPackageService
{
    public class MembersPackageService : IMembersPackageService
    {
        private readonly MembersPackageAC _ac;

        public MembersPackageService(MembersPackageAC ac)
        {
            _ac = ac;
        }

        public async Task<List<CashSales_Series_UnconsumedItemDM>> GetPackageBalanceByCustomerIDAsync(string customerId)
        {
            var request = new GetPackageBalanceRequest
            {
                id = customerId
            };

            var response = await _ac.GetPackageBalanceDetails(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return new List<CashSales_Series_UnconsumedItemDM>();
        }

        public async Task<List<CashSales_Series_UnconsumedItemDM>> GetRedemptionHistoryAsync(string packageAutoId)
        {
            var request = new RedemptionHistoryRequest { id = packageAutoId };
            var response = await _ac.GetRedemptionHistory(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return new List<CashSales_Series_UnconsumedItemDM>();
        }
    }
}
