using System.Collections.ObjectModel;
using Beauty_Aesthetics_WebPos.Components.Models.Employee;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels
{
    public class EmployeePageViewModel
    {
        public ObservableCollection<Employee> Employees { get; private set; } = new();
        public string SearchTerm { get; set; } = "";
        public string SelectedStatus { get; set; } = "All Status";
        public string SelectedLevel { get; set; } = "All Levels";
        public bool IsLoading { get; private set; }
        public string? ErrorMessage { get; private set; }

        private readonly NavigationManager _navManager;
        private readonly IEmployeeService _employeeService;

        public EmployeePageViewModel(NavigationManager navManager, IEmployeeService employeeService)
        {
            _navManager = navManager;
            _employeeService = employeeService;
        }

        public async Task LoadEmployeesAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            var result = await _employeeService.GetAllEmployeesAsync();
            if (!result.Success || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to load employees.";
                Employees = new ObservableCollection<Employee>();
                IsLoading = false;
                return;
            }

            Employees = new ObservableCollection<Employee>(result.Value);
            IsLoading = false;
        }

        public IEnumerable<Employee> FilteredEmployees()
        {
            var query = Employees.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim();
                query = query.Where(employee =>
                    ContainsSearchText(employee.Code, keyword) ||
                    ContainsSearchText(employee.Name, keyword) ||
                    ContainsSearchText(employee.SalesPersonCode, keyword) ||
                    ContainsSearchText(employee.JobTitle, keyword) ||
                    ContainsSearchText(employee.BranchId, keyword));
            }

            if (!string.IsNullOrWhiteSpace(SelectedStatus) && SelectedStatus != "All Status")
            {
                query = query.Where(employee => string.Equals(employee.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SelectedLevel) && SelectedLevel != "All Levels")
            {
                query = query.Where(employee => string.Equals(employee.EmployeeLevel, SelectedLevel, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        public Task NavigateToAddEmployeeAsync()
        {
            _navManager.NavigateTo("/employeeform");
            return Task.CompletedTask;
        }

        public Task NavigateToEditEmployeeAsync(string employeeCode)
        {
            _navManager.NavigateTo($"/employeeform/{Uri.EscapeDataString(employeeCode)}");
            return Task.CompletedTask;
        }

        public async Task<EmployeeOperationResult<Employee>> DeleteEmployeeAsync(string employeeCode)
        {
            ErrorMessage = null;
            var result = await _employeeService.DeactivateEmployeeAsync(employeeCode);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to hide employee.";
                return result;
            }

            var employee = Employees.FirstOrDefault(item =>
                string.Equals(item.Code, employeeCode, StringComparison.OrdinalIgnoreCase));
            if (employee is not null)
            {
                employee.Status = "Inactive";
            }

            return result;
        }

        private static bool ContainsSearchText(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }
    }
}
