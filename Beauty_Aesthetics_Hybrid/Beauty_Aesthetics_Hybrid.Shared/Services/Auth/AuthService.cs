using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Services;

namespace Beauty_Aesthetics_WebPos.Components.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly AuthAC authAC;
    private readonly ITokenStore tokenStore;
    private readonly AppState appState;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public AuthService(AuthAC authAC, ITokenStore tokenStore, AppState appState)
    {
        this.authAC = authAC;
        this.tokenStore = tokenStore;
        this.appState = appState;
    }

    public string? CurrentUserJson => appState.CurrentUserJson;

    public async Task<bool> HasStoredSessionAsync()
    {
        var accessToken = await tokenStore.GetAccessTokenAsync();
        var refreshToken = await tokenStore.GetRefreshTokenAsync();

        return !string.IsNullOrWhiteSpace(accessToken) || !string.IsNullOrWhiteSpace(refreshToken);
    }

    public async Task<AuthResult> EnsureAuthenticatedAsync()
    {
        if (!string.IsNullOrWhiteSpace(appState.CurrentUserJson))
        {
            return AuthResult.Ok();
        }

        var accessToken = await tokenStore.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var userResult = await LoadCurrentUserAsync(accessToken, allowRefresh: true);
            if (userResult.Success)
            {
                return userResult;
            }
        }

        return await RefreshSessionAsync(loadCurrentUser: true);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Fail("Please enter email and password.");
        }

        try
        {
            var normalizedEmail = email.Trim();
            var loginResponse = await authAC.LoginAsync(normalizedEmail, password, rememberMe);
            if (loginResponse.IsUnauthorized)
            {
                return AuthResult.Fail("Invalid email or password.");
            }

            if (!loginResponse.Success || loginResponse.Value is null)
            {
                return AuthResult.Fail(loginResponse.ErrorMessage ?? "Login failed. Please try again.");
            }

            var tokenResult = loginResponse.Value;
            await tokenStore.SaveTokensAsync(tokenResult.AccessToken, tokenResult.RefreshToken);

            var userResult = await LoadCurrentUserAsync(tokenResult.AccessToken, allowRefresh: false, userEmail: normalizedEmail);
            if (!userResult.Success)
            {
                await tokenStore.ClearAsync();
                return userResult;
            }

            return AuthResult.Ok();
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Unable to connect to the login server.");
        }
        catch (TaskCanceledException)
        {
            return AuthResult.Fail("Login request timed out.");
        }
        catch (JsonException)
        {
            return AuthResult.Fail("Login response was invalid.");
        }
    }

    public async Task<AuthResult> LoadCurrentUserAsync()
    {
        var accessToken = await tokenStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return await RefreshSessionAsync(loadCurrentUser: true);
        }

        return await LoadCurrentUserAsync(accessToken, allowRefresh: true);
    }

    public async Task<HttpResponseMessage> SendAuthorizedAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var firstRequest = await CloneRequestAsync(request, cancellationToken);
        await AttachAccessTokenAsync(firstRequest);

        HttpResponseMessage response;
        try
        {
            response = await authAC.SendAsync(firstRequest, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateSyntheticErrorResponse(
                request,
                HttpStatusCode.RequestTimeout,
                "API request timed out. Please try again.");
        }
        catch (HttpRequestException)
        {
            return CreateSyntheticErrorResponse(
                request,
                HttpStatusCode.ServiceUnavailable,
                "Unable to connect to the API server.");
        }

        // A 403 means the authenticated user lacks permission. Refreshing cannot
        // change authorization and must not clear an otherwise valid session.
        if (response.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();

        var refreshResult = await RefreshSessionAsync(loadCurrentUser: false);
        if (!refreshResult.Success)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request,
                ReasonPhrase = "Authentication required"
            };
        }

        var retryRequest = await CloneRequestAsync(request, cancellationToken);
        await AttachAccessTokenAsync(retryRequest);

        try
        {
            return await authAC.SendAsync(retryRequest, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateSyntheticErrorResponse(
                request,
                HttpStatusCode.RequestTimeout,
                "API request timed out. Please try again.");
        }
        catch (HttpRequestException)
        {
            return CreateSyntheticErrorResponse(
                request,
                HttpStatusCode.ServiceUnavailable,
                "Unable to connect to the API server.");
        }
    }

    public async Task LogoutAsync()
    {
        appState.ClearAuthentication();
        await tokenStore.ClearAsync();
    }

    public async Task<AuthResult> RefreshSessionAsync()
    {
        return await RefreshSessionAsync(loadCurrentUser: true);
    }

    private async Task<AuthResult> RefreshSessionAsync(bool loadCurrentUser)
    {
        await refreshLock.WaitAsync();

        try
        {
            var refreshToken = await tokenStore.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                await LogoutAsync();
                return AuthResult.Fail("Please sign in again.");
            }

            var existingAccessToken = await tokenStore.GetAccessTokenAsync();
            var refreshResponse = await authAC.RefreshTokenAsync(existingAccessToken ?? string.Empty, refreshToken);
            if (refreshResponse.IsUnauthorized)
            {
                await LogoutAsync();
                return AuthResult.Fail("Please sign in again.");
            }

            if (!refreshResponse.Success || refreshResponse.Value is null)
            {
                return AuthResult.Fail(refreshResponse.ErrorMessage ?? "Unable to refresh your session. Please sign in again.");
            }

            var tokenResult = refreshResponse.Value;
            await tokenStore.SaveTokensAsync(tokenResult.AccessToken, tokenResult.RefreshToken);

            if (!loadCurrentUser)
            {
                return AuthResult.Ok();
            }

            return await LoadCurrentUserAsync(tokenResult.AccessToken, allowRefresh: false);
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Unable to connect to the login server.");
        }
        catch (TaskCanceledException)
        {
            return AuthResult.Fail("Session refresh timed out.");
        }
        catch (JsonException)
        {
            await LogoutAsync();
            return AuthResult.Fail("Session response was invalid. Please sign in again.");
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task<AuthResult> LoadCurrentUserAsync(string accessToken, bool allowRefresh, string? userEmail = null)
    {
        var response = await authAC.GetCurrentUserAsync(accessToken);
        if (response.IsUnauthorized)
        {
            if (allowRefresh)
            {
                return await RefreshSessionAsync(loadCurrentUser: true);
            }

            return AuthResult.Fail("Unable to verify the signed-in user.");
        }

        if (!response.Success || string.IsNullOrWhiteSpace(response.Value))
        {
            return AuthResult.Fail(response.ErrorMessage ?? "Unable to load the signed-in user.");
        }

        appState.SetAuthenticatedUser(response.Value, userEmail);
        return AuthResult.Ok();
    }

    private async Task AttachAccessTokenAsync(HttpRequestMessage request)
    {
        var accessToken = await tokenStore.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private static HttpResponseMessage CreateSyntheticErrorResponse(
        HttpRequestMessage request,
        HttpStatusCode statusCode,
        string message)
    {
        return new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            ReasonPhrase = message,
            Content = new StringContent(
                JsonSerializer.Serialize(new { message }),
                Encoding.UTF8,
                "application/json")
        };
    }
}
