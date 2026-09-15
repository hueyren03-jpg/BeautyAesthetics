using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using SenangRetails.Shared.Services.AuthService;
using System.Diagnostics;

namespace SenangRetails.Web.Services.TokenSessionStorage
{
    public class StoreTokenService : IStoreTokenService
    {
        private readonly ProtectedSessionStorage _sessionStorage;

        public StoreTokenService(ProtectedSessionStorage sessionStorage)
        {
            _sessionStorage = sessionStorage;
        }
        public async Task ClearAsync()
        {
            await _sessionStorage.DeleteAsync("authToken");
            await _sessionStorage.DeleteAsync("refreshToken");
        }

        public async Task<string?> GetTokenAsync()
        {
            var result = await _sessionStorage.GetAsync<string>("authToken");
            return result.Success ? result.Value : null;
        }

        public async Task SaveTokenAsync(string token, string refreshToken)
        {
            Console.WriteLine($"Saving token: {token}");
            Console.WriteLine($"Saving token: {refreshToken}");

            await _sessionStorage.SetAsync("authToken", token);
            await _sessionStorage.SetAsync("refreshToken", refreshToken);
        }
    }
}
