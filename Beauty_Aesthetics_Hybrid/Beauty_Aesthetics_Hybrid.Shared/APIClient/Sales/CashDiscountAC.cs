using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class CashDiscountAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IAuthService authService;

    public CashDiscountAC(IAuthService authService) => this.authService = authService;

    public async Task<ApiCallResult<List<CashDiscountRuleDTO>>> LoadProxyAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/CashDiscount/LoadProxy");
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return ApiCallResult<List<CashDiscountRuleDTO>>.Unauthorized(response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiCallResult<List<CashDiscountRuleDTO>>.Failure(response.StatusCode, ReadError(body));

        if (string.IsNullOrWhiteSpace(body))
            return ApiCallResult<List<CashDiscountRuleDTO>>.Ok(response.StatusCode, new List<CashDiscountRuleDTO>());

        try
        {
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<CashDiscountRuleDTO>>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                if (!wrapped.IsSuccess)
                    return ApiCallResult<List<CashDiscountRuleDTO>>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapped.Message) ? "Cash discount API request failed." : wrapped.Message);

                return ApiCallResult<List<CashDiscountRuleDTO>>.Ok(
                    response.StatusCode,
                    wrapped.Result ?? new List<CashDiscountRuleDTO>());
            }

            var direct = JsonSerializer.Deserialize<List<CashDiscountRuleDTO>>(body, JsonOptions);
            return ApiCallResult<List<CashDiscountRuleDTO>>.Ok(
                response.StatusCode,
                direct ?? new List<CashDiscountRuleDTO>());
        }
        catch (JsonException)
        {
            return ApiCallResult<List<CashDiscountRuleDTO>>.Failure(
                response.StatusCode,
                "Cash discount list response was invalid.");
        }
    }

    private static string ReadError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            foreach (var name in new[] { "detail", "Detail", "message", "Message", "title", "Title" })
            {
                if (root.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString()!;
            }
        }
        catch (JsonException) { }

        return "Cash discount API request failed.";
    }
}
