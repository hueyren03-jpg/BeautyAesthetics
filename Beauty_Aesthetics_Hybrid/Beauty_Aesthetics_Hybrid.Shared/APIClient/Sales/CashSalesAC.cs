using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class CashSalesAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public CashSalesAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<List<CashSalesProxyDTO>>> LoadProxyAsync(
        CashSalesLoadRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<CashSalesProxyDTO>>(HttpMethod.Post, "/api/Doc_CashSales/LoadProxy", request,
            "Cash sales list response was invalid.", cancellationToken);

    public async Task<ApiCallResult<List<CashSalesProxyDTO>>> GetAppSalesListAsync(
        CashSalesLoadRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        var branchId = request.Id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(branchId))
        {
            Console.WriteLine(
                "[API POST /api/Doc_CashSales/GetAppSalesList] BLOCKED | strBranches is empty.");

            return ApiCallResult<List<CashSalesProxyDTO>>.Failure(
                HttpStatusCode.BadRequest,
                "Working branch is required before sales history can be loaded.");
        }

        // Keep the exact request contract used by the working master
        // implementation. CashSalesLoadRequestDTO serializes Id as "strBranches".
        request.Id = branchId;

        return await SendAsync<List<CashSalesProxyDTO>>(
            HttpMethod.Post,
            "/api/Doc_CashSales/GetAppSalesList",
            request,
            "Cash sales history response was invalid.",
            cancellationToken);
    }

    public Task<ApiCallResult<List<CashSalesProxyDTO>>> LoadRedemptionProxyAsync(
        RedemptionLoadRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<CashSalesProxyDTO>>(
            HttpMethod.Post,
            "/api/Doc_Redemption/LoadProxy",
            request,
            "Redemption list response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<JsonObject>> LoadRecordAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        SendAsync<JsonObject>(HttpMethod.Post, "/api/Doc_CashSales/LoadRecord",
            new CashSalesLookupDTO { Id = documentId }, "Cash sales record response was invalid.", cancellationToken);

    public Task<ApiCallResult<JsonObject>> LoadRedemptionRecordAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        SendAsync<JsonObject>(HttpMethod.Post, "/api/Doc_Redemption/LoadRecord",
            new CashSalesLookupDTO { Id = documentId }, "Redemption record response was invalid.", cancellationToken);

    public Task<ApiCallResult<CreateCashSalesResponseDTO>> CreateRecordAsync(
        JsonObject document,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateCashSalesResponseDTO>(
            HttpMethod.Post,
            "/api/Doc_CashSales/CreateRecord",
            document,
            "Create cash sale response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> RequestBillDownloadLinkAsync(
        CashSalesBillLinkRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<string>(HttpMethod.Post,
            "/api/Doc_CashSales/RequestBillDownloadLink",
            request,
            "Invoice link response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> RequestEInvoiceDirectSubmitAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        SendAsync<string>(HttpMethod.Post,
            "/api/Doc_CashSales/RequestEInvoice_DirectSubmitMethod",
            new CashSalesLookupDTO { Id = documentId },
            "e-Invoice response was invalid.",
            cancellationToken);

    public async Task<ApiCallResult<string>> GetThermalReceiptPdfAsync(
        string documentId,
        int documentTypeId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/POSReceipt_ThermalPOS")
        {
            Content = JsonContent.Create(
                new CashSalesThermalReceiptRequestDTO
                {
                    Id = documentId,
                    DocumentTypeId = documentTypeId
                },
                options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<string>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<string>.Failure(
                response.StatusCode,
                ReadError(body, "Unable to generate the thermal receipt."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<string>.Failure(
                response.StatusCode,
                "Thermal receipt response was empty.");
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var base64Pdf = FindReceiptBase64(json.RootElement);
            return string.IsNullOrWhiteSpace(base64Pdf)
                ? ApiCallResult<string>.Failure(
                    response.StatusCode,
                    "Thermal receipt PDF was missing from the response.")
                : ApiCallResult<string>.Ok(response.StatusCode, base64Pdf);
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Failure(
                response.StatusCode,
                "Thermal receipt response was invalid.");
        }
    }


    public Task<ApiCallResult<JsonElement>> SaveHeaderAsync(
        JsonObject header,
        CancellationToken cancellationToken = default) =>
        SendAsync<JsonElement>(HttpMethod.Post, "/api/Doc_CashSales/SaveDM", header,
            "Cash sale update response was invalid.", cancellationToken);

    public Task<ApiCallResult<JsonElement>> SaveDocumentLineAsync(
        JsonObject documentLine,
        CancellationToken cancellationToken = default) =>
        SendAsync<JsonElement>(HttpMethod.Post, "/api/DocumentLineTable/Save", documentLine,
            "Cash sale document line update response was invalid.", cancellationToken);

    public Task<ApiCallResult<string>> DeleteAsync(
        CashSalesDeleteDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<string>(HttpMethod.Post, "/api/Doc_CashSales/Delete", request,
            "Delete cash sale response was invalid.", cancellationToken);

    public Task<ApiCallResult<List<CashSalesPaymentTypeDTO>>> LoadPaymentTypesAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<List<CashSalesPaymentTypeDTO>>(HttpMethod.Post,
            "/api/Doc_CashSales_POSPaymentLineType/LoadProxy", payload: null,
            "Payment method response was invalid.", cancellationToken);

    public Task<ApiCallResult<List<CashSalesPaymentTypeDTO>>> LoadSystemPaymentTypesAsync(
        CashSalesPaymentTypeRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<CashSalesPaymentTypeDTO>>(HttpMethod.Post,
            "/api/Doc_CashSales_POSPaymentLineType/GetSystemControlledSalesSettlementType",
            request,
            "Payment method response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<CashSalesReceiptLineDTO>>> LoadReceiptLinesAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<CashSalesReceiptLineDTO>>(HttpMethod.Post,
            "/api/Doc_CashSales_POSReceiptLines/LoadProxyByParentID",
            new CashSalesLookupDTO { Id = documentId }, "Payment lines response was invalid.", cancellationToken);

    public Task<ApiCallResult<string>> SaveReceiptLineAsync(
        CashSalesReceiptLineDTO receiptLine,
        CancellationToken cancellationToken = default) =>
        SendAsync<string>(HttpMethod.Post, "/api/Doc_CashSales_POSReceiptLines/Save", receiptLine,
            "Payment save response was invalid.", cancellationToken);

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        string requestBody = string.Empty;

        if (payload is not null)
        {
            request.Content = JsonContent.Create(
                payload,
                mediaType: JsonPatchMediaType,
                options: JsonOptions);

            requestBody = JsonSerializer.Serialize(payload, JsonOptions);
        }

        var shouldLog = IsCriticalCreditEndpoint(uri);
        if (shouldLog)
        {
            Console.WriteLine($"[API {method.Method} {uri}] URL: {uri}");
            Console.WriteLine(
                $"[API {method.Method} {uri}] Request Body: {SanitizeLogBody(requestBody)}");
        }

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        if (shouldLog)
        {
            Console.WriteLine(
                $"[API {method.Method} {uri}] HTTP {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine(
                $"[API {method.Method} {uri}] Response Body: {SanitizeLogBody(body)}");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<T>.Failure(
                response.StatusCode,
                ReadError(body, "Cash sales API request failed."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions);
            if (apiResponse is null || !apiResponse.IsSuccess || apiResponse.Result is null)
            {
                return ApiCallResult<T>.Failure(
                    response.StatusCode,
                    apiResponse?.Message ?? invalidResponseMessage);
            }

            return ApiCallResult<T>.Ok(response.StatusCode, apiResponse.Result);
        }
        catch (JsonException)
        {
            return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
        }
    }

    private static bool IsCriticalCreditEndpoint(string uri) =>
        uri.Equals(
            "/api/Doc_CashSales/CreateRecord",
            StringComparison.OrdinalIgnoreCase) ||
        uri.Equals(
            "/api/Doc_Redemption/LoadRecord",
            StringComparison.OrdinalIgnoreCase) ||
        uri.Equals(
            "/api/Doc_CashSales/GetAppSalesList",
            StringComparison.OrdinalIgnoreCase);

    private static string SanitizeLogBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "<empty>";
        }

        try
        {
            var node = JsonNode.Parse(body);
            if (node is null)
            {
                return body;
            }

            RedactSensitiveJson(node);
            return node.ToJsonString(JsonOptions);
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static void RedactSensitiveJson(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSensitiveLogField(property.Key))
                {
                    obj[property.Key] = "***REDACTED***";
                }
                else if (property.Value is not null)
                {
                    RedactSensitiveJson(property.Value);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    RedactSensitiveJson(item);
                }
            }
        }
    }

    private static bool IsSensitiveLogField(string name) =>
        name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("secret", StringComparison.OrdinalIgnoreCase);

    private static string? FindReceiptBase64(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            return LooksLikePdfBase64(value) ? value : null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var preferredName in new[] { "Result", "result" })
            {
                if (element.TryGetProperty(preferredName, out var preferred))
                {
                    var value = FindReceiptBase64(preferred);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var value = FindReceiptBase64(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool LooksLikePdfBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("data:application/pdf;base64,", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // PDF files start with %PDF; its Base64 prefix is JVBER.
        return normalized.StartsWith("JVBER", StringComparison.Ordinal);
    }

    private static string ReadError(string body, string fallback)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            foreach (var name in new[] { "detail", "Detail", "message", "Message", "title", "Title" })
            {
                if (root.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
