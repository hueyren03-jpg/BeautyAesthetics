using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class GstTaxCodeDto
    {
        [JsonPropertyName("TaxCodeID")] public string? TaxCodeID { get; set; }
        [JsonPropertyName("TaxDescription")] public string? TaxDescription { get; set; }
        [JsonPropertyName("TaxRate")] public decimal TaxRate { get; set; }
        [JsonPropertyName("TaxTypeID")] public string? TaxTypeID { get; set; }
        [JsonPropertyName("Active")] public bool Active { get; set; }
    }
}
