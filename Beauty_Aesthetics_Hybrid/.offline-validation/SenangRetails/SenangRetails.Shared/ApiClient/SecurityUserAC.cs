using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient
{
    public class SecurityUserAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public SecurityUserAC(IStoreTokenService tokenService)
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<UserDetailResult>?> GetUserByEmailAsync(string email)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<UserDetailResult>>(
                "api/Security_User/GetUserByEmail", new { Id = email });
        }

        public async Task<ApiResponseRoot<object>?> UpdateRecordAsync(SecurityUserUpdateDM model)
        {
            if (!await SetBearerToken()) return null;
            return await PutAsync<SecurityUserUpdateDM, ApiResponseRoot<object>>(
                "api/Security_User/UpdateRecord", model);
        }
    }
}
