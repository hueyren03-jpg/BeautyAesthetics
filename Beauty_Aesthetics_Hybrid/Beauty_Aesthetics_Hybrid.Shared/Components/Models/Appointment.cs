namespace Beauty_Aesthetics_WebPos.Components.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public string AppointmentId { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ServiceItem { get; set; } = string.Empty;
        public string ServiceInventoryId { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsAllDay { get; set; }
        public bool IsRecurring { get; set; }
        public string RecurrencePattern { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public int BusyStatus { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int Label { get; set; }
        public int PaymentStatus { get; set; }
        public int AppointmentType { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public string SalesPersonCode { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class ServiceItem
    {
        public string Name { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
    }
}
