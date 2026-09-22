using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class CustomerRatingAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public CustomerRatingAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<bool>> CreateRecordAsync(
        CustomerRating rating,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/CustomerRating/CreateRecord")
        {
            Content = JsonContent.Create(rating, mediaType: JsonPatchMediaType, options: JsonOptions)
        };
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<bool>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<bool>.Failure(
                response.StatusCode,
                ReadError(responseBody, "Customer rating API request failed."));
        }

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(responseBody, JsonOptions);
                if (envelope?.StatusCode > 0 && !envelope.IsSuccess)
                {
                    return ApiCallResult<bool>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(envelope.Message)
                            ? "Customer rating API request failed."
                            : envelope.Message);
                }
            }
            catch (JsonException)
            {
                // A successful plain-text response is valid for this mutation endpoint.
            }
        }

        return ApiCallResult<bool>.Ok(response.StatusCode, true);
    }

    private static string ReadError(string responseBody, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            foreach (var name in new[] { "message", "detail", "title" })
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString() ?? fallback;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return responseBody.Trim();
        }

        return fallback;
    }
}
