using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.UserControl;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class UserControlAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public UserControlAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<List<Security_UserGroupDM>>?> GetAllSecurityGroupsAsync()
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<object, ApiResponseRoot<List<Security_UserGroupDM>>>(
                "api/Security_UserGroup/GetAllSecurityGroups",
                new { });
        }

        public async Task<ApiResponseRoot<List<SecurityDM>>?> LoadProxyByParentID(string id)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<object, ApiResponseRoot<List<SecurityDM>>>(
                "api/Security/LoadProxyByParentID",
                new { id = id });
        }

        public async Task<ApiResponseRoot<string>?> UpdateSecurityRecordAsync(SecurityDM record)
        {
            if (!await SetBearerToken()) return null;

            try
            {
                record.SaveAction = 0; 
                record.IsDirty = true;

                return await PostAsync<SecurityDM, ApiResponseRoot<string>>(
                    "api/Security/UpdateRecord",
                    record);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AC ERROR] UpdateRecord failed: {ex.Message}");
                return new ApiResponseRoot<string> { statusCode = 500, message = ex.Message };
            }
        }
    }
}
