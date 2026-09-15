using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Pages.ConfigPages
{
    public partial class AccountSettings
    {
        private readonly NavigationManager navigationManager;
        private readonly IJSRuntime jsRuntime;
        private void Back()
        {
            navigationManager.NavigateTo("/config");
        }
        public AccountSettings(NavigationManager navigationManager, IJSRuntime jsRuntime)
        {
            this.navigationManager = navigationManager;
            this.jsRuntime = jsRuntime;
        }

        private class GLAccount
        {
            public string GLDescription { get; set; }
            public string GLCode { get; set; }
        }

        // Control Accounts
        private string DefaultDebtorControlAccount { get; set; } = "Account Receivable";
        private bool DebtorControlAccountIsOpen { get; set; } = false;
        private bool DebtorControlAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> DebtorControlAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Account Receivable", GLCode = "14-1-0003" },
        new GLAccount { GLDescription = "Accounts Receivable", GLCode = "14-1-0004" }
    };

        private string DefaultCreditorControlAccount { get; set; } = "Trade Creditors";
        private bool CreditorControlAccountIsOpen { get; set; } = false;
        private bool CreditorControlAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> CreditorControlAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Trade Creditors", GLCode = "21-1-0001" },
        new GLAccount { GLDescription = "Trade Payables", GLCode = "21-1-0002" }
    };

        // Sales
        private string SalesAccount { get; set; } = "Sales";
        private bool SalesAccountIsOpen { get; set; } = false;
        private bool SalesAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> SalesAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Sales", GLCode = "41-1-0001" },
        new GLAccount { GLDescription = "Sales Revenue", GLCode = "41-1-0002" }
    };

        private string CashSalesAccount { get; set; } = "Cash Sales";
        private bool CashSalesAccountIsOpen { get; set; } = false;
        private bool CashSalesAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> CashSalesAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Cash Sales", GLCode = "41-2-0001" }
    };

        private string SalesReturnAccount { get; set; } = "Sales - Return Inwards";
        private bool SalesReturnAccountIsOpen { get; set; } = false;
        private bool SalesReturnAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> SalesReturnAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Sales - Return Inwards", GLCode = "42-1-0001" }
    };

        private string SalesDepositAccount { get; set; } = "Deposits";
        private bool SalesDepositAccountIsOpen { get; set; } = false;
        private bool SalesDepositAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> SalesDepositAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Deposits", GLCode = "22-1-0001" }
    };

        // Purchases
        private string PurchaseAccount { get; set; } = "Purchase";
        private bool PurchaseAccountIsOpen { get; set; } = false;
        private bool PurchaseAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> PurchaseAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Purchase", GLCode = "51-1-0001" }
    };

        private string CashPurchaseAccount { get; set; } = "Purchase";
        private bool CashPurchaseAccountIsOpen { get; set; } = false;
        private bool CashPurchaseAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> CashPurchaseAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Purchase", GLCode = "51-1-0001" }
    };

        private string PurchaseReturnAccount { get; set; } = "Purchase";
        private bool PurchaseReturnAccountIsOpen { get; set; } = false;
        private bool PurchaseReturnAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> PurchaseReturnAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Purchase", GLCode = "51-1-0001" }
    };

        // Stock
        private string OpeningStockAccount { get; set; } = "";
        private bool OpeningStockAccountIsOpen { get; set; } = false;
        private bool OpeningStockAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> OpeningStockAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Opening Stock", GLCode = "13-1-0001" }
    };

        private string ClosingStockAccount { get; set; } = "";
        private bool ClosingStockAccountIsOpen { get; set; } = false;
        private bool ClosingStockAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> ClosingStockAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Closing Stock", GLCode = "13-2-0001" }
    };

        private string StockBalanceAccount { get; set; } = "";
        private bool StockBalanceAccountIsOpen { get; set; } = false;
        private bool StockBalanceAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> StockBalanceAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Stock Balance", GLCode = "13-3-0001" }
    };

        // Others
        private string ForexAccount { get; set; } = "Forex Loss/ (Gain)";
        private bool ForexAccountIsOpen { get; set; } = false;
        private bool ForexAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> ForexAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Forex Loss/ (Gain)", GLCode = "61-1-0001" }
    };

        private string BankChargesAccount { get; set; } = "Bank Charges";
        private bool BankChargesAccountIsOpen { get; set; } = false;
        private bool BankChargesAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> BankChargesAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Bank Charges", GLCode = "62-1-0001" }
    };

        private string ContraAccount { get; set; } = "";
        private bool ContraAccountIsOpen { get; set; } = false;
        private bool ContraAccountOpenUpwards { get; set; } = false;
        private List<GLAccount> ContraAccountItems { get; set; } = new()
    {
        new GLAccount { GLDescription = "Contra Account", GLCode = "99-1-0001" }
    };

        // Toggle methods for Control Accounts
        private async Task ToggleDebtorControlAccount()
        {
            if (!DebtorControlAccountIsOpen)
            {
                DebtorControlAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "DebtorControlAccountSelect"
                );
                DebtorControlAccountIsOpen = true;
            }
            else
            {
                DebtorControlAccountIsOpen = false;
            }
        }

        private void SelectDebtorControlAccount(GLAccount item)
        {
            DefaultDebtorControlAccount = item.GLDescription;
            DebtorControlAccountIsOpen = false;
        }

        private async Task ToggleCreditorControlAccount()
        {
            if (!CreditorControlAccountIsOpen)
            {
                CreditorControlAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "CreditorControlAccountSelect"
                );
                CreditorControlAccountIsOpen = true;
            }
            else
            {
                CreditorControlAccountIsOpen = false;
            }
        }

        private void SelectCreditorControlAccount(GLAccount item)
        {
            DefaultCreditorControlAccount = item.GLDescription;
            CreditorControlAccountIsOpen = false;
        }

        // Toggle methods for Sales
        private async Task ToggleSalesAccount()
        {
            if (!SalesAccountIsOpen)
            {
                SalesAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "SalesAccountSelect"
                );
                SalesAccountIsOpen = true;
            }
            else
            {
                SalesAccountIsOpen = false;
            }
        }

        private void SelectSalesAccount(GLAccount item)
        {
            SalesAccount = item.GLDescription;
            SalesAccountIsOpen = false;
        }

        private async Task ToggleCashSalesAccount()
        {
            if (!CashSalesAccountIsOpen)
            {
                CashSalesAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "CashSalesAccountSelect"
                );
                CashSalesAccountIsOpen = true;
            }
            else
            {
                CashSalesAccountIsOpen = false;
            }
        }

        private void SelectCashSalesAccount(GLAccount item)
        {
            CashSalesAccount = item.GLDescription;
            CashSalesAccountIsOpen = false;
        }

        private async Task ToggleSalesReturnAccount()
        {
            if (!SalesReturnAccountIsOpen)
            {
                SalesReturnAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "SalesReturnAccountSelect"
                );
                SalesReturnAccountIsOpen = true;
            }
            else
            {
                SalesReturnAccountIsOpen = false;
            }
        }

        private void SelectSalesReturnAccount(GLAccount item)
        {
            SalesReturnAccount = item.GLDescription;
            SalesReturnAccountIsOpen = false;
        }

        private async Task ToggleSalesDepositAccount()
        {
            if (!SalesDepositAccountIsOpen)
            {
                SalesDepositAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "SalesDepositAccountSelect"
                );
                SalesDepositAccountIsOpen = true;
            }
            else
            {
                SalesDepositAccountIsOpen = false;
            }
        }

        private void SelectSalesDepositAccount(GLAccount item)
        {
            SalesDepositAccount = item.GLDescription;
            SalesDepositAccountIsOpen = false;
        }

        // Toggle methods for Purchases
        private async Task TogglePurchaseAccount()
        {
            if (!PurchaseAccountIsOpen)
            {
                PurchaseAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "PurchaseAccountSelect"
                );
                PurchaseAccountIsOpen = true;
            }
            else
            {
                PurchaseAccountIsOpen = false;
            }
        }

        private void SelectPurchaseAccount(GLAccount item)
        {
            PurchaseAccount = item.GLDescription;
            PurchaseAccountIsOpen = false;
        }

        private async Task ToggleCashPurchaseAccount()
        {
            if (!CashPurchaseAccountIsOpen)
            {
                CashPurchaseAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "CashPurchaseAccountSelect"
                );
                CashPurchaseAccountIsOpen = true;
            }
            else
            {
                CashPurchaseAccountIsOpen = false;
            }
        }

        private void SelectCashPurchaseAccount(GLAccount item)
        {
            CashPurchaseAccount = item.GLDescription;
            CashPurchaseAccountIsOpen = false;
        }

        private async Task TogglePurchaseReturnAccount()
        {
            if (!PurchaseReturnAccountIsOpen)
            {
                PurchaseReturnAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "PurchaseReturnAccountSelect"
                );
                PurchaseReturnAccountIsOpen = true;
            }
            else
            {
                PurchaseReturnAccountIsOpen = false;
            }
        }

        private void SelectPurchaseReturnAccount(GLAccount item)
        {
            PurchaseReturnAccount = item.GLDescription;
            PurchaseReturnAccountIsOpen = false;
        }

        // Toggle methods for Stock
        private async Task ToggleOpeningStockAccount()
        {
            if (!OpeningStockAccountIsOpen)
            {
                OpeningStockAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "OpeningStockAccountSelect"
                );
                OpeningStockAccountIsOpen = true;
            }
            else
            {
                OpeningStockAccountIsOpen = false;
            }
        }

        private void SelectOpeningStockAccount(GLAccount item)
        {
            OpeningStockAccount = item.GLDescription;
            OpeningStockAccountIsOpen = false;
        }

        private async Task ToggleClosingStockAccount()
        {
            if (!ClosingStockAccountIsOpen)
            {
                ClosingStockAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "ClosingStockAccountSelect"
                );
                ClosingStockAccountIsOpen = true;
            }
            else
            {
                ClosingStockAccountIsOpen = false;
            }
        }

        private void SelectClosingStockAccount(GLAccount item)
        {
            ClosingStockAccount = item.GLDescription;
            ClosingStockAccountIsOpen = false;
        }

        private async Task ToggleStockBalanceAccount()
        {
            if (!StockBalanceAccountIsOpen)
            {
                StockBalanceAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "StockBalanceAccountSelect"
                );
                StockBalanceAccountIsOpen = true;
            }
            else
            {
                StockBalanceAccountIsOpen = false;
            }
        }

        private void SelectStockBalanceAccount(GLAccount item)
        {
            StockBalanceAccount = item.GLDescription;
            StockBalanceAccountIsOpen = false;
        }

        // Toggle methods for Others
        private async Task ToggleForexAccount()
        {
            if (!ForexAccountIsOpen)
            {
                ForexAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "ForexAccountSelect"
                );
                ForexAccountIsOpen = true;
            }
            else
            {
                ForexAccountIsOpen = false;
            }
        }

        private void SelectForexAccount(GLAccount item)
        {
            ForexAccount = item.GLDescription;
            ForexAccountIsOpen = false;
        }

        private async Task ToggleBankChargesAccount()
        {
            if (!BankChargesAccountIsOpen)
            {
                BankChargesAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "BankChargesAccountSelect"
                );
                BankChargesAccountIsOpen = true;
            }
            else
            {
                BankChargesAccountIsOpen = false;
            }
        }

        private void SelectBankChargesAccount(GLAccount item)
        {
            BankChargesAccount = item.GLDescription;
            BankChargesAccountIsOpen = false;
        }

        private async Task ToggleContraAccount()
        {
            if (!ContraAccountIsOpen)
            {
                ContraAccountOpenUpwards = await JSRuntime.InvokeAsync<bool>(
                    "dropdownPositioner.shouldOpenUpwards",
                    "ContraAccountSelect"
                );
                ContraAccountIsOpen = true;
            }
            else
            {
                ContraAccountIsOpen = false;
            }
        }

        private void SelectContraAccount(GLAccount item)
        {
            ContraAccount = item.GLDescription;
            ContraAccountIsOpen = false;
        }
    }
}
