using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

public partial class EmployeeFormBase : ComponentBase
{
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public IEmployeeService EmployeeService { get; set; } = default!;

    [Inject]
    public IBranchLookupService BranchLookupService { get; set; } = default!;

    [Inject]
    public FileUploadAC FileUploadAC { get; set; } = default!;

    [Inject]
    public AppFeedbackService Feedback { get; set; } = default!;

    public EmployeeFormViewModel? ViewModel { get; set; }

    [Parameter]
    public string? EmployeeCode { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ViewModel = new EmployeeFormViewModel(NavigationManager, EmployeeService);
        ViewModel.InitializeBranchList();

        if (!string.IsNullOrEmpty(EmployeeCode))
        {
            await ViewModel.LoadEmployeeForEditAsync(EmployeeCode);
            ViewModel.IsEditMode = true;
            if (string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                Feedback.Info($"Editing employee {ViewModel.Employee.Name}.", "Edit employee", 2800);
            }
            else
            {
                Feedback.Error(ViewModel.ErrorMessage, "Employee could not be opened");
            }
        }
        else
        {
            ViewModel.InitializeNewEmployee();
            ViewModel.IsEditMode = false;
            Feedback.Info("Enter the employee details and save when ready.", "New employee", 2600);
        }
        var branches = await BranchLookupService.LoadBranchesAsync();
        if (branches.Success && branches.Value is not null)
        {
            ViewModel.SetBranchList(branches.Value.Select(branch => (branch.Id, branch.Name)));
        }
        else
        {
            ViewModel.ErrorMessage = branches.ErrorMessage ?? "Unable to load branches.";
            Feedback.Error(ViewModel.ErrorMessage, "Branches unavailable");
        }
    }

    protected async Task HandleEmployeeImageSelected(InputFileChangeEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        var upload = await FileUploadAC.UploadImageAsync(args.File, "Employees");
        if (!upload.Success || string.IsNullOrWhiteSpace(upload.Value))
        {
            ViewModel.ErrorMessage = upload.ErrorMessage ?? "Unable to upload employee image.";
            Feedback.Error(ViewModel.ErrorMessage, "Image upload failed");
            return;
        }

        ViewModel.Employee.ImagePath = upload.Value;
        ViewModel.ErrorMessage = null;
        Feedback.Success("Employee image uploaded and ready to save.", "Image uploaded", 3200);
        await InvokeAsync(StateHasChanged);
    }
    protected async Task SaveAsync()
    {
        if (ViewModel is null || ViewModel.IsSaving)
        {
            return;
        }

        var wasEditing = ViewModel.IsEditMode;
        var feedbackId = Feedback.Loading(
            wasEditing ? "Updating employee..." : "Creating employee...",
            wasEditing ? "Updating employee" : "Creating employee");

        await ViewModel.SaveAsync();
        await InvokeAsync(StateHasChanged);

        if (!string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
        {
            var message = ViewModel.ErrorMessage;
            if (message.Contains("required", StringComparison.OrdinalIgnoreCase))
            {
                Feedback.Dismiss(feedbackId);
                Feedback.Warning(message, "Check employee details");
            }
            else
            {
                Feedback.Fail(
                    feedbackId,
                    message,
                    wasEditing ? "Employee not updated" : "Employee not created");
            }

            return;
        }

        Feedback.Resolve(
            feedbackId,
            wasEditing ? "Employee updated successfully." : "Employee created successfully.",
            wasEditing ? "Employee updated" : "Employee created");
    }

}
