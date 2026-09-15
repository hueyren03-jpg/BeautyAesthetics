using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient
{
    public class SupportingTableAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public SupportingTableAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        /// <summary>Load all entries of a given type. Use id=4 for Item Categories.</summary>
        public async Task<ApiResponseRoot<List<SupportingTableItem>>?> LoadListByTypeAsync(int typeId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<List<SupportingTableItem>>>(
                "api/SupportingTable/LoadListByType", new { id = typeId });
        }

        /// <summary>Load a single supporting table record.</summary>
        public async Task<ApiResponseRoot<SupportingTableItem>?> LoadRecordAsync(string supportingTableId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<SupportingTableItem>>(
                "api/SupportingTable/LoadRecord", new { id = supportingTableId });
        }

        /// <summary>Create a new supporting table entry.</summary>
        public async Task<ApiResponseRoot<object>?> CreateAsync(SupportingTableModel model)
        {
            if (!await SetBearerToken()) return null;
            model.saveAction = "Added";
            model.isDirty = true;
            return await PostAsync<SupportingTableModel, ApiResponseRoot<object>>(
                "api/SupportingTable/Create", model);
        }

        /// <summary>Update an existing supporting table entry.</summary>
        public async Task<ApiResponseRoot<object>?> UpdateAsync(SupportingTableModel model)
        {
            if (!await SetBearerToken()) return null;
            model.saveAction = "Changed";
            model.isDirty = true;
            return await PutAsync<SupportingTableModel, ApiResponseRoot<object>>(
                "api/SupportingTable/Update", model);
        }

        /// <summary>Delete a supporting table entry by ID.</summary>
        public async Task<ApiResponseRoot<object>?> DeleteAsync(string supportingTableId)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<object>>(
                "api/SupportingTable/Delete", new { id = supportingTableId });
        }
    }
}
