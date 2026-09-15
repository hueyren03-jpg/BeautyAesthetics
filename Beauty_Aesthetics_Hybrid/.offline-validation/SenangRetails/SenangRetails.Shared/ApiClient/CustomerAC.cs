using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersBalanceSummary;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using EBI.DM;

namespace SenangRetails.Shared.ApiClient
{
    public class CustomerAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public CustomerAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<List<CustomerDM>> SearchCustomersAsync(string keyword)
        {
            if (!await SetBearerToken()) return new List<CustomerDM>();

            var apiResponse = await PostAsync<object, ApiResponseRoot<List<CustomerDM>>>(
                "api/Customer/SearchByWord",
                new { Id = keyword });

            return apiResponse?.result ?? new List<CustomerDM>();
        }

        public async Task<CustomerDM> GetSingleCustomerAsync(string keyword)
        {
            if (!await SetBearerToken()) return new CustomerDM();

            var apiResponse = await PostAsync<object, ApiResponseRoot<CustomerDM>>(
                "api/Customer/LoadRecord",
                new { id = keyword });

            return apiResponse?.result ?? new CustomerDM();
        }

        public async Task<ApiResponseRoot<CustomerCreateSuccess>?> CreateCustomerAsync(CustomerDM customer)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<CustomerDM, ApiResponseRoot<CustomerCreateSuccess>>(
                "api/Customer/CreateRecord",
                customer);
        }

        public async Task<ApiResponseRoot<string>?> UpdateCustomerAsync(CustomerDM customer)
        {
            if (!await SetBearerToken()) return null;

            return await PutAsync<CustomerDM, ApiResponseRoot<string>>(
                "api/Customer/UpdateRecord",
                customer);
        }

        public async Task<ApiResponseRoot<CreditResponseDTO>> FetchCreditBalance(string id)
        {
            if (!await SetBearerToken()) return null;

            var request = new { id = id };

            return await PostAsync<object, ApiResponseRoot<CreditResponseDTO>>(
                "api/Customer/GetMemberBalanceSummary",
                    request
                );
        }

        public async Task<ApiResponseRoot<MemberBalanceSummaryResult>?> GetMemberBalanceSummary(MemberBalanceSummaryRequest request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<MemberBalanceSummaryRequest, ApiResponseRoot<MemberBalanceSummaryResult>>(
                "api/Customer/GetMemberBalanceSummary",
                request);
        }

        public async Task<ApiResponseRoot<List<MemberOtherBalanceSummaryResult>>?> GetMemberOtherBalanceSummary(MemberOtherBalanceSummaryRequest request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<MemberOtherBalanceSummaryRequest, ApiResponseRoot<List<MemberOtherBalanceSummaryResult>>>(
                "api/WebDashboard/GetMemberOtherBalanceSummary",
                request);
        }

        public async Task<ApiResponseRoot<Dictionary<string, List<CustomerServiceRecordsDM>>>?> GetCustomerServiceRecordByMonth(CustomerServiceRecordRequest request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<CustomerServiceRecordRequest, ApiResponseRoot<Dictionary<string, List<CustomerServiceRecordsDM>>>>(
                "api/Customer/GetCustomerServiceRecordByMonth",
                request);
        }


    }
}