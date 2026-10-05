namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class CustomerFollowUpDTO
{
    public bool IsLoading { get; set; }
    public string? CustomerVisitNoteID { get; set; }
    public DateTime FinancialDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime ModifiedDateTime { get; set; }
    public string RtfMessage { get; set; } = string.Empty;
    public string CustomerID { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public string? Diagnoses { get; set; }
    public int SaveAction { get; set; } = 1;
    public bool IsDirty { get; set; } = true;
}

public sealed class CustomerFollowUpBranchRequestDTO
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BranchID { get; set; } = string.Empty;
}
