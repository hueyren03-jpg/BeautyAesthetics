using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Beauty_Aesthetics_WebPos.Components.Services.Dashboard;
using Beauty_Aesthetics_WebPos.Components.Services.Branches;

namespace Beauty_Aesthetics_WebPos.Components.Pages
{
    public partial class Dashboard : ComponentBase
    {
        [Inject]
        private IJSRuntime JSRuntime { get; set; } = null!;

        [Inject]
        private IDashboardService DashboardService { get; set; } = null!;

        [Inject]
        private IBranchSessionService BranchSession { get; set; } = null!;

        [Inject]
        private NavigationManager NavigationManager { get; set; } = null!;

        protected string selectedBranchId = string.Empty;
        protected string selectedBranchName = "No branch selected";
        protected bool canSwitchBranch => BranchSession.AvailableBranches.Count > 1;
        protected string selectedPeriod = "day";
        protected DateTime selectedDate = DateTime.Now;
        private ElementReference dateInputRef;
        protected bool isDashboardLoading;
        protected string? dashboardError;

        protected decimal totalSales;
        protected int transactionCount;
        protected int appointmentsTotal;
        protected int appointmentsCompleted;
        protected int appointmentsRemaining;
        protected int newCustomers;
        protected int itemsSold;
        protected decimal productSales;

        protected List<Appointment> upcomingAppointments = [];

        protected List<ExpiringPackage> expiringPackages = [];

        protected List<BirthdayMember> birthdayMembers = [];

        protected List<LowStockItem> lowStockItems = [];

        protected List<AppointmentFollowUp> appointmentFollowUps = [];

        protected bool isLowStockExpanded = false;
        protected bool isAppointmentsExpanded = false;
        protected bool isBirthdaysExpanded = false;
        protected bool isExpiringExpanded = false;
        protected bool isFollowUpExpanded = false;

        protected void ToggleLowStock() => isLowStockExpanded = !isLowStockExpanded;
        protected void ToggleAppointments() => isAppointmentsExpanded = !isAppointmentsExpanded;
        protected void ToggleBirthdays() => isBirthdaysExpanded = !isBirthdaysExpanded;
        protected void ToggleExpiring() => isExpiringExpanded = !isExpiringExpanded;
        protected void ToggleFollowUp() => isFollowUpExpanded = !isFollowUpExpanded;

        protected override async Task OnInitializedAsync()
        {
            await BranchSession.InitializeAsync();
            selectedBranchId = BranchSession.CurrentBranch?.Id ?? string.Empty;
            selectedBranchName = BranchSession.CurrentBranch?.DisplayName ?? "No branch selected";

            if (string.IsNullOrWhiteSpace(selectedBranchId))
            {
                dashboardError = BranchSession.ErrorMessage ?? "Select a working branch to load the dashboard.";
                return;
            }

            await LoadDashboardAsync();
        }

        protected void OpenBranchPicker()
        {
            var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            var returnUrl = string.IsNullOrWhiteSpace(relativePath) ? "/" : $"/{relativePath}";
            NavigationManager.NavigateTo($"/select-branch?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        protected async Task SelectPeriod(string period)
        {
            selectedPeriod = period;
            await LoadDashboardAsync();
        }

        protected async Task OnDateSelected(ChangeEventArgs e)
        {
            if (DateTime.TryParse(e.Value?.ToString(), out DateTime date))
            {
                selectedDate = date;
                await LoadDashboardAsync();
            }
        }

        private async Task LoadDashboardAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedBranchId))
            {
                dashboardError = "Select a working branch to load the dashboard.";
                return;
            }

            isDashboardLoading = true;
            dashboardError = null;

