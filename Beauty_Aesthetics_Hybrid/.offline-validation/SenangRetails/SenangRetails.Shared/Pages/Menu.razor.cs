using System;
using Microsoft.AspNetCore.Components;

namespace SenangRetails.Shared.Pages
{
    public partial class Menu : BasePage
    {
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        private string SelectedView { get; set; } = "Product";
        private bool IsSidebarOpen { get; set; } = false;
        private bool IsLeftPanelHidden { get; set; } = true; // ← Changed here

        private void SelectView(string viewName)
        {
            SelectedView = viewName;

            if (IsSidebarOpen)
                IsSidebarOpen = false;

            if (!IsLeftPanelHidden)
            {
                IsLeftPanelHidden = true;
            }
        }

        private void HideLeftPanel()
        {
            IsLeftPanelHidden = true;
            IsSidebarOpen = false;
        }

        private void ToggleSidebar()
        {
            if (IsLeftPanelHidden)
            {
                IsLeftPanelHidden = false;
                IsSidebarOpen = true;
            }
            else
                IsSidebarOpen = !IsSidebarOpen;
        }

        private void GoToProfile()
        {
            Navigation.NavigateTo("/home");
        }
    }
}