using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.UC;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class StockGinAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public StockGinAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<List<Doc_Stock_GINDM>>> LoadProxyAsync(
        StockGinProxyRequestDTO requestDto,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<List<Doc_Stock_GINDM>>(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/LoadProxy",
            requestDto,
            "GIN history response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<Doc_Stock_GIN>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<Doc_Stock_GIN>(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/LoadRecord",
            new StockGinLookupDTO { Id = id },
            "GIN record response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> CreateRecordAsync(
        Doc_Stock_GIN gin,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/CreateRecord",
            gin,
            "Create GIN response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> UpdateRecordAsync(
        Doc_Stock_GIN gin,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Put,
            "/api/Doc_Stock_GIN/UpdateRecord",
            gin,
            "Update GIN response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Delete,
            "/api/Doc_Stock_GIN/Delete",
            new StockGinLookupDTO { Id = id },
            "Delete GIN response was invalid.",
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
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                ReadError(body, "GIN API request failed."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions);
            if (envelope?.StatusCode > 0)
            {
                if (!envelope.IsSuccess)
                {
                    return ApiCallResult<T>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(envelope.Message)
                            ? "GIN API request failed."
                            : envelope.Message);
                }

                return envelope.Result is null
                    ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                    : ApiCallResult<T>.Ok(response.StatusCode, envelope.Result);
            }

            var directResult = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return directResult is null
                ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                : ApiCallResult<T>.Ok(response.StatusCode, directResult);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private async Task<ApiCallResult<string>> SendMutationAsync(
        HttpMethod method,
        string uri,
        object payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<string>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<string>.Failure(
                response.StatusCode,
                ReadError(body, "GIN API request failed."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<string>.Ok(response.StatusCode, "Success");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOptions);
            if (envelope?.StatusCode > 0 && !envelope.IsSuccess)
            {
                return ApiCallResult<string>.Failure(
                    response.StatusCode,
                    string.IsNullOrWhiteSpace(envelope.Message)
                        ? invalidResponseMessage
                        : envelope.Message);
            }

            return ApiCallResult<string>.Ok(
                response.StatusCode,
                string.IsNullOrWhiteSpace(envelope?.Message) ? "Success" : envelope.Message);
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Ok(response.StatusCode, "Success");
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, object payload) =>
        new(method, uri)
        {
            Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions)
        };

    private static string ReadError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body)) return fallback;

        try
        {
            using var json = JsonDocument.Parse(body);
            return FindString(json.RootElement, "message") ??
                   FindString(json.RootElement, "detail") ??
                   FindString(json.RootElement, "title") ??
                   fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? FindString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
