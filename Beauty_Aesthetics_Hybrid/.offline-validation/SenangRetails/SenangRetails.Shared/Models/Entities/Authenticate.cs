using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.Entities
{
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ApiAuthResult
    {
        public bool RequiresTwoFactor { get; set; }
        public bool RequiresCompanySelection { get; set; }
        public AuthResponse AuthResponse { get; set; } = new();
    }
}
