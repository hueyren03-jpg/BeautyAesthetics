using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class PendingStockReceiptLookupDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class PendingStockReceiptLineDTO
{
    [JsonPropertyName("AccountName")]
    public string? AccountName { get; set; }

    [JsonPropertyName("ItemDisplayCode")]
    public string? ItemDisplayCode { get; set; }

    [JsonPropertyName("InventoryMovementID")]
    public string? InventoryMovementID { get; set; }

    [JsonPropertyName("FinancialDate")]
    public DateTime FinancialDate { get; set; }

    [JsonPropertyName("DocumentID")]
    public string? DocumentID { get; set; }

    [JsonPropertyName("DisplayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("DocumentTypeID")]
    public int DocumentTypeID { get; set; }

    [JsonPropertyName("DocumentLineID")]
    public string? DocumentLineID { get; set; }

    [JsonPropertyName("LineItemID")]
    public string? LineItemID { get; set; }

    [JsonPropertyName("InventoryItemAccountID")]
    public string? InventoryItemAccountID { get; set; }

    [JsonPropertyName("Quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("BatchNo")]
    public string? BatchNo { get; set; }

    [JsonPropertyName("ExpiryDate")]
    public DateTime ExpiryDate { get; set; }

    [JsonPropertyName("RefAccountID")]
    public string? RefAccountID { get; set; }

    [JsonPropertyName("MatrixCode")]
    public string? MatrixCode { get; set; }

    [JsonPropertyName("BranchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("GroupID")]
    public string? GroupID { get; set; }

    [JsonPropertyName("Remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("TotalCost")]
    public decimal TotalCost { get; set; }
}
