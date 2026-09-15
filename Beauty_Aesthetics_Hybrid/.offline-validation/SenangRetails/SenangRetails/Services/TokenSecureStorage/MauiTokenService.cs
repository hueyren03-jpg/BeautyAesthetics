using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Services.TokenSecureStorage
{
    public class MauiTokenService : IMauiTokenService
    {
        private const string Key = "access_token";

        // In-memory copy — available immediately within a session, even if SecureStorage has issues.
        // This is a Singleton service, so _memoryToken lives for the lifetime of the app.
        private string? _memoryToken;

        public async Task SaveTokenAsync(string token, string refreshToken)
        {
            _memoryToken = token;
            try
            {
                await SecureStorage.SetAsync(Key, token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Token] SecureStorage.SetAsync failed: {ex.Message}");
            }
        }

        public async Task<string?> GetTokenAsync()
        {
            // Return in-memory token if available — it's the freshest and most reliable.
            if (!string.IsNullOrEmpty(_memoryToken))
                return _memoryToken;

            // Fall back to SecureStorage (e.g. app restarted after a previous session).
            try
            {
                var stored = await SecureStorage.GetAsync(Key);
                if (!string.IsNullOrEmpty(stored))
                {
                    _memoryToken = stored; // Cache it in memory for subsequent calls.
                    return stored;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Token] SecureStorage.GetAsync failed: {ex.Message}");
            }

            return null;
        }

        public void ClearAsync()
        {
            _memoryToken = null;
            try { SecureStorage.Remove(Key); }
            catch { }
        }
    }
}
