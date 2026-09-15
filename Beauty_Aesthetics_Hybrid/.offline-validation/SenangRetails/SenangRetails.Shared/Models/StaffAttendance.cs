using System.Globalization;

namespace SenangRetails.Shared.Models
{
    public class StaffAttendance
    {
        public String Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime? FirstClockIn { get; set; }
        public DateTime? LastClockOut { get; set; }
        public double HoursWorked { get; set; }
        public double MinsWorked { get; set; }
        public double OvertimeHours { get; set; }
        public double OvertimeMins { get; set; }
        public double DaysCount { get; set; }
        public double LateCount { get; set; }
        public bool IsLate { get; set; }
        public double HoursLate { get; set; }
        public string PhoneNo { get; set; } = string.Empty;   
        public string Gender { get; set; } = string.Empty; // "Male" or "Female"
        public string Status { get; set; } = string.Empty; // "Working", "Late", "Off Day", "On Leave", "Overtime"
        public bool HasOvertime { get; set; } = false;
    }
}
