using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.EF;
using EBI.Enum;
using EBI.UC;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class CustomerAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public CustomerAC(IAuthService authService)
    {
        this.authService = authService;
    }

   

    public async Task<ApiCallResult<List<CustomerDM>>> SearchByWordAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Customer/SearchByWord", new CustomerLookupDTO
        {
            Id = keyword ?? string.Empty
        });

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<List<CustomerDM>>(
            response,
            "Customer search response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<CustomerDM>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Customer/LoadRecord", new CustomerLookupDTO
        {
            Id = id
        });

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<CustomerDM>(
            response,
            "Customer record response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<MemberBalanceSummaryDTO>> GetMemberBalanceSummaryAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        // Match Senang Retails exactly: POST application/json with { "id": customerId }.
        var requestJson = JsonSerializer.Serialize(new { id = customerId }, JsonOptions);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/Customer/GetMemberBalanceSummary")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<MemberBalanceSummaryDTO>(
            response,
            "Member balance summary response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<List<MemberOtherBalanceSummaryDTO>>> GetMemberOtherBalanceSummaryAsync(
        string customerId,
        DateTime cutOffDate,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest(
            "/api/WebDashboard/GetMemberOtherBalanceSummary",
            new MemberOtherBalanceSummaryRequestDTO
            {
                Id = customerId,
                CutOffDate = cutOffDate
            });

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<List<MemberOtherBalanceSummaryDTO>>(
            response,
            "Member outstanding balance response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<List<PackageBalanceDetailDTO>>> GetPackageBalanceDetailsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Customer/GetPackageBalanceDetails", new CustomerLookupDTO { Id = customerId });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<List<PackageBalanceDetailDTO>>(response, "Package balance response was invalid.", cancellationToken);
    }

    public async Task<ApiCallResult<List<CreditBalanceDetailDTO>>> GetCreditBalanceDetailsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Customer/GetCreditBalanceDetails", new CustomerLookupDTO { Id = customerId });
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<List<CreditBalanceDetailDTO>>(response, "Credit balance response was invalid.", cancellationToken);
    }
    public async Task<ApiCallResult<List<RedeemableCreditDTO>>> GetRedeemableCreditsAsync(
        string customerId,
        DateTime purchaseCutOffDate,
        CancellationToken cancellationToken = default)
    {
        var payload = new RedeemableCreditRequestDTO
        {
            CustomerId = customerId,
            PurchaseCutOffDate = purchaseCutOffDate
        };

        var requestJson = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/Customer/GetRedeemableCredits")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<List<RedeemableCreditDTO>>(
            response,
            "Redeemable member credit response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<List<CreditRedemptionHistoryDTO>>> GetCreditRedemptionHistoryAsync(
        string arapOutstandingId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest(
            "/api/ARAPOutstanding_MemberCredit/GetRedemptionHistory",
            new CustomerLookupDTO { Id = arapOutstandingId });

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadApiResponseAsync<List<CreditRedemptionHistoryDTO>>(
            response,
            "Member Credit redemption history response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<CustomerCreateResultDTO>> CreateRecordAsync(
        CustomerDM customer,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Customer/CreateRecord", customer);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<CustomerCreateResultDTO>(
            response,
            "Create customer response was invalid.",
            cancellationToken);
    }
  
    public async Task<ApiCallResult<string>> UpdateRecordAsync(
        CustomerDM customer,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePutRequest("/api/Customer/UpdateRecord", customer);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<string>(
            response,
            "Update customer response was invalid.",
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "Customer API request failed."));
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
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? "Customer API request failed." : apiResponse.Message);
                }

                if (apiResponse.Result is null)
                {
                    return ApiCallResult<T>.Failure(response.StatusCode, invalidResponseMessage);
                }

                return ApiCallResult<T>.Ok(response.StatusCode, apiResponse.Result);
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
