namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class FollowUpRecordDTO
{
    public string RecordId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public string Content { get; set; } = string.Empty;
}
