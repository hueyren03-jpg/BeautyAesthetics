using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class EmailSettingDto
    {
        public string emailUsername { get; set; } = string.Empty;
        public string emailPassword { get; set; } = string.Empty;
        public string emailSmtp { get; set; } = string.Empty;
        public int emailPort { get; set; }
        public bool emailEnableSSL { get; set; } = true;
        public string branchID { get; set; }
        public string emailAddress { get; set; } = string.Empty;
        public string emailDisplayName { get; set; } = string.Empty;
        public int SaveAction { get; set; }
    }

    public class EmailSettingUpdateResult
    {
        public string? Id { get; set; }
        public string? DisplayCode { get; set; }
        public string? SuccessMessage { get; set; }
    }
}
