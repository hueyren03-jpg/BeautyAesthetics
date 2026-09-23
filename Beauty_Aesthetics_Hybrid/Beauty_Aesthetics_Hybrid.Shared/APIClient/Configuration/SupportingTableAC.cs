using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class SupportingTableAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public SupportingTableAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<SupportingTableListItemDTO>>> LoadListByTypeAsync(
        int typeId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/SupportingTable/LoadListByType", new { id = typeId });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<List<SupportingTableListItemDTO>>(
            response,
            "Supporting table list response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<SupportingTableDM>> LoadRecordAsync(
        string supportingTableId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest(
            "/api/SupportingTable/LoadRecord",
            new { id = supportingTableId });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<SupportingTableDM>(
            response,
            "Supporting table record response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<SupportingTableSaveResultDTO>> CreateAsync(
        SupportingTableDM payload,
        CancellationToken cancellationToken = default)
    {
        payload.SaveAction = "Added";
        payload.IsDirty = true;
        using var request = CreatePostRequest("/api/SupportingTable/Create", payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadSaveResponseAsync(
            response,
            "Supporting table create response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<SupportingTableSaveResultDTO>> UpdateAsync(
        SupportingTableDM payload,
        CancellationToken cancellationToken = default)
    {
        payload.SaveAction = "Changed";
        payload.IsDirty = true;
        using var request = CreatePutRequest("/api/SupportingTable/Update", payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadSaveResponseAsync(
            response,
            "Supporting table update response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<SupportingTableSaveResultDTO>> DeleteAsync(
        string supportingTableId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/SupportingTable/Delete", new { id = supportingTableId });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadSaveResponseAsync(
            response,
            "Supporting table delete response was invalid.",
            cancellationToken);
    }
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

    private static async Task<ApiCallResult<SupportingTableSaveResultDTO>> ReadSaveResponseAsync(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<SupportingTableSaveResultDTO>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<SupportingTableSaveResultDTO>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "SupportingTable API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<SupportingTableSaveResultDTO>.Ok(
                response.StatusCode,
                new SupportingTableSaveResultDTO { SuccessMessage = "Record has been saved successfully." });
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object && TryGetInt(root, "statusCode", out var statusCode))
            {
                var message = FindString(root, "message");
                if (statusCode is < 200 or >= 300)
                {
                    return ApiCallResult<SupportingTableSaveResultDTO>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(message) ? invalidResponseMessage : message);
                }

                if (TryGetProperty(root, "result", out var resultElement)
                    && resultElement.ValueKind == JsonValueKind.Object)
                {
                    var saveResult = resultElement.Deserialize<SupportingTableSaveResultDTO>(JsonOptions)
                        ?? new SupportingTableSaveResultDTO();
                    saveResult.SuccessMessage ??= string.IsNullOrWhiteSpace(message)
                        ? "Record has been saved successfully."
                        : message;

                    return ApiCallResult<SupportingTableSaveResultDTO>.Ok(response.StatusCode, saveResult);
                }

                return ApiCallResult<SupportingTableSaveResultDTO>.Ok(
                    response.StatusCode,
                    new SupportingTableSaveResultDTO
                    {
                        SuccessMessage = string.IsNullOrWhiteSpace(message)
                            ? "Record has been saved successfully."
                            : message
                    });
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var directResult = root.Deserialize<SupportingTableSaveResultDTO>(JsonOptions)
                    ?? new SupportingTableSaveResultDTO();

                directResult.SuccessMessage ??= "Record has been saved successfully.";
                return ApiCallResult<SupportingTableSaveResultDTO>.Ok(response.StatusCode, directResult);
            }

            return ApiCallResult<SupportingTableSaveResultDTO>.Ok(
                response.StatusCode,
                new SupportingTableSaveResultDTO { SuccessMessage = "Record has been saved successfully." });
        }
        catch (JsonException)
        {
            return ApiCallResult<SupportingTableSaveResultDTO>.Ok(
                response.StatusCode,
                new SupportingTableSaveResultDTO { SuccessMessage = "Record has been saved successfully." });
        }
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "SupportingTable API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            if (TryCreateEmptySaveResult<T>(null, out var emptySaveResult))
            {
                return ApiCallResult<T>.Ok(response.StatusCode, emptySaveResult);
            }

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
                            ? "SupportingTable API request failed."
                            : apiResponse.Message);
                }

                if (apiResponse.Result is null)
                {
                    if (TryCreateEmptySaveResult<T>(apiResponse.Message, out var emptySaveResult))
                    {
                        return ApiCallResult<T>.Ok(response.StatusCode, emptySaveResult);
                    }

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

    private static bool TryCreateEmptySaveResult<T>(string? message, out T result)
    {
        if (typeof(T) == typeof(SupportingTableSaveResultDTO))
        {
            object saveResult = new SupportingTableSaveResultDTO
            {
                SuccessMessage = string.IsNullOrWhiteSpace(message)
                    ? "Record has been saved successfully."
                    : message
            };

            result = (T)saveResult;
            return true;
        }

        result = default!;
        return false;
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

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        if (TryGetProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
