using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class SupportingTableDM
{
    public bool IsLoading { get; set; }
    public string? SupportingTableID { get; set; }
    public string? SupportingTableName { get; set; }
    public int SupportingTableTypeID { get; set; }
    public bool Active { get; set; } = true;
    public string? BranchID { get; set; }
    public decimal NumberField { get; set; }
    public string? TextField { get; set; }
    public int IntegerField { get; set; }
    public string? ExtAccSalesControlAccount { get; set; }
    public string? ExtAccSalesReturnControlAccount { get; set; }
    public string? ExtAccPurchaseControlAccount { get; set; }
    public string? ExtAccPurchaseReturnControlAccount { get; set; }
    public string? ParentID { get; set; }
    public string? ImagePath { get; set; }
    public string? Createdby { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public string? Modifiedby { get; set; }
    public DateTime ModifiedDateTime { get; set; }
    public int SaveAction { get; set; } = 1;
    public bool IsDirty { get; set; }

    [JsonPropertyName("intSelectedInventoryTypeID")]
    public int IntSelectedInventoryTypeID { get; set; }

    [JsonPropertyName("strSelectedBrandID")]
    public string StrSelectedBrandID { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    [JsonPropertyName("lstExchangeRate")]
    public List<SupportingTableExchangeRateDM> LstExchangeRate { get; set; } = new();
}

public sealed class SupportingTableExchangeRateDM
{
    public bool IsLoading { get; set; }
    public string? ExchangeRateID { get; set; }
    public string? CurrencyID { get; set; }
    public DateTime FromDate { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? GroupID { get; set; }
    public int SaveAction { get; set; } = 1;
    public bool IsDirty { get; set; }
    public DateTime ToDate { get; set; }
}

public sealed class SupportingTableSaveResultDTO
{
    public string? Id { get; set; }
    public string? DisplayCode { get; set; }
    public string? SuccessMessage { get; set; }
}
