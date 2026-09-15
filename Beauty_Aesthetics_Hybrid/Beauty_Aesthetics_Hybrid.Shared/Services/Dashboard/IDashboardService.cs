namespace Beauty_Aesthetics_WebPos.Components.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardSnapshot> LoadAsync(
        DateTime startDate,
        DateTime endDate,
        string period,
        string branchId,
        CancellationToken cancellationToken = default);
}

public sealed class DashboardSnapshot
{
    public decimal TotalSales { get; init; }
    public int TransactionCount { get; init; }
    public int ItemsSold { get; init; }
    public int NewCustomers { get; init; }
    public int AppointmentsTotal { get; init; }
    public int AppointmentsCompleted { get; init; }
    public int AppointmentsRemaining { get; init; }
    public IReadOnlyList<DashboardLowStockItem> LowStockItems { get; init; } = [];
    public IReadOnlyList<DashboardCustomerFollowUp> CustomerFollowUps { get; init; } = [];
    public IReadOnlyList<DashboardAppointment> UpcomingAppointments { get; init; } = [];
    public IReadOnlyList<DashboardBirthdayMember> BirthdayMembers { get; init; } = [];
    public IReadOnlyList<DashboardExpiringPackage> ExpiringPackages { get; init; } = [];
    public string? ErrorMessage { get; init; }
}

public sealed record DashboardLowStockItem(string Name, decimal CurrentStock, string Unit);

public sealed record DashboardCustomerFollowUp(string CustomerName, string LastVisit, string Service);

public sealed record DashboardAppointment(string Time, string CustomerName, string Service);

public sealed record DashboardBirthdayMember(string Name, string Date, string Phone);

public sealed record DashboardExpiringPackage(string MemberName, string PackageName, int DaysLeft);
