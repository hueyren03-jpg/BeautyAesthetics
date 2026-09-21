using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class StockGinLookupDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class StockGinProxyRequestDTO
{
    [JsonPropertyName("branchID")]
    public string BranchID { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 200;
}

public sealed class StockGinEnvelopeDTO
{
    [JsonPropertyName("mobjDoc_Stock_GIN")]
    public StockGrnDocumentDTO? Document { get; set; }

    [JsonPropertyName("lstDocumentLine")]
    public List<JsonObject> DocumentLines { get; set; } = new();

    public static StockGinEnvelopeDTO CreateNew() => new()
    {
        Document = new StockGrnDocumentDTO
        {
            DocumentID = Guid.NewGuid().ToString(),
            DocumentTypeID = 0,
            FriendlyDocumentName = "GIN",
            FinancialDate = DateTime.Today,
            PostingDate = DateTime.Today,
            ExchangeRate = 1
        }
    };
}
