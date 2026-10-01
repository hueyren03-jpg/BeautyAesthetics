using System.Net;
using System.Text;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

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
