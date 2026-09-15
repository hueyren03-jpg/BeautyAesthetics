namespace SenangRetails.Shared.Models
{
    public class AppointmentData
    {
        public string Id { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsAllDay { get; set; }
        public string RecurrenceRule { get; set; } = string.Empty;
        public string RecurrenceException { get; set; } = string.Empty;
        public Nullable<int> RecurrenceID { get; set; }
        public string StaffId { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        
        // Appointment Details
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int NumberOfPax { get; set; }
        public string Services { get; set; } = string.Empty;
        public StaffAttendance? AssignedStaff { get; set; } // Single staff member
        public bool IsBlockTimeSlot { get; set; }
        public string Reminder { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // e.g., "Completed", "NoShow", etc.
    }
}
