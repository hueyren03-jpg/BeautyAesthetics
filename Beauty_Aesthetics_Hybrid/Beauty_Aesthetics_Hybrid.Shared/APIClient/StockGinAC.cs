using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class StockGinAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private readonly IAuthService authService;

    public StockGinAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<StockGrnDocumentDTO>>> LoadProxyAsync(
        StockGinProxyRequestDTO requestDto,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/Doc_Stock_GIN/LoadProxy", requestDto);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadDocumentListAsync(response, cancellationToken);
    }

    public Task<ApiCallResult<StockGinEnvelopeDTO>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<StockGinEnvelopeDTO>(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/LoadRecord",
            new StockGinLookupDTO { Id = id },
            "GIN record response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> CreateRecordAsync(
        StockGinEnvelopeDTO gin,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/CreateRecord",
            gin,
            "Create GIN response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> UpdateRecordAsync(
        StockGinEnvelopeDTO gin,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Put,
            "/api/Doc_Stock_GIN/UpdateRecord",
            gin,
            "Update GIN response was invalid.",
            cancellationToken);

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<T>(response, invalidResponseMessage, cancellationToken);
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

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<string>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, ReadError(body, "GIN API request failed."));
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
                    string.IsNullOrWhiteSpace(envelope.Message) ? invalidResponseMessage : envelope.Message);
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
            return ApiCallResult<T>.Failure(response.StatusCode, ReadError(body, "GIN API request failed."));
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
                        string.IsNullOrWhiteSpace(envelope.Message) ? "GIN API request failed." : envelope.Message);
                }

                return envelope.Result is null
                    ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                    : ApiCallResult<T>.Ok(response.StatusCode, envelope.Result);
            }

            var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return result is null
                ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                : ApiCallResult<T>.Ok(response.StatusCode, result);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static async Task<ApiCallResult<List<StockGrnDocumentDTO>>> ReadDocumentListAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(
                response.StatusCode,
                ReadError(body, "Unable to load GIN records."));
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (TryGet(root, "statusCode", out var status) && status.TryGetInt32(out var code) && code is < 200 or >= 300)
            {
                return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(
                    response.StatusCode,
                    FindString(root, "message") ?? "Unable to load GIN records.");
            }

            var payload = TryGet(root, "result", out var result) ? result : root;
            var documents = new List<StockGrnDocumentDTO>();
            ExtractDocuments(payload, documents);
            return ApiCallResult<List<StockGrnDocumentDTO>>.Ok(response.StatusCode, documents);
        }
        catch (JsonException)
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(
                response.StatusCode,
                "GIN list response was invalid.");
        }
    }

    private static void ExtractDocuments(JsonElement element, List<StockGrnDocumentDTO> documents)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && LooksLikeDocument(item))
                {
                    var document = item.Deserialize<StockGrnDocumentDTO>(JsonOptions);
                    if (document is not null) documents.Add(document);
                }
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object) return;
        if (LooksLikeDocument(element))
        {
            var document = element.Deserialize<StockGrnDocumentDTO>(JsonOptions);
            if (document is not null) documents.Add(document);
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Contains("line", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                ExtractDocuments(property.Value, documents);
            }
        }
    }

    private static bool LooksLikeDocument(JsonElement element) =>
        TryGet(element, "documentID", out _) ||
        TryGet(element, "displayCode", out _) ||
        TryGet(element, "friendlyDocumentName", out _);

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string ReadError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            using var json = JsonDocument.Parse(body);
            return FindString(json.RootElement, "message") ??
                   FindString(json.RootElement, "detail") ??
                   fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? FindString(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
