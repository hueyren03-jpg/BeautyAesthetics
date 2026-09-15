using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.UserControl;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.UserControlService
{
    public class UserControlService : IUserControlService
    {
        private readonly UserControlAC _userControlAC;

        public UserControlService(UserControlAC userControlAC)
        {
            _userControlAC = userControlAC;
        }

        public async Task<ApiResponseRoot<List<Security_UserGroupDM>>?> GetAllSecurityGroupsAsync()
        {
            var response = await _userControlAC.GetAllSecurityGroupsAsync();

            return response;
        }

        public async Task<ApiResponseRoot<List<SecurityDM>>?> LoadProxyByParentIDAsync(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return null;

                return await _userControlAC.LoadProxyByParentID(id);
            }
            catch (Exception ex)
            {
                return new ApiResponseRoot<List<SecurityDM>>
                {
                    statusCode = 500,
                    message = $"UserControlService Error: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponseRoot<string>?> UpdateSecurityRecordAsync(SecurityDM record)
        {
            try
            {
                if (record == null) return null;
                return await _userControlAC.UpdateSecurityRecordAsync(record);
            }
            catch (Exception ex)
            {
                return new ApiResponseRoot<string>
                {
                    statusCode = 500,
                    message = $"UserControlService Error: {ex.Message}"
                };
            }
        }
    }
}
