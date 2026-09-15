namespace Beauty_Aesthetics_WebPos.Components.Services.Auth;

public sealed class AuthResult
{
    private AuthResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public string? ErrorMessage { get; }

    public static AuthResult Ok() => new(true, null);

    public static AuthResult Fail(string message) => new(false, message);
}
