namespace SenangRetails.Shared.Services.AccountService
{
    public interface IAccountService
    {
        Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword, string confirmPassword);
    }
}
