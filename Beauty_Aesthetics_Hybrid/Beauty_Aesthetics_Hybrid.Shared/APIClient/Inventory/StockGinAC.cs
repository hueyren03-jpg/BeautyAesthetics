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
    private static readonly MediaTypeHeaderValue ApplicationJsonMediaType =
        MediaTypeHeaderValue.Parse("application/json");
    private readonly IAuthService authService;

    public StockGinAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<StockGrnDocumentDTO>>> LoadProxyAsync(
        StockGinProxyRequestDTO requestDto,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/LoadProxy",
            requestDto);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadDocumentListAsync(response, cancellationToken);
    }

    public async Task<ApiCallResult<StockGinEnvelopeDTO>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "/api/Doc_Stock_GIN/LoadRecord",
            new StockGinLookupDTO { Id = id });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await StockRecordResponseReader.ReadAsync<StockGinEnvelopeDTO, StockGrnDocumentDTO>(
            response,
            ["mobjDoc_Stock_GIN", "objDoc_Stock_GIN"],
            (document, lines) => new StockGinEnvelopeDTO
            {
                Document = document,
                DocumentLines = lines
            },
            allowEmptyDocument: string.IsNullOrWhiteSpace(id),
            requestFailureMessage: "GIN API request failed.",
            invalidResponseMessage: "GIN record response was invalid.",
            cancellationToken: cancellationToken);
    }

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

    public Task<ApiCallResult<string>> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Delete,
            "/api/Doc_Stock_GIN/Delete",
            new StockGinLookupDTO { Id = id },
            "Delete GIN response was invalid.",
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

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string uri, object payload) =>
        new(method, uri)
        {
            Content = JsonContent.Create(payload, mediaType: ApplicationJsonMediaType, options: JsonOptions)
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
                if (item.ValueKind != JsonValueKind.Object) continue;

                var document = TryMapDocument(item);
                if (document is not null)
                {
                    documents.Add(document);
                    continue;
                }

                // Some deployments wrap each row one level deeper.
                ExtractDocuments(item, documents);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object) return;

        var mapped = TryMapDocument(element);
        if (mapped is not null)
        {
            documents.Add(mapped);
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

    private static StockGrnDocumentDTO? TryMapDocument(JsonElement element)
    {
        if (!LooksLikeDocument(element)) return null;

        // LoadProxy rows are not guaranteed to have every nullable EBI field populated.
        // Map only the history/header fields we need instead of deserializing the whole
        // server model, which can throw when a nullable server value reaches a non-nullable DTO.
        return new StockGrnDocumentDTO
        {
            DocumentID = ReadString(element, "documentID"),
            DocumentTypeID = ReadInt(element, "documentTypeID"),
            FriendlyDocumentName = ReadString(element, "friendlyDocumentName"),
            AlphaCode = ReadString(element, "alphaCode"),
            NumericCode = ReadInt(element, "numericCode"),
            BranchID = ReadString(element, "branchID"),
            EditBranchID = ReadString(element, "editBranchID"),
            DisplayCode = ReadString(element, "displayCode"),
            FinancialDate = ReadDate(element, "financialDate") ?? DateTime.Today,
            AccountID = ReadString(element, "accountID"),
            AccountName = ReadString(element, "accountName"),
            ReferenceNumber = ReadString(element, "referenceNumber"),
            TotalBeforeTax = ReadDecimal(element, "totalBeforeTax"),
            TaxableAmount = ReadDecimal(element, "taxableAmount"),
            TaxAmount = ReadDecimal(element, "taxAmount"),
            RoundingAmount = ReadDecimal(element, "roundingAmount"),
            TotalAfterTax = ReadDecimal(element, "totalAfterTax"),
            LocalTotalAfterTax = ReadDecimal(element, "localTotalAfterTax"),
            TransactionCurrencyID = ReadString(element, "transactionCurrencyID"),
            LocalCurrencyID = ReadString(element, "localCurrencyID"),
            ExchangeRate = Math.Max(1m, ReadDecimal(element, "exchangeRate")),
            CreatedByDocumentTypeID = ReadInt(element, "createdByDocumentTypeID"),
            CreatedByDocumentTypeName = ReadString(element, "createdByDocumentTypeName"),
            CreatedByDocumentID = ReadString(element, "createdByDocumentID"),
            CreatedByDocumentDisplayCode = ReadString(element, "createdByDocumentDisplayCode"),
            IsLocked = ReadBool(element, "isLocked"),
            IsVoid = ReadBool(element, "isVoid"),
            OrderBranchID = ReadString(element, "orderBranchID"),
            Remarks = ReadString(element, "remarks"),
            StockActivityType = ReadString(element, "stockActivityType"),
            VerifyStatus = ReadString(element, "verifyStatus")
        };
    }

    private static bool LooksLikeDocument(JsonElement element) =>
        TryGet(element, "documentID", out _) ||
        TryGet(element, "displayCode", out _) ||
        TryGet(element, "documentTypeID", out _) ||
        TryGet(element, "financialDate", out _);

    private static string? ReadString(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return decimal.TryParse(value.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number != 0;
        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static DateTime? ReadDate(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out var date)) return date;
        return DateTime.TryParse(value.ToString(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces,
            out var parsed) ? parsed : null;
    }

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
