using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class AppointmentAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private readonly IAuthService authService;

    public AppointmentAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public Task<ApiCallResult<JsonElement>> GetStatusListAsync(CancellationToken cancellationToken = default) =>
        SendAsync<JsonElement>(
            HttpMethod.Get,
            "/api/Appointment/GetAppointmentStatusList",
            null,
            "Appointment status response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<AppointmentDM>>> GetAllAppointmentsAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<List<AppointmentDM>>(
            HttpMethod.Post,
            "/api/Appointment/GetAllAppointments",
            null,
            "Appointment list response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<AppointmentDM>>> GetAppointmentsAsync(
        AppointmentCustomerRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<AppointmentDM>>(
            HttpMethod.Post,
            "/api/Appointment/GetAppointments",
            request,
            "Customer appointment response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<AppointmentDM>>> GetByEmployeeAsync(
        AppointmentEmployeeRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<AppointmentDM>>(
            HttpMethod.Post,
            "/api/Appointment/GetByEmployee",
            request,
            "Employee appointment response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<List<AppointmentDM>>> GetByEmployeeAndStatusAsync(
        AppointmentEmployeeStatusRequestDTO request,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<AppointmentDM>>(
            HttpMethod.Post,
            "/api/Appointment/GetByEmployeeAndStatus",
            request,
            "Filtered appointment response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> CreateAppointmentAsync(
        AppointmentDM appointment,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Post,
            "/api/Appointment/CreateAppointment",
            appointment,
            "Create appointment response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> UpdateRecordAsync(
        AppointmentDM appointment,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(
            HttpMethod.Put,
            "/api/Appointment/UpdateRecord",
            appointment,
            "Update appointment response was invalid.",
            cancellationToken);

    public Task<ApiCallResult<string>> DeleteAppointmentAsync(
        string appointmentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appointmentId))
        {
            return Task.FromResult(ApiCallResult<string>.Failure(
                HttpStatusCode.BadRequest,
                "The selected appointment has no API record ID."));
        }

        var uri = $"/api/Appointment/DeleteAppointment?id={Uri.EscapeDataString(appointmentId)}";
        return SendMutationAsync(
            HttpMethod.Delete,
            uri,
            null,
            "Delete appointment response was invalid.",
            cancellationToken);
    }
    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? payload,
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
        object? payload,
        string invalidResponseMessage,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, payload);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        return await ReadMutationResponseAsync(response, invalidResponseMessage, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? payload)
    {
        var request = new HttpRequestMessage(method, uri);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, mediaType: JsonPatchMediaType, options: JsonOptions);
        }

        return request;
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "Appointment API request failed."));
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
                            ? "Appointment API request failed."
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
                BuildApiErrorMessage(response.StatusCode, responseBody, "Appointment API request failed."));
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

                var id = apiResponse.Result.ValueKind == JsonValueKind.String
                    ? apiResponse.Result.GetString()
                    : FindString(apiResponse.Result, "appointmentID")
                      ?? FindString(apiResponse.Result, "id");
                var value = !string.IsNullOrWhiteSpace(id)
                    ? id
                    : string.IsNullOrWhiteSpace(apiResponse.Message) ? "Success" : apiResponse.Message;
                return ApiCallResult<string>.Ok(response.StatusCode, value);
            }
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, invalidResponseMessage);
        }

        return ApiCallResult<string>.Ok(response.StatusCode, "Success");
    }

    private static string BuildApiErrorMessage(
        HttpStatusCode statusCode,
        string responseBody,
        string fallbackMessage)
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
            if (!string.IsNullOrWhiteSpace(message)) return message;

            var title = FindString(root, "title");
            var detail = FindString(root, "detail");
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(detail))
                return $"{title}: {detail}";
            if (!string.IsNullOrWhiteSpace(detail)) return detail;
            if (!string.IsNullOrWhiteSpace(title)) return $"{title} ({(int)statusCode}).";
        }
        catch (JsonException)
        {
        }

        return $"{fallbackMessage} ({(int)statusCode}).";
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

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
