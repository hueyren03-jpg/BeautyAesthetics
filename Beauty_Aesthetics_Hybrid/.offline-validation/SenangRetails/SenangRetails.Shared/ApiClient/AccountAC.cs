using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System.Net.Http.Json;
using System.Text.Json;

namespace SenangRetails.Shared.ApiClient
{
    public class AccountAC
    {
        private readonly HttpClient _httpClient;
        private readonly IStoreTokenService _tokenService;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AccountAC(HttpClient httpClient, IStoreTokenService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Returns (statusCode, message, newAccessToken, newRefreshToken).
        /// newAccessToken/newRefreshToken are non-null only on success.
        /// </summary>
        public async Task<(int StatusCode, string Message, string? NewAccessToken, string? NewRefreshToken)> ChangePasswordAsync(
            string oldPassword, string newPassword, string confirmPassword)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsJsonAsync("api/account/Manage/ChangePassword", new
            {
                oldPassword,
                newPassword,
                confirmPassword
            });

            var httpStatus = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[AccountAC ChangePassword] HTTP {httpStatus} | {body}");

            if (string.IsNullOrWhiteSpace(body))
                return (httpStatus, $"HTTP {httpStatus}", null, null);

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Standard ApiResponseRoot wrapper: { statusCode, message, result }
                if (root.TryGetProperty("statusCode", out var sc))
                {
                    var code = sc.GetInt32();
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

                    if (code == 200 && root.TryGetProperty("result", out var result))
                    {
                        string? accessToken = null;
                        string? refreshToken = null;

                        // result.AuthResponse.AccessToken / RefreshToken
                        if (result.TryGetProperty("AuthResponse", out var auth) ||
                            result.TryGetProperty("authResponse", out auth))
                        {
                            if (auth.TryGetProperty("AccessToken", out var at) ||
                                auth.TryGetProperty("accessToken", out at))
                                accessToken = at.GetString();

                            if (auth.TryGetProperty("RefreshToken", out var rt) ||
                                auth.TryGetProperty("refreshToken", out rt))
                                refreshToken = rt.GetString();
                        }

                        return (200, msg, accessToken, refreshToken);
                    }

                    return (code, msg, null, null);
                }

                // ProblemDetails format: { Status, Detail, Title }
                if (root.TryGetProperty("status", out var status) || root.TryGetProperty("Status", out status))
                {
                    var detail = root.TryGetProperty("detail", out var d) ? d.GetString()
                               : root.TryGetProperty("Detail", out d) ? d.GetString()
                               : root.TryGetProperty("title", out var t) ? t.GetString()
                               : root.TryGetProperty("Title", out t) ? t.GetString()
                               : body;

                    return (httpStatus, detail ?? body, null, null);
                }
            }
            catch { }

            return (httpStatus, body, null, null);
        }
    }
}
