using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.Entities;

public sealed class AuthResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    public AuthTokenSet ToTokenSet()
    {
        return new AuthTokenSet(AccessToken, RefreshToken);
    }
}

public sealed class ApiAuthResult
{
    [JsonPropertyName("authResponse")]
    public AuthResponse AuthResponse { get; set; } = new();

    public AuthTokenSet ToTokenSet()
    {
        return AuthResponse.ToTokenSet();
    }
}

public sealed record AuthTokenSet(string AccessToken, string RefreshToken)
{
    public static AuthTokenSet Empty { get; } = new(string.Empty, string.Empty);
}
