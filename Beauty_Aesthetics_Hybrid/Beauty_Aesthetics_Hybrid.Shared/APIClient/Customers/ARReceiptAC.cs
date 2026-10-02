using System.Net;
using System.Text;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.UC;

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

    public async Task<ApiCallResult<OutstandingSettlementSaveResultDTO>> CreateRecordAsync(
        Doc_ARReceipt receipt,
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
