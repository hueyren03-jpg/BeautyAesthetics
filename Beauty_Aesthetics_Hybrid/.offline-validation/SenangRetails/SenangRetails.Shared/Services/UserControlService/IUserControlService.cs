using SenangRetails.Shared.Models.DTOs.UserControl;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.UserControlService
{
    public interface IUserControlService
    {
        Task<ApiResponseRoot<List<Security_UserGroupDM>>?> GetAllSecurityGroupsAsync();
        Task<ApiResponseRoot<List<SecurityDM>>?> LoadProxyByParentIDAsync(string id);
        Task<ApiResponseRoot<string>?> UpdateSecurityRecordAsync(SecurityDM record);
    }
}
