namespace Beauty_Aesthetics_WebPos.Components.Services.Auth;

public interface ITokenStore
{
    Task SaveTokensAsync(string accessToken, string refreshToken);
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task ClearAsync();
}
