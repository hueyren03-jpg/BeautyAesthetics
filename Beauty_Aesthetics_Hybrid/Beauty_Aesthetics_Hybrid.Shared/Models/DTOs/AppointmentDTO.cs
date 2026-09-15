using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public class AppointmentDateRangeRequestDTO
{
    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }
}

public sealed class AppointmentCustomerRequestDTO : AppointmentDateRangeRequestDTO
{
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;
}

public sealed class AppointmentEmployeeRequestDTO : AppointmentDateRangeRequestDTO
{
    [JsonPropertyName("strID")]
    public string EmployeeId { get; set; } = string.Empty;
}

public sealed class AppointmentEmployeeStatusRequestDTO : AppointmentDateRangeRequestDTO
{
    [JsonPropertyName("employeeID")]
    public string EmployeeId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public sealed record AppointmentStatusOption(int Value, string Name);
