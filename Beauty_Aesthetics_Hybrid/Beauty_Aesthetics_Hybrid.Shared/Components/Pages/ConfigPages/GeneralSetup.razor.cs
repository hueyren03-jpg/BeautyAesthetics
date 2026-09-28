using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class GeneralSetup
    {
        [Inject]
        private NavigationManager navigationManager { get; set; } = default!;
        [Inject]
        private IBluetoothPrinterService PrinterSelectionService { get; set; } = default!;

        // State for the active tab
        private string ActiveTab { get; set; } = "Printer";

        private IReadOnlyList<PrinterOption> PrinterOptions { get; set; } = Array.Empty<PrinterOption>();
        private string SelectedPrinterKey { get; set; } = string.Empty;
        private string NetworkPrinterIp { get; set; } = string.Empty;
        private PrinterOption? SelectedPrinter =>
            PrinterOptions.FirstOrDefault(printer =>
                string.Equals(printer.Key, SelectedPrinterKey, StringComparison.OrdinalIgnoreCase));

        protected override void OnInitialized()
        {
            ReloadPrinterOptions();
        }

        private void ReloadPrinterOptions()
        {
            PrinterOptions = PrinterSelectionService.GetPrinterOptions();
            var selected = PrinterSelectionService.GetSelectedPrinter();
            SelectedPrinterKey = selected?.Key ?? string.Empty;
            NetworkPrinterIp = selected?.IpAddress ?? string.Empty;
        }

        private void OnPrinterChanged(ChangeEventArgs args)
        {
            var key = args.Value?.ToString() ?? string.Empty;
            var selected = PrinterOptions.FirstOrDefault(printer =>
                string.Equals(printer.Key, key, StringComparison.OrdinalIgnoreCase));

            if (selected is null)
            {
                SelectedPrinterKey = string.Empty;
                NetworkPrinterIp = string.Empty;
                return;
            }

            PrinterSelectionService.SelectPrinter(selected);
            ReloadPrinterOptions();
        }

        private void SaveNetworkPrinterIp()
        {
            var selected = SelectedPrinter;
            if (selected is null || !selected.IsWifiPrinter)
            {
                return;
            }

            PrinterSelectionService.UpdateNetworkPrinterIp(selected, NetworkPrinterIp);
            ReloadPrinterOptions();
        }

        //The state of the dropdown bar in Rfid reader
        private bool RFIDIsOpen { get; set; } = false;

        public List<string> Items { get; set; } = new() { "MSR Reader", "test" };
        private string SelectedRFID { get; set; } = "";

        private bool RFIDOpenUpwards { get; set; } = false;

        // List of tabs based on your image
        private List<string> Tabs = new List<string>
    {
        "Printer", "Barcode", "Cash Drawer", "Card Reader",
        "Pole Display", "Weighing Scale", "Door Access"
    };

        private void SelectRFIDItem(string item)
        {
            SelectedRFID = item;
        }

        private void ToggleRFID()
        {
            if (!RFIDIsOpen)
            {
                
                RFIDIsOpen = true;
            }
            else
            {
                RFIDIsOpen = false;
            }
        }
        // --- State Variables ---
        private string SelectedTopUpAmount { get; set; } = "";
        private bool IsTopUpDropdownOpen { get; set; } = false;
        private bool IsTopUpDropdownUpwards { get; set; } = false; // Set to true if near bottom of screen

        // --- Data Source ---
        // You can load this from a database or config
        private List<string> TopUpOptions = new List<string>
    {
        "10.00",
        "20.00",
        "50.00",
        "100.00",
        "200.00"
    };

        // --- Methods ---

        private void ToggleTopUpDropdown()
        {
            IsTopUpDropdownOpen = !IsTopUpDropdownOpen;

            // Optional: Logic to determine if it should open upwards
            // Example: if (ElementLocationY > ScreenHeight - 200) IsTopUpDropdownUpwards = true;
        }

        private void SelectTopUpItem(string amount)
        {
            SelectedTopUpAmount = amount;
            IsTopUpDropdownOpen = false; // Close after selection
        }

        // --- Model Definition ---
        public class BranchModel
        {
            public int Id { get; set; }
            public string BranchName { get; set; }
            public bool IsHQ { get; set; }
        }

        // --- State Variables ---
        private string SelectedBranchName { get; set; } = "";
        private int SelectedBranchId { get; set; } // Store this for DB operations

        private bool IsBranchDropdownOpen { get; set; } = false;
        private bool BranchOpenUpwards { get; set; } = false;

        // --- Data Source ---
        private List<BranchModel> Branches = new List<BranchModel>
    {
        new BranchModel { Id = 1, BranchName = "Kuala Lumpur HQ", IsHQ = true },
        new BranchModel { Id = 2, BranchName = "Penang Geo Avenue", IsHQ = false },
        new BranchModel { Id = 3, BranchName = "Johor Bahru City", IsHQ = false },
        new BranchModel { Id = 4, BranchName = "Mont Kiara Outlet", IsHQ = false }
    };

        // --- Methods ---

        private void ToggleBranchDropdown()
        {
            IsBranchDropdownOpen = !IsBranchDropdownOpen;
        }

        private void SelectBranch(BranchModel branch)
        {
            SelectedBranchName = branch.BranchName;
            SelectedBranchId = branch.Id; // Capture the ID
            IsBranchDropdownOpen = false;
        }

        private void Back()
        {
            navigationManager.NavigateTo("/config");
        }

        private string selectedCulture = GetSupportedCulture(System.Globalization.CultureInfo.CurrentUICulture.Name);

        private static string GetSupportedCulture(string name)
        {
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-Hans";
            if (name.StartsWith("ms", StringComparison.OrdinalIgnoreCase)) return "ms";
            return "en";
        }

        private void OnLanguageChanged(ChangeEventArgs e)
        {
            var nextCulture = e.Value?.ToString();
            if (!string.IsNullOrEmpty(nextCulture))
            {
                var redirectUrl = navigationManager.ToBaseRelativePath(navigationManager.Uri);
                var redirect = string.IsNullOrEmpty(redirectUrl) ? "/" : $"/{redirectUrl}";
                navigationManager.NavigateTo($"/culture/set?culture={nextCulture}&redirectUri={Uri.EscapeDataString(redirect)}", forceLoad: true);
            }
        }
    }
}
