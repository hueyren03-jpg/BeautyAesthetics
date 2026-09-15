using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;
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
        }
        else
        {
            ViewModel.InitializeNewEmployee();
            ViewModel.IsEditMode = false;
        }
        var branches = await BranchLookupService.LoadBranchesAsync();
        if (branches.Success && branches.Value is not null)
        {
            ViewModel.SetBranchList(branches.Value.Select(branch => (branch.Id, branch.Name)));
        }
        else
        {
            ViewModel.ErrorMessage = branches.ErrorMessage ?? "Unable to load branches.";
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
            return;
        }

        ViewModel.Employee.ImagePath = upload.Value;
        ViewModel.ErrorMessage = null;
        await InvokeAsync(StateHasChanged);
    }
    protected async Task SaveAsync()
    {
        if (ViewModel is not null)
        {
            var saveTask = ViewModel.SaveAsync();
            await InvokeAsync(StateHasChanged);
            await saveTask;
            await InvokeAsync(StateHasChanged);
        }
    }

}
