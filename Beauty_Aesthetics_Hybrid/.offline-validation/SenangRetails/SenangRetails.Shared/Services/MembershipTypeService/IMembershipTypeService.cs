using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.MembershipTypeService
{
    public interface IMembershipTypeService
    {
        Task<List<MembershipTypeDM>> GetAllMembershipTypesAsync();
        Task<MembershipType?> LoadRecord(string requestId);
        Task<ApiResponseRoot<MembershiptypeCreateResponseDTO>> CreateAsync(MembershipType requestBody);
        Task<ApiResponseRoot<string>> DeleteAsync(string requestId);
        Task<ApiResponseRoot<string>> UpdateAsync(MembershipType requestBody);
    }
}
