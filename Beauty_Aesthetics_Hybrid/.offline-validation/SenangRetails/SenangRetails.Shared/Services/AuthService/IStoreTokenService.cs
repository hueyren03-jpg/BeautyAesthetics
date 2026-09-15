namespace SenangRetails.Shared.Services.AuthService
{
    public interface IStoreTokenService
    {
        Task SaveTokenAsync(string token, string refreshToken);
        Task<string?> GetTokenAsync();
        Task ClearAsync();
    }
}
