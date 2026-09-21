using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class StockTransferAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private readonly IAuthService authService;

    public StockTransferAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<StockTransferDocumentDTO>>> LoadProxyAsync(
        StockTransferProxyRequestDTO requestDto,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/Doc_StockTransfer/LoadProxy", requestDto);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadTransferListResponseAsync(response, cancellationToken);
    }

    public Task<ApiCallResult<StockTransferEnvelopeDTO>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<StockTransferEnvelopeDTO>(
            HttpMethod.Post,
            "/api/Doc_StockTransfer/LoadRecord",
            new StockTransferLookupDTO { Id = id },
            "Stock transfer record response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<StockTransferDocumentDTO>> GetDMAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<StockTransferDocumentDTO>(
            HttpMethod.Post,
            "/api/Doc_StockTransfer/GetDM",
            new StockTransferLookupDTO { Id = id },
            "Stock transfer document response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> CreateRecordAsync(
        StockTransferEnvelopeDTO transfer,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_StockTransfer/CreateRecord",
            transfer,
            "Create stock transfer response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> UpdateRecordAsync(
        StockTransferEnvelopeDTO transfer,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_StockTransfer/UpdateRecord",
            transfer,
            "Update stock transfer response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> SaveDMAsync(
        StockTransferDocumentDTO transfer,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_StockTransfer/SaveDM",
            transfer,
            "Save stock transfer response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Doc_StockTransfer/Delete",
            new StockTransferLookupDTO { Id = id },
            "Delete stock transfer response was invalid.",
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
        return await ReadMutationResponseAsync(response, invalidResponseMessage, cancellationToken);
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

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, responseBody, "Stock transfer API request failed."));
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
                            ? "Stock transfer API request failed."
                            : apiResponse.Message);
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

    private static async Task<ApiCallResult<List<StockTransferDocumentDTO>>> ReadTransferListResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<StockTransferDocumentDTO>>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<StockTransferDocumentDTO>>.Failure(
                response.StatusCode,
                BuildApiErrorMessage(response.StatusCode, body, "Unable to load stock transfer records."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<List<StockTransferDocumentDTO>>.Failure(
                response.StatusCode,
                "Stock transfer list response was empty.");
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (TryGet(root, "statusCode", out var status) &&
                ReadInt(status) is var apiStatus &&
                apiStatus > 0 &&
                apiStatus is < 200 or >= 300)
            {
                return ApiCallResult<List<StockTransferDocumentDTO>>.Failure(
                    response.StatusCode,
                    FindString(root, "message") ?? "Unable to load stock transfer records.");
            }

            var payload = TryGet(root, "result", out var result) ? result : root;
            var documents = new List<StockTransferDocumentDTO>();
            ExtractTransferDocuments(payload, documents);

            return ApiCallResult<List<StockTransferDocumentDTO>>.Ok(response.StatusCode, documents);
        }
        catch (JsonException)
        {
            return ApiCallResult<List<StockTransferDocumentDTO>>.Failure(
                response.StatusCode,
                "Stock transfer list response was invalid.");
        }
    }

    private static void ExtractTransferDocuments(
        JsonElement element,
        List<StockTransferDocumentDTO> documents)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var document = TryMapTransferDocument(item);
                if (document is not null)
                {
                    documents.Add(document);
                    continue;
                }

                ExtractTransferDocuments(item, documents);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var mapped = TryMapTransferDocument(element);
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
                ExtractTransferDocuments(property.Value, documents);
            }
        }
    }

    private static StockTransferDocumentDTO? TryMapTransferDocument(JsonElement element)
    {
        if (!LooksLikeTransferDocument(element)) return null;

        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "isLoading", "documentID", "documentTypeID", "friendlyDocumentName", "alphaCode",
            "numericCode", "branchID", "editBranchID", "displayCode", "financialDate",
            "referenceNumber", "totalBeforeTax", "taxableAmount", "taxAmount", "roundingAmount",
            "totalAfterTax", "localTotalBeforeTax", "localTaxableAmount", "localTaxAmount",
            "localRoundingAmount", "localTotalAfterTax", "exchangeRate", "isLocked", "isVoid",
            "orderBranchID", "fromBranchID", "toBranchID", "remarks", "fromBranch", "toBranch",
            "isConsignment", "stockTransferBranchGroupID", "saveAction", "isDirty"
        };

        var extensionData = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (!knownNames.Contains(property.Name))
            {
                extensionData[property.Name] = property.Value.Clone();
            }
        }

        return new StockTransferDocumentDTO
        {
            IsLoading = ReadBool(element, "isLoading"),
            DocumentID = ReadString(element, "documentID"),
            DocumentTypeID = ReadInt(element, "documentTypeID"),
            FriendlyDocumentName = ReadString(element, "friendlyDocumentName"),
            AlphaCode = ReadString(element, "alphaCode"),
            NumericCode = ReadInt(element, "numericCode"),
            BranchID = ReadString(element, "branchID"),
            EditBranchID = ReadString(element, "editBranchID"),
            DisplayCode = ReadString(element, "displayCode"),
            FinancialDate = ReadDate(element, "financialDate") ?? DateTime.Today,
            ReferenceNumber = ReadString(element, "referenceNumber"),
            TotalBeforeTax = ReadDecimal(element, "totalBeforeTax"),
            TaxableAmount = ReadDecimal(element, "taxableAmount"),
            TaxAmount = ReadDecimal(element, "taxAmount"),
            RoundingAmount = ReadDecimal(element, "roundingAmount"),
            TotalAfterTax = ReadDecimal(element, "totalAfterTax"),
            LocalTotalBeforeTax = ReadDecimal(element, "localTotalBeforeTax"),
            LocalTaxableAmount = ReadDecimal(element, "localTaxableAmount"),
            LocalTaxAmount = ReadDecimal(element, "localTaxAmount"),
            LocalRoundingAmount = ReadDecimal(element, "localRoundingAmount"),
            LocalTotalAfterTax = ReadDecimal(element, "localTotalAfterTax"),
            ExchangeRate = Math.Max(1m, ReadDecimal(element, "exchangeRate")),
            IsLocked = ReadBool(element, "isLocked"),
            IsVoid = ReadBool(element, "isVoid"),
            OrderBranchID = ReadString(element, "orderBranchID"),
            FromBranchID = ReadString(element, "fromBranchID"),
            ToBranchID = ReadString(element, "toBranchID"),
            Remarks = ReadString(element, "remarks"),
            FromBranch = ReadString(element, "fromBranch"),
            ToBranch = ReadString(element, "toBranch"),
            IsConsignment = ReadBool(element, "isConsignment"),
            StockTransferBranchGroupID = ReadString(element, "stockTransferBranchGroupID"),
            ExtensionData = extensionData.Count == 0 ? null : extensionData
        };
    }

    private static bool LooksLikeTransferDocument(JsonElement element) =>
        TryGet(element, "documentID", out _) ||
        TryGet(element, "displayCode", out _) ||
        TryGet(element, "documentTypeID", out _) ||
        TryGet(element, "financialDate", out _) ||
        TryGet(element, "fromBranchID", out _) ||
        TryGet(element, "toBranchID", out _);

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

    private static string? ReadString(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int ReadInt(JsonElement element, string name) =>
        TryGet(element, name, out var value) ? ReadInt(value) : 0;

    private static int ReadInt(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) ||
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
        if (!TryGet(element, name, out var value) ||
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
        if (!TryGet(element, name, out var value) ||
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "Stock transfer API request failed."));
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
                if (!apiResponse.IsSuccess)
                {
                    return ApiCallResult<string>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? invalidResponseMessage : apiResponse.Message);
                }

                var fallback = string.IsNullOrWhiteSpace(apiResponse.Message) ? "Success" : apiResponse.Message;
                return ApiCallResult<string>.Ok(
                    response.StatusCode,
                    ReadMutationResult(apiResponse.Result, fallback));
            }
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, invalidResponseMessage);
        }

        return ApiCallResult<string>.Ok(response.StatusCode, "Success");
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
                "DocumentID", "DocumentId", "StockTransferDocumentID", "StockTransferDocumentId", "Id", "ID", "DisplayCode"
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
            // Fall through to the status-based message.
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
