using System.Net;
using System.Text;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class ARReceiptAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAuthService authService;

    public ARReceiptAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<OutstandingDocumentDTO>>> RetrieveSettlementLinesAsync(
        string customerId,
        string branchGroupId,
        CancellationToken cancellationToken = default)
    {
        const string endpoint = "/api/Doc_ARReceipt/RetrieveSettlementLines";
        var payload = new OutstandingSettlementLinesRequestDTO
        {
            CustomerID = customerId?.Trim() ?? string.Empty,
            DocumentID = "all",
            BranchGroupID = branchGroupId?.Trim() ?? string.Empty
        };
        var requestBody = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        Console.WriteLine($"[API POST {endpoint}] URL: {endpoint}");
        Console.WriteLine($"[API POST {endpoint}] Request Body: {requestBody}");

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine(
            $"[API POST {endpoint}] HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine(
            $"[API POST {endpoint}] Response Body: {(string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody)}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<OutstandingDocumentDTO>>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<OutstandingDocumentDTO>>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Unable to load outstanding documents."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<List<OutstandingDocumentDTO>>.Failure(
                response.StatusCode,
                "Outstanding document response was empty.");
        }

        try
        {
            var wrapper = JsonSerializer.Deserialize<ApiResponse<List<OutstandingDocumentDTO>>>(
                responseBody,
                JsonOptions);

            if (wrapper?.StatusCode > 0)
            {
                if (!wrapper.IsSuccess)
                {
                    return ApiCallResult<List<OutstandingDocumentDTO>>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapper.Message)
                            ? "Unable to load outstanding documents."
                            : wrapper.Message);
                }

                return ApiCallResult<List<OutstandingDocumentDTO>>.Ok(
                    response.StatusCode,
                    wrapper.Result ?? new List<OutstandingDocumentDTO>());
            }

            var direct = JsonSerializer.Deserialize<List<OutstandingDocumentDTO>>(
                responseBody,
                JsonOptions);

            return ApiCallResult<List<OutstandingDocumentDTO>>.Ok(
                response.StatusCode,
                direct ?? new List<OutstandingDocumentDTO>());
        }
        catch (JsonException)
        {
            return ApiCallResult<List<OutstandingDocumentDTO>>.Failure(
                response.StatusCode,
                "Outstanding document response was invalid.");
        }
    }

    public async Task<ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>> RetrieveSettlementLinesRawAsync(
        string customerId,
        string branchGroupId,
        CancellationToken cancellationToken = default)
    {
        const string endpoint = "/api/Doc_ARReceipt/RetrieveSettlementLines";
        var payload = new OutstandingSettlementLinesRequestDTO
        {
            CustomerID = customerId?.Trim() ?? string.Empty,
            DocumentID = "all",
            BranchGroupID = branchGroupId?.Trim() ?? string.Empty
        };
        var requestBody = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        Console.WriteLine($"[API POST {endpoint}] URL: {endpoint}");
        Console.WriteLine($"[API POST {endpoint}] Request Body: {requestBody}");

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine(
            $"[API POST {endpoint}] HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine(
            $"[API POST {endpoint}] Response Body: {(string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody)}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Unable to load AR settlement lines."));
        }

        try
        {
            var wrapper = JsonSerializer.Deserialize<ApiResponse<List<ud_ARAPPaymentOffSetLineDM>>>(
                responseBody,
                JsonOptions);

            if (wrapper is null)
            {
                return ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>.Failure(
                    response.StatusCode,
                    "AR settlement-line response was invalid.");
            }

            if (!wrapper.IsSuccess)
            {
                return ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>.Failure(
                    response.StatusCode,
                    string.IsNullOrWhiteSpace(wrapper.Message)
                        ? "Unable to load AR settlement lines."
                        : wrapper.Message);
            }

            return ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>.Ok(
                response.StatusCode,
                wrapper.Result ?? new List<ud_ARAPPaymentOffSetLineDM>());
        }
        catch (JsonException)
        {
            return ApiCallResult<List<ud_ARAPPaymentOffSetLineDM>>.Failure(
                response.StatusCode,
                "AR settlement-line response was invalid.");
        }
    }

    public async Task<ApiCallResult<List<OutstandingARReceiptHistoryDTO>>> LoadHistoryAsync(
        OutstandingARReceiptHistoryRequestDTO historyRequest,
        CancellationToken cancellationToken = default)
    {
        const string endpoint = "/api/Doc_ARReceipt/LoadProxy";
        var requestBody = JsonSerializer.Serialize(historyRequest, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        Console.WriteLine($"[API POST {endpoint}] URL: {endpoint}");
        Console.WriteLine($"[API POST {endpoint}] Request Body: {requestBody}");

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine($"[API POST {endpoint}] HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine(
            $"[API POST {endpoint}] Response Body: {(string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody)}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Unable to load Outstanding payment history."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Failure(
                response.StatusCode,
                "Outstanding payment history response was empty.");
        }

        try
        {
            var wrapper = JsonSerializer.Deserialize<ApiResponse<List<OutstandingARReceiptHistoryDTO>>>(
                responseBody,
                JsonOptions);

            if (wrapper?.StatusCode > 0)
            {
                if (!wrapper.IsSuccess)
                {
                    return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapper.Message)
                            ? "Unable to load Outstanding payment history."
                            : wrapper.Message);
                }

                return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Ok(
                    response.StatusCode,
                    wrapper.Result ?? new List<OutstandingARReceiptHistoryDTO>());
            }

            var direct = JsonSerializer.Deserialize<List<OutstandingARReceiptHistoryDTO>>(
                responseBody,
                JsonOptions);

            return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Ok(
                response.StatusCode,
                direct ?? new List<OutstandingARReceiptHistoryDTO>());
        }
        catch (JsonException)
        {
            return ApiCallResult<List<OutstandingARReceiptHistoryDTO>>.Failure(
                response.StatusCode,
                "Outstanding payment history response was invalid.");
        }
    }

    public async Task<ApiCallResult<OutstandingARReceiptDetailDTO>> LoadRecordAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        const string endpoint = "/api/Doc_ARReceipt/LoadRecord";
        var requestBody = JsonSerializer.Serialize(
            new { id = documentId?.Trim() ?? string.Empty },
            JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        Console.WriteLine($"[API POST {endpoint}] URL: {endpoint}");
        Console.WriteLine($"[API POST {endpoint}] Request Body: {requestBody}");

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine($"[API POST {endpoint}] HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine(
            $"[API POST {endpoint}] Response Body: {(string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody)}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<OutstandingARReceiptDetailDTO>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<OutstandingARReceiptDetailDTO>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Unable to load Outstanding payment receipt."));
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<OutstandingARReceiptDetailDTO>.Failure(
                response.StatusCode,
                "Outstanding payment receipt response was empty.");
        }

        try
        {
            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            var apiStatus = TryGetInt(root, "statusCode");
            if (apiStatus.HasValue && (apiStatus.Value < 200 || apiStatus.Value >= 300))
            {
                return ApiCallResult<OutstandingARReceiptDetailDTO>.Failure(
                    response.StatusCode,
                    FindString(root, "message") ?? "Unable to load Outstanding payment receipt.");
            }

            var resultNode = TryGetProperty(root, "result") ?? root;
            if (resultNode.ValueKind != JsonValueKind.Object)
            {
                return ApiCallResult<OutstandingARReceiptDetailDTO>.Failure(
                    response.StatusCode,
                    "Outstanding payment receipt response did not contain a valid record.");
            }

            var receiptNode =
                TryGetProperty(resultNode, "objDoc_ARReceipt")
                ?? TryGetProperty(resultNode, "ObjDoc_ARReceipt")
                ?? TryGetProperty(resultNode, "receipt")
                ?? resultNode;

            var receipt = JsonSerializer.Deserialize<OutstandingARReceiptHistoryDTO>(
                receiptNode.GetRawText(),
                JsonOptions) ?? new OutstandingARReceiptHistoryDTO();

            var settlementNode =
                TryGetProperty(resultNode, "lstud_ARAPPaymentOffSetLineDM")
                ?? TryGetProperty(resultNode, "lstARAPPaymentOffSetLineDM")
                ?? TryGetProperty(resultNode, "settlementLines");

            var settlementLines = settlementNode.HasValue &&
                                  settlementNode.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<ud_ARAPPaymentOffSetLineDM>>(
                      settlementNode.Value.GetRawText(),
                      JsonOptions) ?? new List<ud_ARAPPaymentOffSetLineDM>()
                : new List<ud_ARAPPaymentOffSetLineDM>();

            if (string.IsNullOrWhiteSpace(receipt.DocumentID))
            {
                receipt.DocumentID = documentId?.Trim() ?? string.Empty;
            }

            return ApiCallResult<OutstandingARReceiptDetailDTO>.Ok(
                response.StatusCode,
                new OutstandingARReceiptDetailDTO
                {
                    Receipt = receipt,
                    SettlementLines = settlementLines
                });
        }
        catch (JsonException)
        {
            return ApiCallResult<OutstandingARReceiptDetailDTO>.Failure(
                response.StatusCode,
                "Outstanding payment receipt response was invalid.");
        }
    }

    public async Task<ApiCallResult<OutstandingSettlementSaveResultDTO>> CreateRecordAsync(
        OutstandingARReceiptCreateDTO receipt,
        decimal totalAllocatedAmount,
        int settledDocumentCount,
        CancellationToken cancellationToken = default)
    {
        const string endpoint = "/api/Doc_ARReceipt/CreateRecord";
        var requestBody = JsonSerializer.Serialize(receipt, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        Console.WriteLine($"[API POST {endpoint}] URL: {endpoint}");
        Console.WriteLine($"[API POST {endpoint}] Request Body: {requestBody}");

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine(
            $"[API POST {endpoint}] HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine(
            $"[API POST {endpoint}] Response Body: {(string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody)}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<OutstandingSettlementSaveResultDTO>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<OutstandingSettlementSaveResultDTO>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Unable to save outstanding settlement."));
        }

        try
        {
            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            var apiStatus = TryGetInt(root, "statusCode");
            if (apiStatus.HasValue && (apiStatus.Value < 200 || apiStatus.Value >= 300))
            {
                return ApiCallResult<OutstandingSettlementSaveResultDTO>.Failure(
                    response.StatusCode,
                    FindString(root, "message") ?? "Unable to save outstanding settlement.");
            }

            var resultNode = TryGetProperty(root, "result");
            var id = resultNode.HasValue
                ? FindString(resultNode.Value, "Id") ?? FindString(resultNode.Value, "id") ?? string.Empty
                : string.Empty;
            var displayCode = resultNode.HasValue
                ? FindString(resultNode.Value, "DisplayCode") ?? FindString(resultNode.Value, "displayCode") ?? id
                : id;

            // Step 10: HTTP 200 is not enough. A completed AR Receipt save must
            // return the persisted receipt Id so Beauty never reports a false success.
            if (string.IsNullOrWhiteSpace(id))
            {
                Console.WriteLine(
                    $"[Outstanding Step 10] SAVE RESPONSE INVALID | HTTP={(int)response.StatusCode} | " +
                    $"Reason=Missing Result.Id");

                return ApiCallResult<OutstandingSettlementSaveResultDTO>.Failure(
                    response.StatusCode,
                    "Outstanding settlement returned HTTP 200 but no AR Receipt ID. The payment was not confirmed; refresh Outstanding before retrying.");
            }

            Console.WriteLine(
                $"[Outstanding Step 10] API SAVE CONFIRMED | Id={id} | " +
                $"DisplayCode={displayCode} | Amount={totalAllocatedAmount:N2} | " +
                $"Documents={settledDocumentCount}");

            return ApiCallResult<OutstandingSettlementSaveResultDTO>.Ok(
                response.StatusCode,
                new OutstandingSettlementSaveResultDTO
                {
                    Id = id,
                    DisplayCode = displayCode,
                    TotalAllocatedAmount = totalAllocatedAmount,
                    SettledDocumentCount = settledDocumentCount
                });
        }
        catch (JsonException)
        {
            return ApiCallResult<OutstandingSettlementSaveResultDTO>.Failure(
                response.StatusCode,
                "Outstanding settlement save response was invalid.");
        }
    }

    private static JsonElement? TryGetProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static int? TryGetInt(JsonElement element, string name)
    {
        var value = TryGetProperty(element, name);
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number &&
            value.Value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        return int.TryParse(value.Value.ToString(), out parsed)
            ? parsed
            : null;
    }

    private static string ReadError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return FindString(json.RootElement, "detail")
                   ?? FindString(json.RootElement, "message")
                   ?? FindString(json.RootElement, "title")
                   ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? FindString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

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
