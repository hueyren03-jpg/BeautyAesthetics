namespace Beauty_Aesthetics_WebPos.Components.ViewModels
{
    public class DashboardSnapshot
    {
        public int TotalCustomers { get; set; }
        public int ActiveLeads { get; set; }
        public int AppointmentsToday { get; set; }
        public decimal Revenue { get; set; }
        public List<int> Series { get; set; } = new();
        public List<int> Series2 { get; set; } = new();
    }
}
