using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class BranchLookupDTO
{
    [JsonPropertyName("branchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("displayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    [JsonPropertyName("masterAccountID")]
    public string? MasterAccountID { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("accountStatus")]
    public string? AccountStatus { get; set; }

    [JsonPropertyName("branchGroupID")]
    public string? BranchGroupID { get; set; }

    [JsonPropertyName("groupID")]
    public string? GroupID { get; set; }
}
