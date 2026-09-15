using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class SMSSettings
    {
        private string ActiveTab { get; set; } = "SMS Content";

        private List<string> Tabs = new List<string> { "SMS Content", "Whatsapp Template" };

        // Dropdown toggle states
        private bool packageRedemptionOpen = false;
        private bool packageExtensionOpen = false;
        private bool creditTopUpOpen = false;
        private bool creditRedemptionOpen = false;
        private bool pointRedemptionOpen = false;
        private bool timeRedemptionOpen = false;
        private bool requestOtpOpen = false;
        private bool passwordResendOpen = false;
        private bool itemReadyOpen = false;

        // Selected values
        private string PackageRedemptionSelected { get; set; } = "";
        private string PackageExtensionSelected { get; set; } = "";
        private string CreditTopUpSelected { get; set; } = "";
        private string CreditRedemptionSelected { get; set; } = "";
        private string PointRedemptionSelected { get; set; } = "";
        private string TimeRedemptionSelected { get; set; } = "";
        private string RequestOtpSelected { get; set; } = "";
        private string PasswordResendSelected { get; set; } = "";
        private string ItemReadySelected { get; set; } = "";

        // Template class
        private class TemplateClass
        {
            public string name { get; set; } = "";
            public string category { get; set; } = "";
            public string status { get; set; } = "";
            public string content { get; set; } = "";
        }

        // Dropdown data for each section
        private List<TemplateClass> PackageRedemptionOptions = new()
    {
        new TemplateClass { name = "Package Redeem 1", category = "Marketing", status = "Active", content = "Your package has been redeemed" },
        new TemplateClass { name = "Package Redeem 2", category = "Transactional", status = "Active", content = "Redemption successful" }
    };

        private List<TemplateClass> PackageExtensionOptions = new()
    {
        new TemplateClass { name = "Extension Alert 1", category = "Notification", status = "Active", content = "Your package has been extended" },
        new TemplateClass { name = "Extension Alert 2", category = "Marketing", status = "Inactive", content = "Extension confirmed" }
    };

        private List<TemplateClass> CreditTopUpOptions = new()
    {
        new TemplateClass { name = "TopUp Success", category = "Transactional", status = "Active", content = "Credit top-up successful" },
        new TemplateClass { name = "TopUp Confirmation", category = "Notification", status = "Active", content = "Your credit has been topped up" }
    };

        private List<TemplateClass> CreditRedemptionOptions = new()
    {
        new TemplateClass { name = "Credit Redeem 1", category = "Transactional", status = "Active", content = "Credit redeemed successfully" },
        new TemplateClass { name = "Credit Redeem 2", category = "Marketing", status = "Active", content = "Your credits have been used" }
    };

        private List<TemplateClass> PointRedemptionOptions = new()
    {
        new TemplateClass { name = "Points Redeem", category = "Loyalty", status = "Active", content = "Points redeemed successfully" },
        new TemplateClass { name = "Points Alert", category = "Notification", status = "Active", content = "You've used your points" }
    };

        private List<TemplateClass> TimeRedemptionOptions = new()
    {
        new TemplateClass { name = "Time Used", category = "Transactional", status = "Active", content = "Time has been redeemed" },
        new TemplateClass { name = "Time Alert", category = "Notification", status = "Active", content = "Your time package is active" }
    };

        private List<TemplateClass> RequestOtpOptions = new()
    {
        new TemplateClass { name = "OTP Request", category = "Security", status = "Active", content = "Your OTP code is {code}" },
        new TemplateClass { name = "OTP Verification", category = "Security", status = "Active", content = "Verification code: {code}" }
    };

        private List<TemplateClass> PasswordResendOptions = new()
    {
        new TemplateClass { name = "Password Reset", category = "Security", status = "Active", content = "Your new password is {password}" },
        new TemplateClass { name = "Password Recovery", category = "Security", status = "Active", content = "Password reset link sent" }
    };

        private List<TemplateClass> ItemReadyOptions = new()
    {
        new TemplateClass { name = "Collection Notice", category = "Notification", status = "Active", content = "Your item is ready for collection" },
        new TemplateClass { name = "Pickup Ready", category = "Notification", status = "Active", content = "Item ready for pickup" }
    };

        // Individual toggle methods for each dropdown
        private void TogglePackageRedemption()
        {
            packageRedemptionOpen = !packageRedemptionOpen;
        }

        private void TogglePackageExtension()
        {
            packageExtensionOpen = !packageExtensionOpen;
        }

        private void ToggleCreditTopUp()
        {
            creditTopUpOpen = !creditTopUpOpen;
        }

        private void ToggleCreditRedemption()
        {
            creditRedemptionOpen = !creditRedemptionOpen;
        }

        private void TogglePointRedemption()
        {
            pointRedemptionOpen = !pointRedemptionOpen;
        }

        private void ToggleTimeRedemption()
        {
            timeRedemptionOpen = !timeRedemptionOpen;
        }

        private void ToggleRequestOtp()
        {
            requestOtpOpen = !requestOtpOpen;
        }

        private void TogglePasswordResend()
        {
            passwordResendOpen = !passwordResendOpen;
        }

        private void ToggleItemReady()
        {
            itemReadyOpen = !itemReadyOpen;
        }

        // Individual select methods for each dropdown
        private void SelectPackageRedemption(TemplateClass item)
        {
            PackageRedemptionSelected = item.name;
            packageRedemptionOpen = false;
        }

        private void SelectPackageExtension(TemplateClass item)
        {
            PackageExtensionSelected = item.name;
            packageExtensionOpen = false;
        }

        private void SelectCreditTopUp(TemplateClass item)
        {
            CreditTopUpSelected = item.name;
            creditTopUpOpen = false;
        }

        private void SelectCreditRedemption(TemplateClass item)
        {
            CreditRedemptionSelected = item.name;
            creditRedemptionOpen = false;
        }

        private void SelectPointRedemption(TemplateClass item)
        {
            PointRedemptionSelected = item.name;
            pointRedemptionOpen = false;
        }

        private void SelectTimeRedemption(TemplateClass item)
        {
            TimeRedemptionSelected = item.name;
            timeRedemptionOpen = false;
        }

        private void SelectRequestOtp(TemplateClass item)
        {
            RequestOtpSelected = item.name;
            requestOtpOpen = false;
        }

        private void SelectPasswordResend(TemplateClass item)
        {
            PasswordResendSelected = item.name;
            passwordResendOpen = false;
        }

        private void SelectItemReady(TemplateClass item)
        {
            ItemReadySelected = item.name;
            itemReadyOpen = false;
        }
        [Inject]
        private NavigationManager navigationManager { get; set; } = default!;
        private void Back()
        {
            navigationManager.NavigateTo("/config");
        }
    }
}
