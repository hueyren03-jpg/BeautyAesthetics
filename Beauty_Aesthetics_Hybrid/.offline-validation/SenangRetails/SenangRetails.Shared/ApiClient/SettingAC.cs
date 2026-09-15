using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SenangRetails.Shared.ApiClient
{
    public class SettingAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;
        private readonly AppState _appState;

        public SettingAC(IStoreTokenService tokenService, AppState appState) : base()
        {
            _tokenService = tokenService;
            _appState = appState;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<EmailSettingUpdateResult>?> UpdateEmailSettingAsync(EmailSettingDto request)
        {
            if (!await SetBearerToken()) return null;

            try
            {
                request.SaveAction = 0;
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"[SettingAC] UpdateEmailSettingAsync Request: {json}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingAC] Failed to serialize UpdateEmailSettingAsync request: {ex.Message}");
            }

            return await PostAsync<EmailSettingDto, ApiResponseRoot<EmailSettingUpdateResult>>("api/DefaultAccount/UpdateRecord", request);
        }

        public async Task<ApiResponseRoot<EmailSettingDto>?> GetEmailSettingAsync(string branchId)
        {
            if (!await SetBearerToken()) return null;
            var payload = new { Id = branchId };
            return await PostAsync<object, ApiResponseRoot<EmailSettingDto>>("api/DefaultAccount/LoadRecord", payload);
        }
    }
}
