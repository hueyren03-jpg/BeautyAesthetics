using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class CentralControl
    {
        private string ActiveTab { get; set; } = "Other Settings";
        [Inject]
        private NavigationManager navigationManager { get; set; } = default!;
        private void Back()
        {
            navigationManager.NavigateTo("/config");
        }
    }

}
