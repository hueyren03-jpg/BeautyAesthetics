using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class WebDashboardAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public WebDashboardAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<JsonElement>> GetBranchPerformanceSummaryAsync(
        DashboardDateRangeRequest payload,
        CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>(
            "/api/WebDashboard/GetBranchPerformanceSummary",
            payload,
            "Branch performance response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<SalesByTypeDTO>> GetSalesByTypeAsync(
        DashboardDateRangeRequest payload,
        CancellationToken cancellationToken = default) =>
        PostAsync<SalesByTypeDTO>(
            "/api/WebDashboard/GetSalesByType",
            payload,
            "Sales-by-type response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<SalesByCollectionDTO>>> GetSalesByCollectionAsync(
        DashboardDateRangeRequest payload,
        CancellationToken cancellationToken = default) =>
        PostAsync<List<SalesByCollectionDTO>>(
            "/api/WebDashboard/GetSalesByCollection",
            payload,
            "Sales collection response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<MemberStatisticSummaryDTO>> GetMemberStatisticSummaryAsync(
        string branchId,
        CancellationToken cancellationToken = default) =>
        PostAsync<MemberStatisticSummaryDTO>(
            "/api/WebDashboard/GetMemberStatisticSummary",
            new DashboardIdRequest { Id = branchId },
            "Member statistic response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<JsonElement>> GetStockBelowReorderPointAsync(
        string branchId,
        CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>(
            "/api/WebDashboard/GetStockBelowReorderPoint",
            new DashboardIdRequest { Id = branchId },
            "Low-stock response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<JsonElement>> GetCustomerLastVisitAsync(
        CustomerLastVisitRequest payload,
        CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>(
            "/api/WebDashboard/GetCustomerLastVisit",
            payload,
            "Customer last-visit response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<JsonElement>> GetMemberOtherBalanceDetailAsync(
        MemberOtherBalanceDetailRequest payload,
        CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>(
            "/api/WebDashboard/GetMemberOtherBalanceDetail",
            payload,
            "Expiring package response was invalid.",
            cancellationToken);

    private async Task<ApiCallResult<T>> PostAsync<T>(
        string uri,
        object payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions)
        };
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Dashboard API request failed."));
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, JsonOptions);
            if (apiResponse is null || !apiResponse.IsSuccess || apiResponse.Result is null)
            {
                return ApiCallResult<T>.Failure(
                    response.StatusCode,
                    string.IsNullOrWhiteSpace(apiResponse?.Message)
                        ? invalidResponseMessage
                        : apiResponse.Message);
            }

            return ApiCallResult<T>.Ok(response.StatusCode, apiResponse.Result);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static string ReadError(string responseBody, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            foreach (var name in new[] { "message", "detail", "title" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? fallback;
                }

                var match = root.EnumerateObject()
                    .FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
                if (match.Value.ValueKind == JsonValueKind.String)
                {
                    return match.Value.GetString() ?? fallback;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
