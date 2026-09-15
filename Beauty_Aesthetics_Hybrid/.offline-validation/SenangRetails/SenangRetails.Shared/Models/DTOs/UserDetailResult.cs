using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class UserDetailResult
    {
        [JsonPropertyName("BranchID")]
        public string BranchID { get; set; } = string.Empty;

        [JsonPropertyName("DefaultWorkingBranchID")]
        public string DefaultWorkingBranchID { get; set; } = string.Empty;

        [JsonPropertyName("EmployeeName")]
        public string EmployeeName { get; set; } = string.Empty;

        [JsonPropertyName("UserID")]
        public string UserID { get; set; } = string.Empty;

        [JsonPropertyName("EmployeeID")]
        public string EmployeeID { get; set; } = string.Empty;

        [JsonPropertyName("UserGroupID")]
        public string UserGroupID { get; set; } = string.Empty;

        [JsonPropertyName("UserGroupName")]
        public string UserGroupName { get; set; } = string.Empty;

        [JsonPropertyName("BranchGroupID")]
        public string BranchGroupID { get; set; } = string.Empty;

        [JsonPropertyName("lstSecurities")]
        public Dictionary<string, SecurityPermissionItem>? LstSecurities { get; set; }
    }
}
