using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Services.TokenSecureStorage
{
    public interface IMauiTokenService
    {
        Task SaveTokenAsync(string token, string refreshToken);
        Task<string?> GetTokenAsync();
        void ClearAsync();
    }
}
