namespace Beauty_Aesthetics_WebPos.Components.Models
{
    public class Lead
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Source { get; set; }
        public string? Outlet { get; set; }
        public string? Remarks { get; set; }
        public string? AttendedBy { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
