using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class StockGrnAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public StockGrnAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<StockGrnDocumentDTO>>> LoadProxyAsync(
        StockGrnProxyRequestDTO requestDto,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Doc_Stock_GRN/LoadProxy", requestDto);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadGrnListResponseAsync(
            response,
            "GRN list response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<StockGrnEnvelopeDTO>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Doc_Stock_GRN/LoadRecord", new StockGrnLookupDTO
        {
            Id = id
        });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<StockGrnEnvelopeDTO>(
            response,
            "GRN record response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<StockGrnDocumentDTO>> GetDMAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Doc_Stock_GRN/GetDM", new StockGrnLookupDTO
        {
            Id = id
        });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<StockGrnDocumentDTO>(
            response,
            "GRN document response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<string>> CreateRecordAsync(
        StockGrnEnvelopeDTO grn,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Doc_Stock_GRN/CreateRecord", grn);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadMutationResponseAsync(
            response,
            "Create GRN response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<string>> UpdateRecordAsync(
        StockGrnEnvelopeDTO grn,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePutRequest("/api/Doc_Stock_GRN/UpdateRecord", grn);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadMutationResponseAsync(
            response,
            "Update GRN response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<string>> SaveDMAsync(
        StockGrnDocumentDTO grn,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Doc_Stock_GRN/SaveDM", grn);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadMutationResponseAsync(
            response,
            "Save GRN response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<string>> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Doc_Stock_GRN/Delete", new StockGrnLookupDTO
        {
            Id = id
        });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadMutationResponseAsync(
            response,
            "Delete GRN response was invalid.",
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "GRN API request failed."));
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
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? "GRN API request failed." : apiResponse.Message);
                }

                return apiResponse.Result is null
                    ? ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage)
                    : ApiCallResult<T>.Ok(response.StatusCode, apiResponse.Result);
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

    private static async Task<ApiCallResult<List<StockGrnDocumentDTO>>> ReadGrnListResponseAsync(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "GRN API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(response.StatusCode, invalidResponseMessage);
        }

        try
        {
            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            if (TryGetPropertyIgnoreCase(root, "statusCode", out var statusElement) &&
                statusElement.TryGetInt32(out var apiStatusCode) &&
                apiStatusCode is < 200 or >= 300)
            {
                return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(
                    response.StatusCode,
                    FindString(root, "message") ?? "GRN API request failed.");
            }

            var payload = TryGetPropertyIgnoreCase(root, "result", out var resultElement)
                ? resultElement
                : root;
            var documents = new List<StockGrnDocumentDTO>();
            ExtractGrnDocuments(payload, documents);

            return ApiCallResult<List<StockGrnDocumentDTO>>.Ok(response.StatusCode, documents);
        }
        catch (JsonException)
        {
            return ApiCallResult<List<StockGrnDocumentDTO>>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static void ExtractGrnDocuments(JsonElement element, List<StockGrnDocumentDTO> documents)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var document = TryMapGrnDocument(item);
                if (document is not null)
                {
                    documents.Add(document);
                    continue;
                }

                ExtractGrnDocuments(item, documents);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var mapped = TryMapGrnDocument(element);
        if (mapped is not null)
        {
            documents.Add(mapped);
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Contains("line", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                ExtractGrnDocuments(property.Value, documents);
            }
        }
    }

    private static StockGrnDocumentDTO? TryMapGrnDocument(JsonElement element)
    {
        if (!LooksLikeGrnDocument(element)) return null;

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
            PaymentTermID = ReadString(element, "paymentTermID"),
            PaymentTermName = ReadString(element, "paymentTermName"),
            OrderBranchID = ReadString(element, "orderBranchID"),
            PODocumentID = ReadString(element, "poDocumentID"),
            PODisplayCode = ReadString(element, "poDisplayCode"),
            Remarks = ReadString(element, "remarks"),
            StockActivityType = ReadString(element, "stockActivityType"),
            VerifyStatus = ReadString(element, "verifyStatus")
        };
    }

    private static bool LooksLikeGrnDocument(JsonElement element) =>
        TryGetPropertyIgnoreCase(element, "documentID", out _) ||
        TryGetPropertyIgnoreCase(element, "displayCode", out _) ||
        TryGetPropertyIgnoreCase(element, "documentTypeID", out _) ||
        TryGetPropertyIgnoreCase(element, "financialDate", out _);

    private static string? ReadString(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return decimal.TryParse(
            value.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) ? parsed : 0;
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number != 0;
        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static DateTime? ReadDate(JsonElement element, string name)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out var date)) return date;
        return DateTime.TryParse(
            value.ToString(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces,
            out var parsed) ? parsed : null;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
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

    private static async Task<ApiCallResult<string>> ReadMutationResponseAsync(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<string>.Unauthorized(response.StatusCode);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<string>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "GRN API request failed."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<string>.Ok(response.StatusCode, "Success");
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(responseBody, JsonOptions);
            if (apiResponse?.StatusCode > 0)
            {
                return apiResponse.IsSuccess
                    ? ApiCallResult<string>.Ok(response.StatusCode, string.IsNullOrWhiteSpace(apiResponse.Message) ? "Success" : apiResponse.Message)
                    : ApiCallResult<string>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? invalidResponseMessage : apiResponse.Message);
            }
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, invalidResponseMessage);
        }

        return ApiCallResult<string>.Ok(response.StatusCode, "Success");
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