            try
            {
                var (startDate, endDate) = GetDateRange();
                var snapshot = await DashboardService.LoadAsync(
                    startDate,
                    endDate,
                    selectedPeriod,
                    selectedBranchId);

                totalSales = snapshot.TotalSales;
                transactionCount = snapshot.TransactionCount;
                itemsSold = snapshot.ItemsSold;
                newCustomers = snapshot.NewCustomers;
                appointmentsTotal = snapshot.AppointmentsTotal;
                appointmentsCompleted = snapshot.AppointmentsCompleted;
                appointmentsRemaining = snapshot.AppointmentsRemaining;
                upcomingAppointments = snapshot.UpcomingAppointments
                    .Select(item => new Appointment
                    {
                        Time = item.Time,
                        CustomerName = item.CustomerName,
                        Service = item.Service
                    })
                    .ToList();
                birthdayMembers = snapshot.BirthdayMembers
                    .Select(item => new BirthdayMember
                    {
                        Name = item.Name,
                        Date = item.Date,
                        Phone = item.Phone
                    })
                    .ToList();
                expiringPackages = snapshot.ExpiringPackages
                    .Select(item => new ExpiringPackage
                    {
                        MemberName = item.MemberName,
                        PackageName = item.PackageName,
                        DaysLeft = item.DaysLeft
                    })
                    .ToList();
                lowStockItems = snapshot.LowStockItems
                    .Select(item => new LowStockItem
                    {
                        Name = item.Name,
                        CurrentStock = item.CurrentStock,
                        Unit = item.Unit
                    })
                    .ToList();
                appointmentFollowUps = snapshot.CustomerFollowUps
                    .Select(item => new AppointmentFollowUp
                    {
                        CustomerName = item.CustomerName,
                        Date = item.LastVisit,
                        Service = item.Service,
                        Status = "Pending"
                    })
                    .ToList();
                dashboardError = snapshot.ErrorMessage;
            }
            catch (Exception)
            {
                dashboardError = "Unable to load dashboard data.";
            }
            finally
            {
                isDashboardLoading = false;
            }
        }

        private (DateTime StartDate, DateTime EndDate) GetDateRange()
        {
            var date = selectedDate.Date;
            var startDate = selectedPeriod switch
            {
                "week" => date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
                "month" => new DateTime(date.Year, date.Month, 1),
                "year" => new DateTime(date.Year, 1, 1),
                _ => date
            };
            var endDate = selectedPeriod switch
            {
                "week" => startDate.AddDays(7).AddTicks(-1),
                "month" => startDate.AddMonths(1).AddTicks(-1),
                "year" => startDate.AddYears(1).AddTicks(-1),
                _ => startDate.AddDays(1).AddTicks(-1)
            };

            return (startDate, endDate);
        }

        protected async Task OpenDatePicker()
        {
            try
            {
                // Try to use showPicker() API if available, otherwise click the input
                await JSRuntime.InvokeVoidAsync("eval", @"
                    (function() {
                        const input = document.getElementById('dashboard-date-input');
                        if (input) {
                            if (typeof input.showPicker === 'function') {
                                input.showPicker();
                            } else {
                                input.focus();
                                input.click();
                            }
                        }
                    })();
                ");
            }
            catch
            {
                // Fallback: try to focus and click
                try
                {
                    await dateInputRef.FocusAsync();
                    await Task.Delay(50);
                    await JSRuntime.InvokeVoidAsync("eval", @"
                        (function() {
                            const input = document.getElementById('dashboard-date-input');
                            if (input) input.click();
                        })();
                    ");
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        protected string GetPerformanceTitle()
        {
            return selectedPeriod switch
            {
                "day" => "Today's Performance",
                "week" => "This Week's Performance",
                "month" => "This Month's Performance",
                "year" => "This Year's Performance",
                _ => "Today's Performance"
            };
        }

        protected string GetDateRangeLabel()
        {
            var (startDate, endDate) = GetDateRange();
            return selectedPeriod == "day"
                ? $"{startDate:dd/MM/yyyy} performance snapshot"
                : $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy} performance snapshot";
        }

        public class Appointment
        {
            public string Time { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string Service { get; set; } = "";
        }

        public class ExpiringPackage
        {
            public string MemberName { get; set; } = "";
            public string PackageName { get; set; } = "";
            public int DaysLeft { get; set; }
        }

        public class BirthdayMember
        {
            public string Name { get; set; } = "";
            public string Date { get; set; } = "";
            public string Phone { get; set; } = "";
        }

        public class LowStockItem
        {
            public string Name { get; set; } = "";
            public decimal CurrentStock { get; set; }
            public string Unit { get; set; } = "";
        }

        public class AppointmentFollowUp
        {
            public string CustomerName { get; set; } = "";
            public string Service { get; set; } = "";
            public string Date { get; set; } = "";
            public string Status { get; set; } = "";
        }
    }
}
