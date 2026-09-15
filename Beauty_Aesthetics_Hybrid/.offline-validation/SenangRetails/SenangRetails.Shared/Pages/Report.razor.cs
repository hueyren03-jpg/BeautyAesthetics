using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models;

namespace SenangRetails.Shared.Pages
{
    public partial class Report
    {
        private bool isLeftPanelExpanded = false;
        private bool isLeftPanelHidden = true; // Changed to true for hidden by default
        private string selectedLeftTab = "Sales By Type";
        private bool isInitialized = false;

        private HashSet<string> expandedGroups = new HashSet<string> { "sales", "inventory", "staff", "operation", "available", "analytics", "member" };

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender && !isInitialized)
            {
                try
                {
                    // Check if device is mobile/tablet on initial load
                    var isMobile = await JSRuntime.InvokeAsync<bool>("checkIfMobile");

                    if (isMobile)
                    {
                        isLeftPanelHidden = true;
                        isLeftPanelExpanded = false;
                    }
                    else
                    {
                        // For desktop, we still want sidebar hidden by default like settings page
                        isLeftPanelHidden = true;
                        isLeftPanelExpanded = false;
                    }
                    isInitialized = true;
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking mobile device: {ex.Message}");
                    // Default to hidden sidebar
                    isLeftPanelHidden = true;
                    isLeftPanelExpanded = false;
                    StateHasChanged();
                }
            }
        }

        private void ToggleGroup(string groupId)
        {
            if (expandedGroups.Contains(groupId))
                expandedGroups.Remove(groupId);
            else
                expandedGroups.Add(groupId);
        }

        private void HideLeftPanel()
        {
            isLeftPanelHidden = true;
            isLeftPanelExpanded = false;
            StateHasChanged();
        }

        private void GoBack()
        {
            Navigation.NavigateTo("/home");
        }

        private void SelectLeftTab(string tab)
        {
            selectedLeftTab = tab;
            // Auto-hide sidebar on mobile/tablet after selection
            if (isLeftPanelHidden == false)
            {
                // Small delay to let the tab selection register first
                HideLeftPanel();
            }
            StateHasChanged();
        }

        private void ShowLeftPanel()
        {
            isLeftPanelHidden = false;
            isLeftPanelExpanded = true;
            StateHasChanged();
        }
    }
}