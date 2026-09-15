using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class Commission
    {
        private bool CashierCommissionGroupIsOpen = false;
        private bool CashierCommissionOpenUpwards = false;
        private string CashierCommissionGroupSelect = string.Empty;
        [Inject]
        private NavigationManager navigationManager { get; set; } = default!;
        private void Back()
        {
            navigationManager.NavigateTo("/config");
        }
        private List<string> CommissionGroupList = new List<string>
    {
        "Commission Account 1",
        "Commission Account 2",
        "Commission Account 3",
        "Commission Account 4",
        "Commission Account 5",
    };

        private async Task ToggleCashierCommissionGroup()
        {
            if (!CashierCommissionGroupIsOpen)
            {
                CashierCommissionOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "CashierCommissionGroupSelect"
                );
                CashierCommissionGroupIsOpen = true;
            }
            else
            {
                CashierCommissionGroupIsOpen = false;
            }
        }

        private void SelectDebtorControlAccount(string item)
        {
            CashierCommissionGroupIsOpen = false;
        }
    }
}
