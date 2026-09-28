using Beauty_Aesthetics_WebPos.Components.Models.Employee;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.ViewModels
{
    public class EmployeeFormViewModel
    {
        private NavigationManager? _navigationManager;
        private readonly IEmployeeService _employeeService;

        public EmployeeFormViewModel(NavigationManager navigationManager, IEmployeeService employeeService)
        {
            _navigationManager = navigationManager;
            _employeeService = employeeService;
        }

        public Employee Employee { get; set; } = new();
        public bool IsSaving { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsEditMode { get; set; } = false;

        public List<string> EmployeeLevels { get; set; } = new() { "Junior", "Senior", "Manager" };
        public List<string> Genders { get; set; } = new() { "Male", "Female", "Other" };
        public List<string> WorkingShifts { get; set; } = new() { "Morning", "Afternoon", "Night" };
        public List<string> CommissionSchemes { get; set; } = new() { "Standard", "High", "Custom" };
        public List<string> AutoAllocationGroups { get; set; } = new() { "None", "Group A", "Group B" };

        public bool IsExpanded { get; set; } = false;

        public class BranchModel
        {
            public string BranchId { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public bool Available { get; set; }
        }

        public List<BranchModel> BranchList { get; set; } = new();

        public void InitializeBranchList()
        {
            BranchList = new List<BranchModel>();
        }

        public void SetBranchList(IEnumerable<(string Id, string Name)> branches)
        {
            BranchList = branches
                .Where(branch => !string.IsNullOrWhiteSpace(branch.Id))
                .Select(branch => new BranchModel
                {
                    BranchId = branch.Id.Trim(),
                    BranchName = string.IsNullOrWhiteSpace(branch.Name) ? branch.Id.Trim() : branch.Name.Trim(),
                    Available = string.Equals(branch.Id.Trim(), Employee.BranchId?.Trim(), StringComparison.OrdinalIgnoreCase)
                })
                .GroupBy(branch => branch.BranchId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(branch => branch.BranchName)
                .ToList();

            var employeeBranchId = Employee.BranchId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(employeeBranchId) &&
                !BranchList.Any(branch => string.Equals(branch.BranchId, employeeBranchId, StringComparison.OrdinalIgnoreCase)))
            {
                BranchList.Insert(0, new BranchModel
                {
                    BranchId = employeeBranchId,
                    BranchName = employeeBranchId,
                    Available = true
                });
            }

            SyncBranchAvailabilityFromEmployee();
        }

        public void SelectBranch(string branchId)
        {
            Employee.BranchId = branchId?.Trim() ?? string.Empty;
            SyncBranchAvailabilityFromEmployee();
        }

        public void SyncBranchAvailabilityFromEmployee()
        {
            foreach (var branch in BranchList)
            {
                branch.Available = string.Equals(
                    branch.BranchId,
                    Employee.BranchId?.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public void SetNavigationManager(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }

        public Task InitializeAsync()
        {
            InitializeBranchList();
            InitializeNewEmployee();
            return Task.CompletedTask;
        }

        public void InitializeNewEmployee()
        {
            Employee = new Employee
            {
                Status = "Active",
                BranchId = "HQ"
            };
        }

        public async Task LoadEmployeeForEditAsync(string code)
        {
            ErrorMessage = null;
            var result = await _employeeService.LoadEmployeeAsync(code);
            if (!result.Success || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to load employee.";
                InitializeNewEmployee();
                return;
            }

            Employee = result.Value;
            EnsureCurrentEmployeeOptions();
        }

        private void EnsureCurrentEmployeeOptions()
        {
            EnsureOption(EmployeeLevels, Employee.EmployeeLevel);
            EnsureOption(Genders, Employee.Gender);
            EnsureOption(WorkingShifts, Employee.WorkingShift);
            EnsureOption(CommissionSchemes, Employee.CommissionScheme);
            EnsureOption(AutoAllocationGroups, Employee.AutoAllocationGroup);
        }

        private static void EnsureOption(List<string> options, string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                options.Any(option => string.Equals(option, value.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            options.Insert(0, value.Trim());
        }

        public async Task SaveAsync()
        {
            if (IsSaving)
            {
                return;
            }

            ErrorMessage = null;

            if (!ValidateEmployee())
            {
                return;
            }

            IsSaving = true;

            try
            {
                if (IsEditMode)
                {
                    await UpdateEmployeeAsync();
                }
                else
                {
                    await AddEmployeeAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to save employee. {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }

            if (string.IsNullOrWhiteSpace(ErrorMessage))
            {
                _navigationManager?.NavigateTo("/employee", true);
            }
        }

        private bool ValidateEmployee()
        {
            if (string.IsNullOrWhiteSpace(Employee.Name))
            {
                ErrorMessage = "Employee name is required.";
                ActiveTab = "General Info";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Employee.Status))
            {
                Employee.Status = "Active";
            }

            if (string.IsNullOrWhiteSpace(Employee.BranchId))
            {
                Employee.BranchId = "HQ";
            }

            return true;
        }

        private async Task AddEmployeeAsync()
        {
            var result = await _employeeService.CreateEmployeeAsync(Employee);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to save employee.";
            }
        }

        private async Task UpdateEmployeeAsync()
        {
            var result = await _employeeService.UpdateEmployeeAsync(Employee);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to update employee.";
            }
        }

        public string ActiveTab { get; set; } = "General Info";

        public void ChangeTab(string tabName)
        {
            ActiveTab = tabName;
        }

        public void Cancel()
        {
            _navigationManager?.NavigateTo("/employee");
        }

        public List<Employee> ServiceRecords { get; set; } = new();

        public void AddServiceRecord()
        {
            ServiceRecords.Add(new Employee
            {
                BranchId = "HQ",
                EffectiveDate = DateTime.Now,
                DailyBasic = 0,
                CommMethod = "Amount",
                CommAmount = 0,
                UpdatedBy = "Admin"
            });
        }

        public void RemoveServiceRecord()
        {
            if (ServiceRecords.Any())
                ServiceRecords.RemoveAt(ServiceRecords.Count - 1);
        }
    }
}
