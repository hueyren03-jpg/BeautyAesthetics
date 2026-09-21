using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class FileUploadResponseDTO
{
    [JsonPropertyName("fileUri")]
    public string? FileUri { get; set; }
}