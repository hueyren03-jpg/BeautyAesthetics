using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class StockBalanceRequestDTO
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("branchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("groupID")]
    public string? GroupID { get; set; }

    [JsonPropertyName("inventoryIDs")]
    public string? InventoryIDs { get; set; }

    [JsonPropertyName("financialDate")]
    public DateTime? FinancialDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("redemptionEndDate")]
    public DateTime? RedemptionEndDate { get; set; }

    [JsonPropertyName("minQuantityBalanceToShow")]
    public decimal MinQuantityBalanceToShow { get; set; }

    [JsonPropertyName("newExpiryDate")]
    public DateTime? NewExpiryDate { get; set; }
}

public sealed class StockBalanceItemDTO
{
    [JsonPropertyName("InventoryID")]
    public string InventoryID { get; set; } = string.Empty;

    [JsonPropertyName("BatchNo")]
    public string? BatchNo { get; set; }

    [JsonPropertyName("Matrix")]
    public string? Matrix { get; set; }

    [JsonPropertyName("ExistingQuantity")]
    public decimal ExistingQuantity { get; set; }

    [JsonPropertyName("OrderPendingDelivery")]
    public decimal OrderPendingDelivery { get; set; }

    [JsonPropertyName("StockBalanceAfterSalesOrder")]
    public decimal StockBalanceAfterSalesOrder { get; set; }

    [JsonPropertyName("UOMConvertedExistingQuantity")]
    public decimal UOMConvertedExistingQuantity { get; set; }

    [JsonPropertyName("UOMConvertedOrderPendingDelivery")]
    public decimal UOMConvertedOrderPendingDelivery { get; set; }

    [JsonPropertyName("UOMConvertedStockBalanceAfterSalesOrder")]
    public decimal UOMConvertedStockBalanceAfterSalesOrder { get; set; }
}
