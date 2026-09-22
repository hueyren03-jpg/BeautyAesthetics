using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class InventoryPendingAcceptAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private readonly IAuthService authService;

    public InventoryPendingAcceptAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<List<InventoryMovement_PendingAcceptDM>>> GetPendingByBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default) =>
        PostListAsync(
            "/api/InventoryMovement_PendingAccept/GetPendingAcceptDocumentByBranchID",
            branchId,
            "Pending stock receipt list response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<InventoryMovement_PendingAcceptDM>>> GetDetailsAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        PostListAsync(
            "/api/InventoryMovement_PendingAccept/GetPendingAcceptDocumentDetails",
            documentId,
            "Pending stock receipt details response was invalid.",
            cancellationToken);

    public async Task<ApiCallResult<string>> AcceptStockInAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest(
            "/api/InventoryMovement_PendingAccept/AcceptStockIn",
            new PendingStockReceiptLookupDTO { Id = documentId });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadMutationResponseAsync(response, cancellationToken);
    }

    private async Task<ApiCallResult<List<InventoryMovement_PendingAcceptDM>>> PostListAsync(
        string uri,
        string id,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreatePostRequest(uri, new PendingStockReceiptLookupDTO { Id = id });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<List<InventoryMovement_PendingAcceptDM>>(
            response,
            invalidResponseMessage,
            cancellationToken);
    }

    private static HttpRequestMessage CreatePostRequest<T>(string uri, T payload) => new(HttpMethod.Post, uri)
    {
        Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions)
    };

    private static async Task<ApiCallResult<T>> ReadApiResponseAsync<T>(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, ReadError(body, "Pending stock receipt request failed."));
        }

        try
        {
            var wrapper = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions);
            if (wrapper?.StatusCode > 0)
            {
                return wrapper.IsSuccess && wrapper.Result is not null
                    ? ApiCallResult<T>.Ok(response.StatusCode, wrapper.Result)
                    : ApiCallResult<T>.Failure(response.StatusCode, wrapper.Message ?? invalidResponseMessage);
            }

            var direct = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return direct is null
                ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                : ApiCallResult<T>.Ok(response.StatusCode, direct);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static async Task<ApiCallResult<string>> ReadMutationResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<string>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, ReadError(body, "Accept stock request failed."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<string>.Ok(response.StatusCode, "Stock received successfully.");
        }

        try
        {
            var wrapper = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOptions);
            if (wrapper?.StatusCode > 0)
            {
                return wrapper.IsSuccess
                    ? ApiCallResult<string>.Ok(
                        response.StatusCode,
                        ReadMutationResult(
                            wrapper.Result,
                            wrapper.Message ?? "Stock received successfully."))
                    : ApiCallResult<string>.Failure(response.StatusCode, wrapper.Message ?? "Accept stock request failed.");
            }
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, "Accept stock response was invalid.");
        }

        return ApiCallResult<string>.Ok(response.StatusCode, "Stock received successfully.");
    }

    private static string ReadMutationResult(JsonElement result, string fallback)
    {
        if (result.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return fallback;
        }

        if (result.ValueKind == JsonValueKind.String)
        {
            return string.IsNullOrWhiteSpace(result.GetString()) ? fallback : result.GetString()!;
        }

        if (result.ValueKind == JsonValueKind.Number)
        {
            return result.GetRawText();
        }

        if (result.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in result.EnumerateArray())
            {
                var value = ReadMutationResult(item, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return fallback;
        }

        if (result.ValueKind == JsonValueKind.Object)
        {
            var preferredNames = new[]
            {
                "Id", "DocumentID", "DocumentId", "GRNDocumentID", "GRNDocumentId", "DisplayCode"
            };

            foreach (var preferredName in preferredNames)
            {
                foreach (var property in result.EnumerateObject())
                {
                    if (!string.Equals(property.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = ReadMutationResult(property.Value, string.Empty);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return fallback;
    }

    private static string ReadError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var name in new[] { "message", "detail", "title" })
            {
                if (document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? fallback;
                }

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
        }

        return fallback;
    }
}
