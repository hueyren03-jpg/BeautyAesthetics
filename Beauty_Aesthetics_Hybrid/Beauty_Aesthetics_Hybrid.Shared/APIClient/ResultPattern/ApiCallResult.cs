using System.Net;

namespace Beauty_Aesthetics_WebPos.APIClient.ResultPattern;

public sealed class ApiCallResult<T>
{
    private ApiCallResult(bool success, HttpStatusCode statusCode, T? value, string? errorMessage)
    {
        Success = success;
        StatusCode = statusCode;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public HttpStatusCode StatusCode { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public bool IsUnauthorized => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    public static ApiCallResult<T> Ok(HttpStatusCode statusCode, T value) => new(true, statusCode, value, null);

    public static ApiCallResult<T> Failure(HttpStatusCode statusCode, string message) => new(false, statusCode, default, message);

    public static ApiCallResult<T> Unauthorized(HttpStatusCode statusCode) => new(false, statusCode, default, "Unauthorized");
}
