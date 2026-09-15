using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.ApiClient
{
    public class AuthAC : BaseAC
    {
        private readonly HttpClient _httpClient;
        public AuthAC(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<ApiResponseRoot<ApiAuthResult>?> LoginAsync(string email, string password)
        {
            var request = new LoginDTO
            {
                email = email,
                password = password
            };

            var response = await _httpClient.PostAsJsonAsync("/api/Account/Login", request);
            Debug.WriteLine($"Login Response: {response}");

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResult = await response.Content
                .ReadFromJsonAsync<ApiResponseRoot<ApiAuthResult>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            return apiResult;
        }



        public async Task<ApiResponseRoot<UserDetailResult>?> GetUserByEmailAsync(string email)
        {
            var request = new { Id = email };

            var response = await PostAsync<object, ApiResponseRoot<UserDetailResult>>(
                "api/Security_User/GetUserByEmail", request);

            if (response?.statusCode == 200)
            {
                return response;
            }
            return null;
        }

        //    public async Task<ApiResponse<AuthResponse>?> RefreshTokenAsync()
        //    {
        //        var users = await _userService.GetUsersAsync();
        //        var currentUser = users.FirstOrDefault();

        //        if (currentUser == null)
        //        {
        //            return null;
        //        }
        //        var accessToken = currentUser.AccessToken;
        //        var refreshToken = currentUser.RefreshToken;

        //        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        //            return null;

        //        var request = new
        //        {
        //            accessToken = accessToken,
        //            refreshToken = refreshToken
        //        };

        //        return await PostAsync<object, ApiResponse<AuthResponse>>("api/Account/RefreshToken", request);
        //    }

        //public async Task<ApiResponseRoot<ApiAuthResult>?> GetUserByEmail(string email)
        //{
        //    var users = await _userService.GetUsersAsync();
        //    var currentUser = users.FirstOrDefault();
        //    if (currentUser == null || currentUser.AccessToken == null)
        //    {
        //        return null;
        //    }

        //    if (!CreateBearerAuthAsync(currentUser.AccessToken))
        //        return null;

        //    var request = new LoadWithIdReq { Id = email };

        //    var response = await PostAsync<LoadWithIdReq, ApiResponseRoot>(
        //        "api/Security_User/GetUserByEmail", request);

        //    Console.WriteLine($"User Permission: {JsonSerializer.Serialize(response)}");

        //    if (response == null || response.Result == null)
        //        return null;

        //    return response;
        //}

        //}

        //// Generic API response wrapper
        //public class ApiResponse<T>
        //{
        //    [JsonPropertyName("statusCode")]
        //    public int StatusCode { get; set; }

        //    [JsonPropertyName("message")]
        //    public string Message { get; set; } = string.Empty;

        //    [JsonPropertyName("result")]
        //    public T? Result { get; set; }
        //}

        //public class LoginResult
        //{
        //    [JsonPropertyName("RequiresTwoFactor")]
        //    public bool RequiresTwoFactor { get; set; }

        //    [JsonPropertyName("RequiresCompanySelection")]
        //    public bool RequiresCompanySelection { get; set; }

        //    [JsonPropertyName("AuthResponse")]
        //    public AuthResponse AuthResponse { get; set; } = new();
        //}

        //public class AuthResponse
        //{
        //    [JsonPropertyName("AccessToken")]
        //    public string AccessToken { get; set; } = string.Empty;

        //    [JsonPropertyName("RefreshToken")]
        //    public string RefreshToken { get; set; } = string.Empty;
        //}

        //public class ApiResponseRoot
        //{
        //    [JsonPropertyName("statusCode")]
        //    public int StatusCode { get; set; }

        //    [JsonPropertyName("message")]
        //    public string Message { get; set; }

        //    [JsonPropertyName("result")]
        //    public UserResultDTO Result { get; set; }
        //}

        //public class UserResultDTO
        //{
        //    [JsonPropertyName("lstSecurities")]
        //    public UserModuleDTO lstSecurities { get; set; }
        //}

        //public class UserModuleDTO
        //{
        //    [JsonPropertyName("PointofSales")]
        //    public UserPermissionDTO PointOfSales { get; set; }
        //    [JsonPropertyName("Inventory")]
        //    public UserPermissionDTO Inventory { get; set; }
        //    [JsonPropertyName("ItemGroup")]
        //    public UserPermissionDTO ItemGroup { get; set; }
        //    [JsonPropertyName("CashSalesList")]
        //    public UserPermissionDTO CashSalesList { get; set; }
        //    [JsonPropertyName("WebDashboard")]
        //    public UserPermissionDTO WebDashboard { get; set; }
        //    [JsonPropertyName("Reports")]
        //    public UserPermissionDTO Reports { get; set; }
        //    [JsonPropertyName("SubmitConsoEInvoice")]
        //    public UserPermissionDTO SubmitConsoEInvoice { get; set; }

        //}

        //public class UserPermissionDTO
        //{
        //    [JsonPropertyName("CanViewRecord")]
        //    public bool CanViewRecord { get; set; }
        //}
    }
}
