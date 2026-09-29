using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class MedicalCertificateRecordDTO
{
    public string RecordId { get; set; } = string.Empty;
    public string CertificateNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = "day-off";
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IncludeHalfDay { get; set; }
    public string DurationText { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorRegistrationNumber { get; set; } = string.Empty;
    public string SignatureDataUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "Issued";
}

public sealed class CustomerVisitNoteRecordDTO
{
    public string CustomerVisitNoteID { get; set; } = string.Empty;
    public DateTime FinancialDate { get; set; }
    public string RtfMessage { get; set; } = string.Empty;
    public string CustomerID { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDateTime { get; set; }
    public DateTime ModifiedDateTime { get; set; }
}

public sealed class CustomerVisitNoteWriteDTO
{
    public string CustomerVisitNoteID { get; set; } = string.Empty;
    public DateTime FinancialDate { get; set; }
    public string RtfMessage { get; set; } = string.Empty;
    public string CustomerID { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public string? Diagnoses { get; set; }
    public int SaveAction { get; set; }
    public bool IsDirty { get; set; } = true;
    public bool IsLoading { get; set; }
}
