namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class CustomerFollowUpDTO
{
    public bool IsLoading { get; set; }
    public string CustomerVisitNoteID { get; set; } = string.Empty;
    public DateTime FinancialDate { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDateTime { get; set; } = DateTime.Now;
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime ModifiedDateTime { get; set; } = DateTime.Now;
    public string RtfMessage { get; set; } = string.Empty;
    public string CustomerID { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public string Symptoms { get; set; } = string.Empty;
    public string Diagnoses { get; set; } = string.Empty;
    public string SaveAction { get; set; } = "Added";
    public bool IsDirty { get; set; } = true;
}

public sealed class CustomerFollowUpBranchRequestDTO
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BranchID { get; set; } = string.Empty;
}
