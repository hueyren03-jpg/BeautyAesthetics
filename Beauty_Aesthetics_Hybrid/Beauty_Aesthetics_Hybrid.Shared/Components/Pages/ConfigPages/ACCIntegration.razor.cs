using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class ACCIntegration
    {
        private readonly NavigationManager navigationManager;
        public ACCIntegration(NavigationManager navigationManager)
        {
            this.navigationManager = navigationManager;
        }

        private string ActiveTab { get; set; } = "Default Control Account";

        private readonly List<string> Tabs =
        [
            "Default Control Account",
        "Sales Control Account",
        "Sales Return Control Account"
        ];
        private void Back()
        {
            navigationManager.NavigateTo("/config");
        }
    }
}
