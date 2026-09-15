using EBI.DM;
using SenangRetails.Shared.Models.DTOs.MembersPackage;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.MembersPackageService
{
    public interface IMembersPackageService
    {
        Task<List<CashSales_Series_UnconsumedItemDM>> GetPackageBalanceByCustomerIDAsync(string customerId);
        Task<List<CashSales_Series_UnconsumedItemDM>> GetRedemptionHistoryAsync(string packageAutoId);
    }
}
