using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersBalanceSummary;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using static SenangRetails.Shared.ApiClient.CustomerAC;
using EBI.DM;

namespace SenangRetails.Shared.Services.CustomerService
{
    public interface ICustomerService
    {
        Task<List<CustomerDM>> SearchCustomer(string keyword);
        Task<CustomerDM> GetSingleCustomer(string keyword);
        Task<ApiResponseRoot<CustomerCreateSuccess>?> CreateCustomer(CustomerDM customer);
        Task<ApiResponseRoot<string>?> UpdateCustomer(CustomerDM customer);
        Task<ApiResponseRoot<CreditResponseDTO>> GetCreditBalance(string id);
        Task<MemberBalanceSummaryResult?> GetMemberBalanceSummaryAsync(string customerId);
        Task<List<MemberOtherBalanceSummaryResult>?> GetMemberOtherBalanceSummaryAsync(string customerId, DateTime cutOffDate);
        Task<ApiResponseRoot<Dictionary<string, List<CustomerServiceRecordsDM>>>?> GetCustomerServiceRecordByMonthAsync(string customerId, int year, int month);

    }
}