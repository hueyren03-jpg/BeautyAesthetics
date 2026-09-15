using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class StaffAC : BaseAC
    {
        private readonly HttpClient _httpClient;
        private readonly IStoreTokenService _tokenService;

        public StaffAC(HttpClient httpClient, IStoreTokenService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponse<List<StaffResponseDTO>>> FetchStaffListAsync()
        {
            if (!await SetBearerToken()) return null;
            var apiResponse = await PostAsync<object, ApiResponse<List<StaffResponseDTO>>>("api/Employee/GetAllEmployees", null);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var jsonDebug = System.Text.Json.JsonSerializer.Serialize(apiResponse, options);
            Debug.WriteLine("testing1", jsonDebug);
            return apiResponse ?? new ApiResponse<List<StaffResponseDTO>> { StatusCode = 500, Message = "No response from server" };
        }

        public async Task<ApiResponseRoot<string>> CreateStaffAsync(StaffRequestDTO payload)
        {
            if (!await SetBearerToken()) return null;
            payload.SaveAction = "Added";
            var debugOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, debugOptions);

            Console.WriteLine("DEBUG: Sending Payload to api/Employee/CreateRecord:");
            Console.WriteLine(payloadJson);

            var apiResponse = await PostAsync<StaffRequestDTO, ApiResponseRoot<string>>("api/Employee/CreateRecord", payload);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var jsonDebug = System.Text.Json.JsonSerializer.Serialize(apiResponse, options);

            return apiResponse;
        }

        public async Task<ApiResponseRoot<string>> PutStaffAsync(StaffRequestDTO payload)
        {
            if (!await SetBearerToken()) return null;

            var debugOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, debugOptions);

            Console.WriteLine("DEBUG: Sending Payload to api/Employee/UpdateRecord:");
            Console.WriteLine(payloadJson);

            var apiResponse = await PutAsync<StaffRequestDTO, ApiResponseRoot<string>>("api/Employee/UpdateRecord", payload);

            return apiResponse;
        }
    }
}
