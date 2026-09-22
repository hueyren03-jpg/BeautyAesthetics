using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Components.Models.Passcode;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels
{
    public class PasscodePageViewModel
    {
        private readonly NavigationManager _nav;

        public string SearchTerm { get; set; } = "";
        public string SelectedStatus { get; set; } = "All Status";

        public List<PasscodeModel> Passcode { get; set; } = new();

        public PasscodePageViewModel(NavigationManager nav)
        {
            _nav = nav;
        }

        public async Task LoadPasscodesAsync()
        {
            Passcode = new List<PasscodeModel>()
            {
                new PasscodeModel("1903", "Create new customer", "HAN JUAN LIM", "10/10/2025 04:53 PM", "HAN JUAN LIM", "10/10/2025 04:53 PM", "Active"),
                new PasscodeModel("3565", "Delete customer", "-", "", "HAN JUAN LIM", "10/10/2025 04:53 PM", "Used"),
                new PasscodeModel("8341", "Create new customer", "HAN JUAN LIM", "10/10/2025 04:49 PM", "HAN JUAN LIM", "10/10/2025 04:49 PM", "Active"),
            };

            await Task.CompletedTask;
        }
    }
}
