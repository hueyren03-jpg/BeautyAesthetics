using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs.UserControl
{
    public class SecurityDM
    {
        public bool IsLoading { get; set; }
        public string AutoID { get; set; } = string.Empty;
        public string UserGroupID { get; set; } = string.Empty;
        public string FormName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool CanViewRecord { get; set; }
        public bool CanAddRecord { get; set; }
        public bool CanEditRecord { get; set; }
        public bool CanDeleteRecord { get; set; }
        public bool CanSearch { get; set; }
        public string ControlType { get; set; } = string.Empty;
        public int SaveAction { get; set; }
        public bool IsDirty { get; set; }
    }
}
