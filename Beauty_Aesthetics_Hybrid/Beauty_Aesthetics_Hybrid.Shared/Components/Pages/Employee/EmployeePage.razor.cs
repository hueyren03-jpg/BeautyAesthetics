using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Pages.Employee
{
    public partial class EmployeePage : ComponentBase
    {
        [Inject] private NavigationManager NavManager { get; set; } = default!;
        [Inject] private IEmployeeService EmployeeService { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private AppFeedbackService Feedback { get; set; } = default!;

        private EmployeePageViewModel VM { get; set; } = default!;

        private Beauty_Aesthetics_WebPos.Components.Models.Employee.Employee? SelectedEmployee { get; set; }
        private bool IsEmployeeDetailsLoading { get; set; }
        private string? EmployeeDetailsError { get; set; }
        private const int EmployeePageSize = 10;
        private int employeeCurrentPage = 1;
        private int FilteredEmployeeCount => VM.FilteredEmployees().Count();
        private int EmployeeTotalPages => Math.Max(1, (int)Math.Ceiling(FilteredEmployeeCount / (double)EmployeePageSize));
        private int EmployeeCurrentPage => Math.Min(employeeCurrentPage, EmployeeTotalPages);
        private IReadOnlyList<Beauty_Aesthetics_WebPos.Components.Models.Employee.Employee> PagedEmployees => VM.FilteredEmployees()
            .Skip((EmployeeCurrentPage - 1) * EmployeePageSize)
            .Take(EmployeePageSize)
            .ToList();

        private void ResetEmployeePage() => employeeCurrentPage = 1;

        private void SetEmployeeStatus(string status)
        {
            VM.SelectedStatus = status;
            ResetEmployeePage();
        }

        private void SetEmployeeLevel(string level)
        {
            VM.SelectedLevel = level;
            ResetEmployeePage();
        }

        private void PreviousEmployeePage()
        {
            if (employeeCurrentPage > 1) employeeCurrentPage--;
        }

        private void NextEmployeePage()
        {
            if (employeeCurrentPage < EmployeeTotalPages) employeeCurrentPage++;
        }

        private async Task ViewEmployeeDetailsAsync(Beauty_Aesthetics_WebPos.Components.Models.Employee.Employee employee)
        {
            SelectedEmployee = employee;
            EmployeeDetailsError = null;
            IsEmployeeDetailsLoading = true;

            var result = await EmployeeService.LoadEmployeeAsync(employee.Code);
            if (result.Success && result.Value is not null)
            {
                SelectedEmployee = result.Value;
            }
            else
            {
                EmployeeDetailsError = result.ErrorMessage ?? "Unable to load the complete employee record.";
                Feedback.Error(EmployeeDetailsError, "Employee details unavailable");
            }

            IsEmployeeDetailsLoading = false;
        }

        private void CloseEmployeeDetails()
        {
            SelectedEmployee = null;
            EmployeeDetailsError = null;
            IsEmployeeDetailsLoading = false;
        }

        private static string DisplayEmployeeValue(string? value, string fallback = "-") =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string DisplayEmployeeDate(DateTime? value) =>
            value.HasValue ? value.Value.ToString("dd/MM/yyyy") : "-";

        private static bool IsEmployeeActive(string? status) =>
            string.Equals(status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase);

        protected override async Task OnInitializedAsync()
        {
            VM = new EmployeePageViewModel(NavManager, EmployeeService);
            await VM.LoadEmployeesAsync();
        }

        private async Task NavigateToAddEmployeeAsync()
        {
            Feedback.Info("Opening a new employee form.", "Add employee", 2200);
            await VM.NavigateToAddEmployeeAsync();
        }

        private async Task NavigateToEditEmployeeAsync(string code)
        {
            Feedback.Info($"Opening employee {code} for editing.", "Edit employee", 2200);
            await VM.NavigateToEditEmployeeAsync(code);
        }

        private async Task DeleteEmployeeAsync(string code)
        {
            var confirmed = await JS.InvokeAsync<bool>(
                "confirm",
                "Set this employee to inactive?");

            if (!confirmed)
            {
                Feedback.Info("Employee deactivation cancelled.", "No changes made", 1800);
                return;
            }

            var feedbackId = Feedback.Loading($"Deactivating employee {code}...", "Deactivating employee");
            var result = await VM.DeleteEmployeeAsync(code);

            if (!result.Success)
            {
                Feedback.Fail(
                    feedbackId,
                    result.ErrorMessage ?? VM.ErrorMessage ?? "Unable to deactivate employee.",
                    "Employee not deactivated");
                return;
            }

            if (SelectedEmployee?.Code == code)
            {
                SelectedEmployee = null;
            }

            Feedback.Resolve(
                feedbackId,
                $"Employee {code} was deactivated successfully.",
                "Employee deactivated");
        }
    }
}

