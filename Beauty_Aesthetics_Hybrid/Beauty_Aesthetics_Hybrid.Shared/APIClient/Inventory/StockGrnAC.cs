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

public sealed class StockGrnAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private static readonly MediaTypeHeaderValue ApplicationJsonMediaType =
        MediaTypeHeaderValue.Parse("application/json");

    private readonly IAuthService authService;

    public StockGrnAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<List<Doc_Stock_GRNDM>>> LoadProxyAsync(
        StockGrnProxyRequestDTO requestDto,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<List<Doc_Stock_GRNDM>>(
            HttpMethod.Post,
            "/api/Doc_Stock_GRN/LoadProxy",
            requestDto,
            "GRN history response was invalid.",
            cancellationToken,
            useApplicationJson: true);

    public Task<ApiCallResult<Doc_Stock_GRN>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<Doc_Stock_GRN>(
            HttpMethod.Post,
            "/api/Doc_Stock_GRN/LoadRecord",
            new StockGrnLookupDTO { Id = id },
            "GRN record response was invalid.",
            cancellationToken,
            useApplicationJson: true);

    public Task<ApiCallResult<Doc_Stock_GRNDM>> GetDMAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendForResultAsync<Doc_Stock_GRNDM>(
            HttpMethod.Post,
            "/api/Doc_Stock_GRN/GetDM",
            new StockGrnLookupDTO { Id = id },
            "GRN document response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> CreateRecordAsync(
        Doc_Stock_GRN grn,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_Stock_GRN/CreateRecord",
            grn,
            "Create GRN response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> UpdateRecordAsync(
        Doc_Stock_GRN grn,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Put,
            "/api/Doc_Stock_GRN/UpdateRecord",
            grn,
            "Update GRN response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> SaveDMAsync(
        Doc_Stock_GRNDM grn,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_Stock_GRN/SaveDM",
            grn,
            "Save GRN response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Delete,
            "/api/Doc_Stock_GRN/Delete",
            new StockGrnLookupDTO { Id = id },
            "Delete GRN response was invalid.",
            cancellationToken);

    private async Task<ApiCallResult<T>> SendForResultAsync<T>(
        HttpMethod method,
        string uri,
        object payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken,
        bool useApplicationJson = false)
    {
        using var request = CreateRequest(method, uri, payload, useApplicationJson);
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
                ReadError(body, "Inventory document API request failed."));
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
                            ? "Inventory document API request failed."
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
                ReadError(body, "Inventory document API request failed."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<string>.Ok(response.StatusCode, "Success");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOptions);
            if (envelope?.StatusCode > 0)
            {
                if (!envelope.IsSuccess)
                {
                    return ApiCallResult<string>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(envelope.Message)
                            ? invalidResponseMessage
                            : envelope.Message);
                }

                var fallback = string.IsNullOrWhiteSpace(envelope.Message) ? "Success" : envelope.Message;
                return ApiCallResult<string>.Ok(
                    response.StatusCode,
                    ReadMutationResult(envelope.Result, fallback));
            }
        }
        catch (JsonException)
        {
            // A successful API may return a non-standard success body.
        }

        return ApiCallResult<string>.Ok(response.StatusCode, "Success");
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string uri,
        object payload,
        bool useApplicationJson = false) =>
        new(method, uri)
        {
            Content = JsonContent.Create(
                payload,
                mediaType: useApplicationJson ? ApplicationJsonMediaType : JsonPatchMediaType,
                options: JsonOptions)
        };

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
            foreach (var preferredName in new[]
                     {
                         "DocumentID", "DocumentId", "StockTransferDocumentID",
                         "StockTransferDocumentId", "Id", "ID", "DisplayCode"
                     })
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

            foreach (var property in result.EnumerateObject())
            {
                if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
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

        return fallback;
    }

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
