using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class CustomerLookupDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
