using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

/// <summary>
/// Thin HTTP wrapper for the <c>/api/MembershipType</c> endpoints.
/// </summary>
public sealed class MembershipTypeAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public MembershipTypeAC(IAuthService authService)
    {
        this.authService = authService;
    }

    // ── Queries ────────────────────────────────────────────

    public async Task<ApiCallResult<List<MembershipTypeDM>>> GetAllMembershipTypesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/MembershipType/GetAllMembershipTypes", new { });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<List<MembershipTypeDM>>(
            response,
            "Membership type list response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<MembershipTypeDM>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/MembershipType/LoadRecord", new { id });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<MembershipTypeDM>(
            response,
            "Membership type record response was invalid.",
            cancellationToken);
    }

    // ── Mutations ──────────────────────────────────────────

    public async Task<ApiCallResult<JsonElement>> CreateRecordAsync(
        MembershipTypeSaveDTO payload,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/MembershipType/CreateRecord", payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<JsonElement>(
            response,
            "Create membership type response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<JsonElement>> UpdateRecordAsync(
        MembershipTypeSaveDTO payload,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePutRequest("/api/MembershipType/UpdateRecord", payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<JsonElement>(
            response,
            "Update membership type response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<JsonElement>> DeleteRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/MembershipType/DeleteRecord", new { id });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<JsonElement>(
            response,
            "Delete membership type response was invalid.",
            cancellationToken);
    }

    // ── Helpers ────────────────────────────────────────────

    private static HttpRequestMessage CreatePostRequest<T>(string uri, T payload)
    {
        return new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions)
        };
    }

    private static HttpRequestMessage CreatePutRequest<T>(string uri, T payload)
    {
        return new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions)
        };
    }

    private static async Task<ApiCallResult<T>> ReadApiResponseAsync<T>(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "MembershipType API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, JsonOptions);
            if (apiResponse?.StatusCode > 0)
            {
                if (!apiResponse.IsSuccess)
                {
                    return ApiCallResult<T>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message)
                            ? "MembershipType API request failed."
                            : apiResponse.Message);
                }

                if (apiResponse.Result is null)
                {
                    return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
                }

                return ApiCallResult<T>.Ok(response.StatusCode, apiResponse.Result);
            }

            var directResult = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
            return directResult is null
                ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                : ApiCallResult<T>.Ok(response.StatusCode, directResult);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static string BuildApiErrorMessage(HttpStatusCode statusCode, string responseBody, string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"{fallbackMessage} ({(int)statusCode}).";
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var message = FindString(root, "message");
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            var title = FindString(root, "title");
            var detail = FindString(root, "detail");

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(detail))
            {
                return $"{title}: {detail}";
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                return $"{title} ({(int)statusCode}).";
            }
        }
        catch (JsonException)
        {
            return $"{fallbackMessage} ({(int)statusCode}).";
        }

        return $"{fallbackMessage} ({(int)statusCode}).";
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
