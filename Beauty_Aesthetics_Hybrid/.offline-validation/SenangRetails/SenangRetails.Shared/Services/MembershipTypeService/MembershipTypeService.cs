using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.MembershipTypeService
{
    public class MembershipTypeService : IMembershipTypeService
    {
        private readonly MembershipTypeAC _membershipTypeAC;

        public MembershipTypeService(MembershipTypeAC membershipTypeAC)
        {
            _membershipTypeAC = membershipTypeAC;
        }

        public Task<ApiResponseRoot<MembershiptypeCreateResponseDTO>> CreateAsync(MembershipType requestBody)
        {
            return _membershipTypeAC.CreateMembershipTypeAsync(requestBody);
        }

        public Task<ApiResponseRoot<string>> UpdateAsync(MembershipType requestBody)
        {
            return _membershipTypeAC.PutMembershipTypeAsync(requestBody);
        }

        public async Task<List<MembershipTypeDM>> GetAllMembershipTypesAsync()
        {
            var response = await _membershipTypeAC.GetMembershipTypesAsync();
            return response?.Result ?? [];
        }

        public async Task<ApiResponseRoot<string>> DeleteAsync(string requestId)
        {
            return await _membershipTypeAC.DeleteMembershipTypeAsync(requestId);
        }

        public async Task<MembershipType?> LoadRecord(string requestId)
        {
            var response = await _membershipTypeAC.LoadRecordAsync(requestId);
            return response?.Result;
        }
    }
}
