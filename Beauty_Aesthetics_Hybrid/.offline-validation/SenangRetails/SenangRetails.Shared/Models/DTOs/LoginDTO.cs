using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class LoginDTO
    {
        public string email { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
}
