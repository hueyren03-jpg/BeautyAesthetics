namespace Beauty_Aesthetics_WebPos.Components.Services.Auth;

public sealed class AuthApiOptions
{
    public const string DefaultBaseUrl = "https://ebisoftware.com.my:5000";

    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
