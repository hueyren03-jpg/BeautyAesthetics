using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class CustomerCreateResultDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("successMessage")]
    public string SuccessMessage { get; set; } = string.Empty;
}
