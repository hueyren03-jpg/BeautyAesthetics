using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient
{
    public class GSTTaxCodeAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public GSTTaxCodeAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }


        public async Task<ApiResponseRoot<CreateResponse>?> CreateRecord(GSTTaxCodeDM request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<GSTTaxCodeDM, ApiResponseRoot<CreateResponse>>(
                "api/GSTTaxCode/CreateRecord",
                request);
        }

        public async Task<ApiResponseRoot<string>?> UpdateRecord(GSTTaxCodeDM request)
        {
            if (!await SetBearerToken()) return null;

            return await PutAsync<GSTTaxCodeDM, ApiResponseRoot<string>>(
                "api/GSTTaxCode/UpdateRecord",
                request);
        }

        public async Task<ApiResponseRoot<string>?> Delete(string strID)
        {
            if (!await SetBearerToken()) return null;

            return await DeleteAsync<ApiResponseRoot<string>>($"api/GSTTaxCode/Delete?id={strID}");

            //return await PutAsync<object, ApiResponseRoot<string>>(
            //    "api/GSTTaxCode/Delete",
            //    new { Id = strID });
        }

        public async Task<ApiResponseRoot<string>?> DeleteByParentID(string strID)
        {
            if (!await SetBearerToken()) return null;

            return await PutAsync<object, ApiResponseRoot<string>>(
                "api/GSTTaxCode/DeleteByParentID",
                new { Id = strID });
        }

        public async Task<Dictionary<string, GSTTaxCodeDM>> LoadDictionaryByParentID(string strTaxTypeID)
        {
            if (!await SetBearerToken()) return new Dictionary<string, GSTTaxCodeDM>();

            var apiResponse = await PostAsync<object, ApiResponseRoot<Dictionary<string, GSTTaxCodeDM>>>(
                "api/GSTTaxCode/LoadDictionaryByParentID",
                new { Id = strTaxTypeID });
            return apiResponse?.result ?? new Dictionary<string, GSTTaxCodeDM>();
        }

        public async Task<ApiResponseRoot<List<GstTaxCodeDto>>?> LoadProxyByParentID(string taxTypeID)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<GstTaxCodeDto>>>(
                "api/GSTTaxCode/LoadProxyByParentID", new { id = taxTypeID });
        }
    }
}
