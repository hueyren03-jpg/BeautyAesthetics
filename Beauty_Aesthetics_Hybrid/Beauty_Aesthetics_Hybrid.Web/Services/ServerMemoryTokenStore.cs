using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebProtectedTokenStore(ProtectedLocalStorage storage) : ITokenStore
{
    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";

    public async Task SaveTokensAsync(string accessToken, string refreshToken)
    {
        await storage.SetAsync(AccessTokenKey, accessToken);
        await storage.SetAsync(RefreshTokenKey, refreshToken);
    }

    public Task<string?> GetAccessTokenAsync()
    {
        return GetTokenAsync(AccessTokenKey);
    }

    public Task<string?> GetRefreshTokenAsync()
    {
        return GetTokenAsync(RefreshTokenKey);
    }

    public async Task ClearAsync()
    {
        await DeleteIfAvailableAsync(AccessTokenKey);
        await DeleteIfAvailableAsync(RefreshTokenKey);
    }

    private async Task<string?> GetTokenAsync(string key)
    {
        try
        {
            var result = await storage.GetAsync<string>(key);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private async Task DeleteIfAvailableAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}