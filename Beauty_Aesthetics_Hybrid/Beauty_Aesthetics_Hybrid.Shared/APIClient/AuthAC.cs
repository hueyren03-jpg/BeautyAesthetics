using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using Beauty_Aesthetics_WebPos.Models.Entities;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class AuthAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;

    public AuthAC(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<ApiCallResult<AuthTokenSet>> LoginAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        var request = new LoginDTO
        {
            Email = email,
            Password = password,
            RememberMe = rememberMe,
            ReturnUrl = string.Empty
        };

        using var response = await httpClient.PostAsJsonAsync("/api/Account/Login", request, cancellationToken);

        return await ReadTokenResponseAsync(response, "Login response was invalid.", cancellationToken);
    }

    public async Task<ApiCallResult<AuthTokenSet>> RefreshTokenAsync(
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var request = new RefreshTokenDTO
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken 
        };

        using var response = await httpClient.PostAsJsonAsync("/api/Account/RefreshToken", request, cancellationToken);

        return await ReadTokenResponseAsync(response, "Session response was invalid.", cancellationToken);
    }

    public async Task<ApiCallResult<string>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/Manage/GetCurrentUser");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.RequestTimeout,
                "The session verification request timed out.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "Unable to connect to the API server.");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return ApiCallResult<string>.Unauthorized(response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<string>.Failure(response.StatusCode, "Unable to load the signed-in user.");
            }

            var currentUserJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return ApiCallResult<string>.Ok(response.StatusCode, currentUserJson);
        }
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<ApiCallResult<AuthTokenSet>> ReadTokenResponseAsync(
        HttpResponseMessage response,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<AuthTokenSet>.Unauthorized(response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<AuthTokenSet>.Failure(response.StatusCode, "Authentication request failed.");
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ApiCallResult<AuthTokenSet>.Failure(response.StatusCode, invalidResponseMessage);
        }

        AuthTokenSet tokenSet;
        string? apiMessage = null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<ApiAuthResult>>(responseBody, JsonOptions);

            if (apiResponse?.StatusCode > 0)
            {
                apiMessage = apiResponse.Message;
                if (!apiResponse.IsSuccess)
                {
                    return ApiCallResult<AuthTokenSet>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? "Authentication request failed." : apiResponse.Message);
                }

                tokenSet = apiResponse.Result?.ToTokenSet() ?? AuthTokenSet.Empty;
                if (string.IsNullOrWhiteSpace(tokenSet.AccessToken) || string.IsNullOrWhiteSpace(tokenSet.RefreshToken))
                {
                    tokenSet = ExtractTokens(root);
                }
            }
            else
            {
                var directResponse = JsonSerializer.Deserialize<AuthResponse>(responseBody, JsonOptions);
                tokenSet = directResponse?.ToTokenSet() ?? ExtractTokens(root);
            }
        }
        catch (JsonException)
        {
            return ApiCallResult<AuthTokenSet>.Failure(response.StatusCode, invalidResponseMessage);
        }

        if (string.IsNullOrWhiteSpace(tokenSet.AccessToken) || string.IsNullOrWhiteSpace(tokenSet.RefreshToken))
        {
            return ApiCallResult<AuthTokenSet>.Failure(
                response.StatusCode,
                string.IsNullOrWhiteSpace(apiMessage) ? invalidResponseMessage : apiMessage);
        }

        return ApiCallResult<AuthTokenSet>.Ok(response.StatusCode, tokenSet);
    }

    private static AuthTokenSet ExtractTokens(JsonElement root)
    {
        return new AuthTokenSet(
            FindStringValue(root, "accessToken") ?? string.Empty,
            FindStringValue(root, "refreshToken") ?? string.Empty);
    }

    private static string? FindStringValue(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    var nestedValue = FindStringValue(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nestedValue = FindStringValue(item, propertyName);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }

                break;
        }

        return null;
    }
}
