using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class GstTaxCodeDTO
{
    [JsonPropertyName("TaxCodeID")] public string? TaxCodeID { get; set; }
    [JsonPropertyName("TaxDescription")] public string? TaxDescription { get; set; }
    [JsonPropertyName("TaxRate")] public decimal TaxRate { get; set; }
    [JsonPropertyName("TaxTypeID")] public string? TaxTypeID { get; set; }
    [JsonPropertyName("Active")] public bool Active { get; set; }
}
