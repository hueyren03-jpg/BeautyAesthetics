using System;
using Microsoft.AspNetCore.Components;
using SenangRetails.Shared.Components.Inventory;

namespace SenangRetails.Shared.Pages
{
    public partial class Inventory
    {
        private bool isNavOpen = false;

        private void ToggleNav()
        {
            isNavOpen = !isNavOpen;
        }

        private void CloseNav()
        {
            isNavOpen = false;
        }

        private string selectedNav = "summary";

        private string searchQuery = "";

        private void FilterSettings()
        {
            StateHasChanged();
        }

        private bool IsVisible(string keyword)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return true;

            return keyword.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOverviewOpen { get; set; } = true;
        private bool IsOperationsOpen { get; set; } = true;
        private bool IsPartnersOpen { get; set; } = true;

        private bool isStockInModalOpen = false;
        private bool isStockOutModalOpen = false;

        private InventoryStockIn? stockInComponent;
        private InventoryStockIn? stockInModalComponent;
        private InventoryStockOut? stockOutComponent;
        private InventoryStockOut? stockOutModalComponent;

        // Success Alert / Popup Notification State
        private bool isNotificationVisible = false;
        private string notificationMessage = "";
        private string notificationType = "success";

        // Shared Modal Overlays State
        private bool isAddProductModalOpen = false;
        private bool isItemDetailsModalOpen = false;
        private bool isQuantityCalculatorOpen = false;
        private bool isPriceCalculatorOpen = false;

        private string activeModalContext = ""; // "stockin" or "stockout"
        private string addProductTitle = "Add Product";
        private string itemDetailsTitle = "Item Details";

        // Reactive stocks grid bindings
        private int stockInQuantity = 10;
        private double stockInPrice = 5.00;

        private int stockOutQuantity = 2;
        private double stockOutPrice = 5.00;

        // Binders for overlays
        private int selectedQuantity = 10;
        private double selectedCost = 5.00;
        private double selectedPrice = 5.00;
        private string selectedExpiryDate = "";

        private void SelectSection(string section)
        {
            selectedNav = section;
            if (isNavOpen) isNavOpen = false;
        }

        private void OpenModalSection(string section)
        {
            if (section == "stockin")
            {
                isStockInModalOpen = true;
            }
            else if (section == "stockout")
            {
                isStockOutModalOpen = true;
            }
            else
            {
                SelectSection(section);
            }
        }

        private void CloseStockInModal()
        {
            isStockInModalOpen = false;
        }

        private void CloseStockOutModal()
        {
            isStockOutModalOpen = false;
        }

        // Shared Modals Navigation Flow Methods
        private void OpenAddProductFlow(string context)
        {
            activeModalContext = context;
            addProductTitle = context == "stockin" ? "Add Product" : "Add Product (Stock Out)";
            isAddProductModalOpen = true;
        }

        private void CloseAddProductModal()
        {
            isAddProductModalOpen = false;
        }

        private void HandleProductSelected(string masterAccountId)
        {
            isAddProductModalOpen = false;
            if (activeModalContext == "stockin")
            {
                if (isStockInModalOpen)
                {
                    stockInModalComponent?.AddProductToStockIn(masterAccountId);
                }
                else
                {
                    stockInComponent?.AddProductToStockIn(masterAccountId);
                }
            }
            else if (activeModalContext == "stockout")
            {
                if (isStockOutModalOpen)
                {
                    stockOutModalComponent?.AddProductToStockOut(masterAccountId);
                }
                else
                {
                    stockOutComponent?.AddProductToStockOut(masterAccountId);
                }
            }
        }

        private void OpenQuantityCalculatorFlow(string context)
        {
            activeModalContext = context;
            selectedQuantity = context == "stockin" ? stockInQuantity : stockOutQuantity;
            isQuantityCalculatorOpen = true;
        }

        private void CloseQuantityCalculator()
        {
            isQuantityCalculatorOpen = false;
        }

        private void HandleQuantitySave(double value)
        {
            selectedQuantity = (int)value;
            isQuantityCalculatorOpen = false;
            
            // Transition directly to Item Details form modal
            itemDetailsTitle = activeModalContext == "stockin" ? "Item Details" : "Item Details (Stock Out)";
            selectedCost = activeModalContext == "stockin" ? stockInPrice : stockOutPrice;
            selectedExpiryDate = "";
            isItemDetailsModalOpen = true;
        }

        private void OpenPriceCalculatorFlow(string context)
        {
            activeModalContext = context;
            selectedPrice = context == "stockin" ? stockInPrice : stockOutPrice;
            isPriceCalculatorOpen = true;
        }

        private void ClosePriceCalculator()
        {
            isPriceCalculatorOpen = false;
        }

        private void HandlePriceSave(double value)
        {
            if (activeModalContext == "stockin")
            {
                stockInPrice = value;
            }
            else
            {
                stockOutPrice = value;
            }
            isPriceCalculatorOpen = false;
        }

        private void CloseItemDetailsModal()
        {
            isItemDetailsModalOpen = false;
        }

        private void HandleItemDetailsConfirm()
        {
            if (activeModalContext == "stockin")
            {
                stockInQuantity = selectedQuantity;
                stockInPrice = selectedCost;
            }
            else
            {
                stockOutQuantity = selectedQuantity;
                stockOutPrice = selectedCost;
            }
            isItemDetailsModalOpen = false;
        }

        private void HandleStockInConfirm()
        {
            isStockInModalOpen = false;
            notificationMessage = "Stock In confirmed successfully!";
            notificationType = "success";
            isNotificationVisible = true;
            if (selectedNav == "stockin")
            {
                selectedNav = "summary";
            }
            StateHasChanged();
        }

        private void HandleStockOutConfirm()
        {
            isStockOutModalOpen = false;
            notificationMessage = "Stock Out confirmed successfully!";
            notificationType = "success";
            isNotificationVisible = true;
            if (selectedNav == "stockout")
            {
                selectedNav = "summary";
            }
            StateHasChanged();
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            var uri = NavigationManager.Uri;
            if (uri.Contains("tab=stocktransfer") || uri.Contains("tab=incomingstocktransfer"))
            {
                selectedNav = "incomingstocktransfer";
            }
        }

        private void GoBack()
        {
            var returnUrl = NavigationManager.Uri.Contains("returnTo=admin") ? "/admin" : "/home";
            NavigationManager.NavigateTo(returnUrl);
        }
    }
}
