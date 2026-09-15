using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.BranchService;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SenangRetails.Shared.Pages
{
    public partial class SelectBranch
    {
        [Inject] private IBranchService BranchService { get; set; } = default!;
        [Inject] private IStoreTokenService StoreTokenService { get; set; } = default!;

        private Dictionary<string, string> BranchNames { get; } = new();

        protected override async Task OnInitializedAsync()
        {
            var tasks = AppState.AvailableBranches.Select(async id =>
            {
                var (details, _) = await BranchService.GetBranchDetailsAsync(id);
                if (details != null && !string.IsNullOrWhiteSpace(details.Branch))
                    BranchNames[id] = details.Branch;
            });
            await Task.WhenAll(tasks);
        }

        private async Task ChooseBranch(string clickedBranchId)
        {
            try
            {
                var (details, errorMsg) = await BranchService.GetBranchDetailsAsync(clickedBranchId);

                if (details != null)
                {
                    AppState.SelectedBranchID = clickedBranchId;
                    AppState.CurrentBranch = details;
                    AppState.SelectedBranchGroupID = details.BranchGroupID ?? "";

                    await JS.InvokeVoidAsync("localStorage.setItem", "currentBranch", clickedBranchId);

                    var jsonDetails = JsonSerializer.Serialize(details);
                    await JS.InvokeVoidAsync("localStorage.setItem", "currentBranchDetails", jsonDetails);

                    Nav.NavigateTo("/home");
                }
                else
                {
                    await JS.InvokeVoidAsync("alert", $"Failed to load branch details: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during branch selection: {ex.Message}");
            }
        }

        private async Task Logout()
        {
            AppState.SelectedBranchID = string.Empty;
            AppState.CurrentBranch = null;
            AppState.SelectedBranchGroupID = string.Empty;
            AppState.AvailableBranches.Clear();
            await StoreTokenService.ClearAsync();
            await JS.InvokeVoidAsync("localStorage.removeItem", "currentBranch");
            await JS.InvokeVoidAsync("localStorage.removeItem", "currentBranchDetails");
            await JS.InvokeVoidAsync("localStorage.removeItem", "available_branches");
            Nav.NavigateTo("/");
        }
    }
}
