using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Pages;
using SenangRetails.Shared.Services.AuthService;
using static SenangRetails.Shared.Components.Reports.MemberCreditBalanceReport;
using static SenangRetails.Shared.Pages.CompanySettings;

namespace SenangRetails.Shared.Services
{
    public class TaxService : ITaxService
    {
        private readonly HttpClient _httpClient;
        private readonly IStoreTokenService _tokenService;

        public TaxService(HttpClient httpClient, IStoreTokenService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        public async Task<ApiResponse<List<TaxCodeItem>>?> LoadProxyByParentIDAsync(
          string id)
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var request = new
                {
                    id = id
                };

                Debug.WriteLine($"LoadProxyByParentID Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync(
                    "api/GSTTaxCode/LoadProxyByParentID",
                    request);

                Debug.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                // Deserialize as List of items since API returns an array
                var apiResult = await response.Content.ReadFromJsonAsync<ApiResponse<List<TaxCodeItem>>>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in LoadProxyByParentIDAsync: {ex.Message}");
                return null;
            }
        }
    }
}