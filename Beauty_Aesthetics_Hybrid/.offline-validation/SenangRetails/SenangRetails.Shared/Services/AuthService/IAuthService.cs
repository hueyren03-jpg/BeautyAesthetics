using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.AuthService
{
    public interface IAuthService
    {
        Task<bool> AuthenticateAsync(string email, string password);
    }
}
