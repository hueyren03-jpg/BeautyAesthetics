using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Services.TokenSecureStorage
{
    /// <summary>
    /// Adapts IMauiTokenService to IStoreTokenService so shared services
    /// (InventoryAC, SupportingTableAC, etc.) can inject IStoreTokenService in MAUI.
    /// </summary>
    public class MauiStoreTokenService : IStoreTokenService
    {
        private readonly IMauiTokenService _inner;

        public MauiStoreTokenService(IMauiTokenService inner)
        {
            _inner = inner;
        }

        public Task SaveTokenAsync(string token, string refreshToken)
            => _inner.SaveTokenAsync(token, refreshToken);

        public Task<string?> GetTokenAsync()
            => _inner.GetTokenAsync();

        public Task ClearAsync()
        {
            _inner.ClearAsync();
            return Task.CompletedTask;
        }
    }
}
