using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Microsoft.Maui.Storage;

namespace Beauty_Aesthetics_Hybrid.Services;

public sealed class MauiSecureTokenStore : ITokenStore
{
    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";

    public async Task SaveTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public Task<string?> GetAccessTokenAsync()
    {
        return SecureStorage.Default.GetAsync(AccessTokenKey);
    }

    public Task<string?> GetRefreshTokenAsync()
    {
        return SecureStorage.Default.GetAsync(RefreshTokenKey);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }
}
