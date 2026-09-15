namespace Beauty_Aesthetics_WebPos.Components.Services.Auth;

public interface IAuthService
{
    string? CurrentUserJson { get; }

    Task<bool> HasStoredSessionAsync();

    Task<AuthResult> EnsureAuthenticatedAsync();

    Task<AuthResult> LoginAsync(string email, string password, bool rememberMe);

    Task<AuthResult> LoadCurrentUserAsync();

    Task<HttpResponseMessage> SendAuthorizedAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshSessionAsync();

    Task LogoutAsync();

  
}

