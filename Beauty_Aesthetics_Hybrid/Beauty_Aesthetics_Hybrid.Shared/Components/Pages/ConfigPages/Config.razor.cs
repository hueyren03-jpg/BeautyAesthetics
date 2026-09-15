using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class Config
    {
        private readonly NavigationManager navigationManager;

        public Config(NavigationManager navigationManager)
        {
            this.navigationManager = navigationManager;
        }

        private class SettingDetail
        {
            public string Name        { get; set; } = "";
            public string Description { get; set; } = "";
            public string IconClass   { get; set; } = "fa-solid fa-gear";
        }

        private readonly List<SettingDetail> settingOptions = new()
        {
            new SettingDetail { Name = "General Setup",       IconClass = "fa-solid fa-sliders" },
            new SettingDetail { Name = "Account Settings",    IconClass = "fa-solid fa-gears" },
            new SettingDetail { Name = "Configuration",       IconClass = "fa-solid fa-toggle-on" },
            new SettingDetail { Name = "Commission",          IconClass = "fa-solid fa-percent" },
            new SettingDetail { Name = "Table/Room Layout",   IconClass = "fa-solid fa-table-cells-large" },
            new SettingDetail { Name = "Email Settings",      IconClass = "fa-solid fa-envelope" },
            new SettingDetail { Name = "SMS Settings",        IconClass = "fa-solid fa-comment-sms" },
            new SettingDetail { Name = "Account Integration", IconClass = "fa-solid fa-link" },
            new SettingDetail { Name = "Mall Submission",     IconClass = "fa-solid fa-store" },
            new SettingDetail { Name = "EBI Online/eInvoice", IconClass = "fa-solid fa-file-invoice" },
            new SettingDetail { Name = "Central Control",     IconClass = "fa-solid fa-building-columns" },
        };


        //Im gonna be honest, i have no idea if i should use switch case or individual method to navigate to each page
        //performance probably negligible will use switch for easier management
        private void NavigateToSettings(string location)
        {
            switch (location)
            {
                case "General Setup":
                    navigationManager.NavigateTo("GeneralSetup");
                    break;

                case "Account Settings":
                    navigationManager.NavigateTo("AccountSettings");
                    break;

                case "Configuration":
                    navigationManager.NavigateTo("Configuration"); // Or "MembershipSettings"
                    break;

                case "Commission":
                    navigationManager.NavigateTo("Commission");
                    break;

                case "Table/Room Layout":
                    // Removing the slash for a valid URL/Page name
                    navigationManager.NavigateTo("RoomLayoutSettings");
                    break;

                case "Email Settings":
                    navigationManager.NavigateTo("EmailSettings");
                    break;

                case "SMS Settings":
                    navigationManager.NavigateTo("SmsSettings");
                    break;

                case "Account Integration":
                    navigationManager.NavigateTo("ACCIntegration");
                    break;

                case "Mall Submission":
                    navigationManager.NavigateTo("MallSubmission");
                    break;

                case "Counter Visible Items":
                    navigationManager.NavigateTo("CounterVisibleItems");
                    break;

                case "EBI Online/eInvoice":
                    navigationManager.NavigateTo("EInvoice");
                    break;

                // I renamed this based on your previous image description
                case "Central Control":
                    navigationManager.NavigateTo("CentralControl");
                    break;

                default:
                    // Good practice: Handle unknown routes or log an error
                    Console.WriteLine($"Route not found for: {location}");
                    break;
            }
        }

        private string GetSettingNameKey(string name)
        {
            return name switch
            {
                "General Setup" => "GeneralSetup",
                "Account Settings" => "AccountSettings",
                "Configuration" => "Configurations",
                "Commission" => "Commission",
                "Table/Room Layout" => "RoomLayoutSettings",
                "Email Settings" => "EmailSettings",
                "SMS Settings" => "SMSSettings",
                "Account Integration" => "ACCIntegration",
                "Mall Submission" => "MallSubmission",
                "EBI Online/eInvoice" => "EInvoice",
                "Central Control" => "CentralControl",
                _ => name
            };
        }

        private string GetSettingDescriptionKey(string name)
        {
            return name switch
            {
                "General Setup" => "GeneralSetupDesc",
                "Account Settings" => "AccountSettingsDesc",
                "Configuration" => "ConfigurationDesc",
                "Commission" => "CommissionDesc",
                "Table/Room Layout" => "RoomLayoutSettingsDesc",
                "Email Settings" => "EmailSettingsDesc",
                "SMS Settings" => "SmsSettingsDesc",
                "Account Integration" => "AccIntegrationDesc",
                "Mall Submission" => "MallSubmissionDesc",
                "EBI Online/eInvoice" => "EBIOnlineEInvoiceDesc",
                "Central Control" => "CentralControlDesc",
                _ => ""
            };
        }
    }
}
