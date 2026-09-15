using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class PointConversionAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public PointConversionAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<List<PointConversionDM>>> LoadProxyAsync(
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<List<PointConversionDM>>(
            HttpMethod.Post,
            "/api/CashSales_PointConversionFormula/LoadProxy",
            new { },
            "Member point list response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<PointConversionDM>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<PointConversionDM>(
            HttpMethod.Post,
            "/api/CashSales_PointConversionFormula/LoadRecord",
            new { id },
            "Member point record response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<bool>> CreateRecordAsync(
        PointConversionDM payload,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/CashSales_PointConversionFormula/CreateRecord",
            payload,
            cancellationToken);

    public Task<ApiCallResult<bool>> UpdateRecordAsync(
        PointConversionDM payload,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Put,
            "/api/CashSales_PointConversionFormula/UpdateRecord",
            payload,
            cancellationToken);

    public Task<ApiCallResult<bool>> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Delete,
            $"/api/CashSales_PointConversionFormula/Delete?id={Uri.EscapeDataString(id)}",
            null,
            cancellationToken);

    private async Task<ApiCallResult<T>> SendForResultAsync<T>(
        HttpMethod method,
        string uri,
        object payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Member point API request failed."));
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, JsonOptions);
            if (envelope?.StatusCode > 0)
            {
                if (!envelope.IsSuccess || envelope.Result is null)
                {
                    return ApiCallResult<T>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(envelope.Message) ? invalidResponseMessage : envelope.Message);
                }

                return ApiCallResult<T>.Ok(response.StatusCode, envelope.Result);
            }
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }

        return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
    }

    private async Task<ApiCallResult<bool>> SendMutationAsync(
        HttpMethod method,
        string uri,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<bool>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<bool>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Member point API request failed."));
        }

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(responseBody, JsonOptions);
                if (envelope?.StatusCode > 0 && !envelope.IsSuccess)
                {
                    return ApiCallResult<bool>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(envelope.Message)
                            ? "Member point API request failed."
                            : envelope.Message);
                }
            }
            catch (JsonException)
            {
                // A 2xx plain-text response is still a successful mutation.
            }
        }

        return ApiCallResult<bool>.Ok(response.StatusCode, true);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? payload)
    {
        var request = new HttpRequestMessage(method, uri);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions);
        }

        return request;
    }

    private static string ReadError(string responseBody, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            foreach (var name in new[] { "message", "detail", "title" })
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString() ?? fallback;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return fallback;
        }

        return fallback;
    }
}
