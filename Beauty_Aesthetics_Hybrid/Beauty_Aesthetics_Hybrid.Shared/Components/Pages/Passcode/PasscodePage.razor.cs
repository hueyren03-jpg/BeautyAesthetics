using Beauty_Aesthetics_WebPos.Components.Models.Passcode;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Microsoft.AspNetCore.Components;
using EmployeeModel = Beauty_Aesthetics_WebPos.Components.Models.Employee.Employee;

namespace Beauty_Aesthetics_WebPos.Components.Pages.Passcode;

public partial class PasscodePage
{
    private const int ItemsPerPage = 10;

    private PasscodePageViewModel? VM;
    private int CurrentPage { get; set; } = 1;
    private bool showAddPopup;
    private bool showEditPopup;
    private bool showViewPopup;
    private bool showDeletePopup;
    private PasscodeModel? selectedPasscode;
    private PasscodeModel editPasscode = new();
    private PasscodeModel newPasscode = CreateNewPasscode();
    private IReadOnlyList<EmployeeModel> activeEmployees = Array.Empty<EmployeeModel>();
    private bool isEmployeesLoading;
    private bool IsPageInitializing { get; set; } = true;
    private string? employeeLoadError;

    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject] private IEmployeeService EmployeeService { get; set; } = default!;
    [Inject] private AppFeedbackService Feedback { get; set; } = default!;

    public List<string> PermissionList { get; } =
    [
        "Create new customer",
        "Edit customer",
        "Delete customer",
        "Create new employee"
    ];

    private List<PasscodeModel> FilteredPasscodes
    {
        get
        {
            if (VM is null)
            {
                return [];
            }

            var search = VM.SearchTerm?.Trim() ?? string.Empty;
            var status = VM.SelectedStatus?.Trim() ?? "All Status";

            return VM.Passcode
                .Where(passcode => status == "All Status" ||
                    string.Equals(passcode.Status, status, StringComparison.OrdinalIgnoreCase))
                .Where(passcode => string.IsNullOrWhiteSpace(search) ||
                    Contains(passcode.Code, search) ||
                    Contains(passcode.Permission, search) ||
                    Contains(passcode.UsedBy, search) ||
                    Contains(passcode.CreatedBy, search))
                .ToList();
        }
    }

    private IEnumerable<PasscodeModel> PaginatedPasscodes => FilteredPasscodes
        .Skip((CurrentPage - 1) * ItemsPerPage)
        .Take(ItemsPerPage);

    private int TotalPasscodes => VM?.Passcode.Count ?? 0;
    private int ActivePasscodes => CountByStatus("Active");
    private int UsedPasscodes => CountByStatus("Used");
    private int InactivePasscodes => CountByStatus("Inactive");
    private bool HasActiveFilters => VM is not null &&
        (!string.IsNullOrWhiteSpace(VM.SearchTerm) || VM.SelectedStatus != "All Status");

    protected override async Task OnInitializedAsync()
    {
        IsPageInitializing = true;
        VM = new PasscodePageViewModel(NavManager);
        try
        {
            await Task.WhenAll(VM.LoadPasscodesAsync(), LoadEmployeesAsync());
        }
        finally
        {
            IsPageInitializing = false;
        }
    }

    private async Task LoadEmployeesAsync()
    {
        isEmployeesLoading = true;
        employeeLoadError = null;

        try
        {
            var result = await EmployeeService.GetAllEmployeesAsync();
            if (!result.Success || result.Value is null)
            {
                employeeLoadError = result.ErrorMessage ?? "Employees could not be loaded.";
                activeEmployees = Array.Empty<EmployeeModel>();
                Feedback.Error(employeeLoadError, "Employees unavailable");
                return;
            }

            activeEmployees = result.Value
                .Where(employee =>
                    !string.IsNullOrWhiteSpace(employee.Code) &&
                    string.Equals(employee.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .GroupBy(employee => employee.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(employee => employee.Name)
                .ThenBy(employee => employee.Code)
                .ToList();
        }
        finally
        {
            isEmployeesLoading = false;
        }
    }

    private void OnSearchChanged(ChangeEventArgs args)
    {
        VM!.SearchTerm = args.Value?.ToString() ?? string.Empty;
        CurrentPage = 1;
    }

    private void OnStatusChanged(ChangeEventArgs args)
    {
        VM!.SelectedStatus = args.Value?.ToString() ?? "All Status";
        CurrentPage = 1;
    }

    private void ClearFilters()
    {
        VM!.SearchTerm = string.Empty;
        VM.SelectedStatus = "All Status";
        CurrentPage = 1;
    }

    private void ChangePage(int page)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(FilteredPasscodes.Count / (double)ItemsPerPage));
        CurrentPage = Math.Clamp(page, 1, totalPages);
    }

    private void ShowAddPopup()
    {
        newPasscode = CreateNewPasscode();
        showAddPopup = true;
        Feedback.Info("Enter the passcode and choose one or more permissions.", "New passcode", 2200);
    }

    private void CloseAddPopup() => showAddPopup = false;

    private void SavePasscode()
    {
        if (VM is null || string.IsNullOrWhiteSpace(newPasscode.Code) ||
            string.IsNullOrWhiteSpace(newPasscode.Permission))
        {
            Feedback.Warning("Passcode and at least one permission are required.", "Passcode not saved");
            return;
        }

        newPasscode.Code = newPasscode.Code.Trim();
        ApplyEmployeeSelection(newPasscode);
        newPasscode.CreatedBy = "SYSTEM";
        newPasscode.CreatedDate = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
        newPasscode.Status = "Active";
        VM.Passcode.Insert(0, newPasscode);

        var savedCode = newPasscode.Code;
        newPasscode = CreateNewPasscode();
        CurrentPage = 1;
        showAddPopup = false;
        Feedback.Success($"Passcode {savedCode} created successfully.", "Passcode created");
    }

    private void ShowViewPopup(PasscodeModel passcode)
    {
        selectedPasscode = passcode;
        showViewPopup = true;
    }

    private void CloseViewPopup()
    {
        showViewPopup = false;
        selectedPasscode = null;
    }

    private void EditSelectedPasscode()
    {
        if (selectedPasscode is null)
        {
            return;
        }

        var passcode = selectedPasscode;
        showViewPopup = false;
        ShowEditPopup(passcode);
    }

    private void ShowEditPopup(PasscodeModel passcode)
    {
        selectedPasscode = passcode;
        editPasscode = Clone(passcode);
        if (string.IsNullOrWhiteSpace(editPasscode.UsedByEmployeeId))
        {
            editPasscode.UsedByEmployeeId = activeEmployees.FirstOrDefault(employee =>
                string.Equals(employee.Name, editPasscode.UsedBy, StringComparison.OrdinalIgnoreCase))?.Code;
        }
        showEditPopup = true;
        Feedback.Info($"Editing passcode {passcode.Code}.", "Edit passcode", 2200);
    }

    private void CloseEditPopup()
    {
        var updatedCode = selectedPasscode.Code;
        showEditPopup = false;
        selectedPasscode = null;
        Feedback.Success($"Passcode {updatedCode} updated successfully.", "Passcode updated");
    }

    private void UpdatePasscode()
    {
        if (VM is null || selectedPasscode is null || string.IsNullOrWhiteSpace(editPasscode.Permission))
        {
            Feedback.Warning("Select at least one permission before saving.", "Passcode not updated");
            return;
        }

        selectedPasscode.Permission = editPasscode.Permission;
        selectedPasscode.UsedByEmployeeId = editPasscode.UsedByEmployeeId;
        ApplyEmployeeSelection(selectedPasscode);
        selectedPasscode.Status = editPasscode.Status;
        selectedPasscode.UsedDate = editPasscode.UsedDate;

        showEditPopup = false;
        selectedPasscode = null;
    }

    private void ShowDeleteConfirmation(PasscodeModel passcode)
    {
        selectedPasscode = passcode;
        showDeletePopup = true;
    }

    private void CloseDeleteConfirmation()
    {
        showDeletePopup = false;
        selectedPasscode = null;
    }

    private void DeleteSelectedPasscode()
    {
        if (VM is null || selectedPasscode is null)
        {
            return;
        }

        var deletedCode = selectedPasscode.Code;
        VM.Passcode.Remove(selectedPasscode);
        showDeletePopup = false;
        selectedPasscode = null;
        Feedback.Success($"Passcode {deletedCode} deleted successfully.", "Passcode deleted");

        var totalPages = Math.Max(1, (int)Math.Ceiling(FilteredPasscodes.Count / (double)ItemsPerPage));
        CurrentPage = Math.Min(CurrentPage, totalPages);
    }

    private int CountByStatus(string status) => VM?.Passcode.Count(passcode =>
        string.Equals(passcode.Status, status, StringComparison.OrdinalIgnoreCase)) ?? 0;

    private static bool Contains(string? source, string search) =>
        source?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static string DisplayValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private void ApplyEmployeeSelection(PasscodeModel passcode)
    {
        var employee = activeEmployees.FirstOrDefault(item =>
            string.Equals(item.Code, passcode.UsedByEmployeeId, StringComparison.OrdinalIgnoreCase));

        passcode.UsedByEmployeeId = employee?.Code ?? string.Empty;
        passcode.UsedBy = employee?.Name ?? "-";
    }

    private static bool HasPermission(PasscodeModel passcode, string permission) =>
        GetGrantedPermissions(passcode).Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static void TogglePermission(PasscodeModel passcode, string permission)
    {
        var permissions = GetGrantedPermissions(passcode);
        var existing = permissions.FirstOrDefault(value =>
            string.Equals(value, permission, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            permissions.Add(permission);
        }
        else
        {
            permissions.Remove(existing);
        }

        passcode.Permission = string.Join(";", permissions);
    }

    private static List<string> GetGrantedPermissions(PasscodeModel passcode) =>
        (passcode.Permission ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string GetStatusClass(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "active" => "active",
        "used" => "used",
        "inactive" => "inactive",
        _ => "unknown"
    };

    private static PasscodeModel CreateNewPasscode() => new()
    {
        Code = string.Empty,
        Permission = string.Empty,
        UsedBy = "-",
        UsedDate = string.Empty,
        CreatedBy = "SYSTEM",
        CreatedDate = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt"),
        Status = "Active"
    };

    private static PasscodeModel Clone(PasscodeModel passcode) => new()
    {
        Code = passcode.Code,
        Permission = passcode.Permission,
        UsedBy = passcode.UsedBy,
        UsedByEmployeeId = passcode.UsedByEmployeeId,
        UsedDate = passcode.UsedDate,
        CreatedBy = passcode.CreatedBy,
        CreatedDate = passcode.CreatedDate,
        Status = passcode.Status
    };

    private static string GetPermissionTranslationKey(string permission) => permission switch
    {
        "Create new customer" => "CreateNewCustomer",
        "Edit customer" => "EditCustomer",
        "Delete customer" => "DeleteCustomer",
        "Create new employee" => "CreateNewEmployee",
        _ => permission
    };
}
