using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient
{
    public class MembershipTypeAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public MembershipTypeAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponse<List<MembershipTypeDM>>?> GetMembershipTypesAsync()
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            return await PostAsync<object, ApiResponse<List<MembershipTypeDM>>>("api/MembershipType/GetAllMembershipTypes", null);
        }

        public async Task<ApiResponse<MembershipType?>?> LoadRecordAsync(string strMembershipTypeID)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            return await PostAsync<object, ApiResponse<MembershipType?>>("api/MembershipType/LoadRecord", new { id = strMembershipTypeID });
        }

        public async Task<ApiResponseRoot<MembershiptypeCreateResponseDTO>> CreateMembershipTypeAsync(MembershipType requestBody)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            requestBody.objMembershipType.SaveAction = EBI.Enum.EntityState.Added;
            return await PostAsync<MembershipType, ApiResponseRoot<MembershiptypeCreateResponseDTO>>("api/MembershipType/CreateRecord", requestBody);
        }

        public async Task<ApiResponseRoot<string>> PutMembershipTypeAsync(MembershipType requestBody)
        {
            var token = await _tokenService.GetTokenAsync();
            requestBody.objMembershipType.SaveAction = EBI.Enum.EntityState.Changed;
            var objSetting = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };
            var printDebug = System.Text.Json.JsonSerializer.Serialize(requestBody, objSetting);
            System.Diagnostics.Debug.Print(printDebug);
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            return await PutAsync<MembershipType, ApiResponseRoot<string>>("api/MembershipType/UpdateRecord", requestBody);
        }

        public async Task<ApiResponseRoot<string>> DeleteMembershipTypeAsync(string id)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            var requestBody = new
            {
                id = id
            };
            return await PostAsync<object, ApiResponseRoot<string>>("api/MembershipType/DeleteRecord", requestBody);
        }
    }
}
