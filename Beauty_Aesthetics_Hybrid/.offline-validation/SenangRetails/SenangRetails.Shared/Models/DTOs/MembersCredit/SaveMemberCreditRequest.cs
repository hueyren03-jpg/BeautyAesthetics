using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs.MembersCredit
{
    public class SaveMemberCreditRequest
    {
        [JsonPropertyName("accountID")] public string? AccountID { get; set; }
        [JsonPropertyName("financialDate")] public DateTime FinancialDate { get; set; }
        [JsonPropertyName("dueDate")] public DateTime DueDate { get; set; }
        [JsonPropertyName("documentID")] public string? DocumentID { get; set; }
        [JsonPropertyName("lineItemID")] public string? LineItemID { get; set; }
        [JsonPropertyName("groupID")] public string? GroupID { get; set; }
        [JsonPropertyName("memberTypeID")] public string? MemberTypeID { get; set; }
        [JsonPropertyName("totalAmount")] public double TotalAmount { get; set; }
        [JsonPropertyName("balanceCredit")] public double BalanceCredit { get; set; }
        [JsonPropertyName("branchID")] public string? BranchID { get; set; }
        [JsonPropertyName("exchangeRate")] public double ExchangeRate { get; set; } = 1;
        [JsonPropertyName("isRedeemable")] public bool IsRedeemable { get; set; } = true;
        [JsonPropertyName("saveAction")] public string SaveAction { get; set; } = "Added";
        [JsonPropertyName("isDirty")] public bool IsDirty { get; set; } = true;
    }
}
