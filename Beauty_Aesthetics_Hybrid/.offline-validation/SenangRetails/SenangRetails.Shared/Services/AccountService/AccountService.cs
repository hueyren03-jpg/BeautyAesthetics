using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.Services.AccountService
{
    public class AccountService : IAccountService
    {
        private readonly AccountAC _ac;
        private readonly IStoreTokenService _tokenService;

        public AccountService(AccountAC ac, IStoreTokenService tokenService)
        {
            _ac = ac;
            _tokenService = tokenService;
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword, string confirmPassword)
        {
            try
            {
                var (code, message, newAccessToken, newRefreshToken) =
                    await _ac.ChangePasswordAsync(oldPassword, newPassword, confirmPassword);

                if (code == 200)
                {
                    // Persist the new tokens so the user stays logged in with the new password.
                    if (!string.IsNullOrEmpty(newAccessToken) && !string.IsNullOrEmpty(newRefreshToken))
                        await _tokenService.SaveTokenAsync(newAccessToken, newRefreshToken);

                    return (true, message);
                }

                var detail = !string.IsNullOrWhiteSpace(message)
                    ? message
                    : $"Server returned code {code}.";

                return (false, detail);
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }
    }
}
