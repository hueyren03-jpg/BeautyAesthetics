using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class EmployeeAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public EmployeeAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<EmployeeDM>>> GetAllEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Employee/GetAllEmployees");
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<List<EmployeeDM>>(
            response,
            "Employee list response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<EmployeeDM>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Employee/LoadRecord", new EmployeeLookupDTO
        {
            Id = id
        });

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<EmployeeDM>(
            response,
            "Employee record response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<string>> CreateRecordAsync(
        EmployeeDM employee,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Employee/CreateRecord", employee);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadMutationResponseAsync(
            response,
            "Create employee response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<string>> UpdateRecordAsync(
        EmployeeDM employee,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePutRequest("/api/Employee/UpdateRecord", employee);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadMutationResponseAsync(
            response,
            "Update employee response was invalid.",
            cancellationToken);
    }

    public async Task<ApiCallResult<List<EmployeeDM>>> GetActiveEmployeesByBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreatePostRequest("/api/Employee/GetActiveEmployeesByBranch", new EmployeeLookupDTO
        {
            Id = branchId
        });

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        return await ReadApiResponseAsync<List<EmployeeDM>>(
            response,
            "Active employee response was invalid.",
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "Employee API request failed."));
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
                        string.IsNullOrWhiteSpace(apiResponse.Message) ? "Employee API request failed." : apiResponse.Message);
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "Employee API request failed."));
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
