using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class StockTransferLookupDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class StockTransferProxyRequestDTO
{
    // StockTransfer/LoadProxy uses branchId in the SenangRetails contract.
    [JsonPropertyName("branchId")]
    public string BranchID { get; set; } = "HQ";

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 200;
}

public sealed class StockTransferEnvelopeDTO
{
    [JsonPropertyName("objDoc_StockTransfer")]
    public StockTransferDocumentDTO Document { get; set; } = new();

    // Some deployments return the same envelope with the older mobj prefix.
    [JsonPropertyName("mobjDoc_StockTransfer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StockTransferDocumentDTO? LegacyDocument
    {
        get => null;
        set
        {
            if (value is not null)
            {
                Document = value;
            }
        }
    }
    [JsonPropertyName("lstDocumentLine")]
    public List<JsonObject> DocumentLines { get; set; } = new();
}

public sealed class StockTransferDocumentDTO
{
    [JsonPropertyName("isLoading")]
    public bool IsLoading { get; set; }

    [JsonPropertyName("documentID")]
    public string? DocumentID { get; set; }

    [JsonPropertyName("documentTypeID")]
    public int DocumentTypeID { get; set; }

    [JsonPropertyName("friendlyDocumentName")]
    public string? FriendlyDocumentName { get; set; }

    [JsonPropertyName("alphaCode")]
    public string? AlphaCode { get; set; }

    [JsonPropertyName("numericCode")]
    public int NumericCode { get; set; }

    [JsonPropertyName("branchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("editBranchID")]
    public string? EditBranchID { get; set; }

    [JsonPropertyName("displayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("financialDate")]
    public DateTime FinancialDate { get; set; } = DateTime.Today;

    [JsonPropertyName("referenceNumber")]
    public string? ReferenceNumber { get; set; }

    [JsonPropertyName("totalBeforeTax")]
    public decimal TotalBeforeTax { get; set; }

    [JsonPropertyName("taxableAmount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("roundingAmount")]
    public decimal RoundingAmount { get; set; }

    [JsonPropertyName("totalAfterTax")]
    public decimal TotalAfterTax { get; set; }

    [JsonPropertyName("localTotalBeforeTax")]
    public decimal LocalTotalBeforeTax { get; set; }

    [JsonPropertyName("localTaxableAmount")]
    public decimal LocalTaxableAmount { get; set; }

    [JsonPropertyName("localTaxAmount")]
    public decimal LocalTaxAmount { get; set; }

    [JsonPropertyName("localRoundingAmount")]
    public decimal LocalRoundingAmount { get; set; }

    [JsonPropertyName("localTotalAfterTax")]
    public decimal LocalTotalAfterTax { get; set; }

    [JsonPropertyName("exchangeRate")]
    public decimal ExchangeRate { get; set; } = 1;

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("isVoid")]
    public bool IsVoid { get; set; }

    [JsonPropertyName("orderBranchID")]
    public string? OrderBranchID { get; set; }

    [JsonPropertyName("fromBranchID")]
    public string? FromBranchID { get; set; }

    [JsonPropertyName("toBranchID")]
    public string? ToBranchID { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("fromBranch")]
    public string? FromBranch { get; set; }

    [JsonPropertyName("toBranch")]
    public string? ToBranch { get; set; }

    [JsonPropertyName("isConsignment")]
    public bool IsConsignment { get; set; }

    [JsonPropertyName("stockTransferBranchGroupID")]
    public string? StockTransferBranchGroupID { get; set; }

    [JsonPropertyName("saveAction")]
    public object? SaveAction { get; set; }

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
