using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class BranchAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public BranchAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<BranchItem>?> LoadRecordAsync(string branchId)
        {
            if (!await SetBearerToken()) return null;

            var payload = new { Id = branchId };

            return await PostAsync<object, ApiResponseRoot<BranchItem>>(
                "api/Branch/LoadRecord", payload);
        }

        public async Task<ApiResponseRoot<BranchItem>?> UpdateRecordAsync(BranchItem item)
        {
            if (!await SetBearerToken()) return null;

            // Inject SaveAction as a string — API requires it to treat the record as an update.
            // BranchItem intentionally omits SaveAction because the API returns it as an int (incompatible type).
            var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                System.Text.Json.JsonSerializer.Serialize(item))!;
            payload["SaveAction"] = System.Text.Json.JsonSerializer.SerializeToElement("Changed");

            return await PutAsync<Dictionary<string, System.Text.Json.JsonElement>, ApiResponseRoot<BranchItem>>(
                "api/Branch/UpdateRecord", payload);
        }
    }
}
