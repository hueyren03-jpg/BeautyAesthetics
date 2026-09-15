using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.Services.EInvoiceService
{
    public class EInvoiceService : IEInvoiceService
    {
        private readonly HttpClient _httpClient;
        private readonly IStoreTokenService _tokenService;

        public EInvoiceService(HttpClient httpClient, IStoreTokenService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        public async Task<ApiResponse<EInvoiceSubmissionSummary>?> GetSubmissionSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            string branchID)
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
                    startDate = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    branchID = branchID
                };

                Debug.WriteLine($"GetSubmissionSummary Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync("/api/WebDashboard/GetCashSalesEInvoiceSubmissionSummary", request);
                Debug.WriteLine($"GetSubmissionSummary Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var apiResult = await response.Content
                    .ReadFromJsonAsync<ApiResponse<EInvoiceSubmissionSummary>>(
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                return apiResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSubmissionSummaryAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiResponse<EInvoiceSubmitPayload>?> SubmitConsolidatedAsync(
            DateTime startDate,
            DateTime endDate,
            string strID)
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
                    strID = strID,
                    startDate = startDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    endDate = endDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                Debug.WriteLine($"SubmitConsolidated Request: {JsonSerializer.Serialize(request)}");

                var response = await _httpClient.PostAsJsonAsync("/api/EInvoiceMY/SubmitConsolidatedCashSales", request);
                Debug.WriteLine($"SubmitConsolidated Response Status: {response.StatusCode}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"SubmitConsolidated Response Content: {responseContent}");

                // Always try to parse the response, whether success or error
                if (response.IsSuccessStatusCode)
                {
                    // Parse successful response
                    var apiResult = await response.Content
                        .ReadFromJsonAsync<ApiResponse<EInvoiceSubmitPayload>>(
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                    return apiResult;
                }
                else
                {
                    // Parse error response
                    try
                    {
                        // Try to parse as your error format
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(
                            responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (errorResponse != null)
                        {
                            // Convert Extensions to Dictionary if needed
                            Dictionary<string, object>? extensions = null;
                            if (errorResponse.Extensions != null)
                            {
                                // Try to convert to dictionary
                                var jsonElement = (JsonElement)errorResponse.Extensions;
                                if (jsonElement.ValueKind == JsonValueKind.Object)
                                {
                                    extensions = new Dictionary<string, object>();
                                    foreach (var property in jsonElement.EnumerateObject())
                                    {
                                        extensions[property.Name] = property.Value.ToString();
                                    }
                                }
                            }

                            // Return an ApiResponse with error details
                            return new ApiResponse<EInvoiceSubmitPayload>
                            {
                                IsError = true,
                                Type = errorResponse.Type,
                                Title = errorResponse.Title,
                                Status = errorResponse.Status,
                                Detail = errorResponse.Detail,
                                Instance = errorResponse.Instance,
                                Extensions = extensions,
                                StatusCode = (int)response.StatusCode,
                                Message = errorResponse.Title ?? $"Error {response.StatusCode}",
                                Result = null
                            };
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"Failed to parse error response as JSON: {jsonEx.Message}");
                    }

                    // Fallback error response
                    return new ApiResponse<EInvoiceSubmitPayload>
                    {
                        IsError = true,
                        StatusCode = (int)response.StatusCode,
                        Title = response.ReasonPhrase ?? "Request failed",
                        Message = responseContent ?? response.ReasonPhrase ?? "Unknown error",
                        Result = null
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in SubmitConsolidatedAsync: {ex.Message}");
                return new ApiResponse<EInvoiceSubmitPayload>
                {
                    IsError = true,
                    Title = "Exception",
                    Detail = ex.Message,
                    Message = ex.Message,
                    Result = null
                };
            }
        }
    }

    // Error response model
    public class ErrorResponse
    {
        public bool IsError { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Status { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string Instance { get; set; } = string.Empty;
        public object Extensions { get; set; } = string.Empty;
    }
}