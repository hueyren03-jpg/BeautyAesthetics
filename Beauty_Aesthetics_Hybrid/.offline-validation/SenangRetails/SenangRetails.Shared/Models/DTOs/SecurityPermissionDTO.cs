using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class SecurityPermissionItem
    {
        [JsonPropertyName("AutoID")]          public string? AutoID          { get; set; }
        [JsonPropertyName("UserGroupID")]     public string? UserGroupID     { get; set; }
        [JsonPropertyName("FormName")]        public string? FormName        { get; set; }
        [JsonPropertyName("ModuleName")]      public string? ModuleName      { get; set; }
        [JsonPropertyName("Category")]        public string? Category        { get; set; }
        [JsonPropertyName("CanViewRecord")]   public bool    CanViewRecord   { get; set; }
        [JsonPropertyName("CanAddRecord")]    public bool    CanAddRecord    { get; set; }
        [JsonPropertyName("CanEditRecord")]   public bool    CanEditRecord   { get; set; }
        [JsonPropertyName("CanDeleteRecord")] public bool    CanDeleteRecord { get; set; }
        [JsonPropertyName("CanSearch")]       public bool    CanSearch       { get; set; }
        [JsonPropertyName("ControlType")]     public string? ControlType     { get; set; }
        [JsonPropertyName("SaveAction")]      public int     SaveAction      { get; set; }
        [JsonPropertyName("IsDirty")]         public bool    IsDirty         { get; set; }
    }

    public class SecurityUserUpdateDM
    {
        [JsonPropertyName("UserID")]                 public string? UserID                 { get; set; }
        [JsonPropertyName("EmployeeID")]             public string? EmployeeID             { get; set; }
        [JsonPropertyName("UserGroupID")]            public string? UserGroupID            { get; set; }
        [JsonPropertyName("UserGroupName")]          public string? UserGroupName          { get; set; }
        [JsonPropertyName("BranchGroupID")]          public string? BranchGroupID          { get; set; }
        [JsonPropertyName("BranchID")]               public string? BranchID               { get; set; }
        [JsonPropertyName("DefaultWorkingBranchID")] public string? DefaultWorkingBranchID { get; set; }
        [JsonPropertyName("lstSecurities")]          public Dictionary<string, SecurityPermissionItem>? LstSecurities { get; set; }
        [JsonPropertyName("SaveAction")]             public int     SaveAction             { get; set; } = 2;
        [JsonPropertyName("IsDirty")]                public bool    IsDirty                { get; set; } = true;
    }
}
