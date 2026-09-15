using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models.DTOs;
using static SenangRetails.Shared.Pages.Orders;

namespace SenangRetails.Shared.Services
{
    public class AppState
    {
        // User identity & permissions (populated on login via SecurityUserService.ReloadPermissionsAsync)
        public string? Platform { get; set; } = "Unknown";
        public string? UserID { get; set; }
        public string? EmployeeID { get; set; }
        public string? UserGroupID { get; set; }
        public string? UserGroupName { get; set; }
        public string? BranchGroupID { get; set; }
        public string? DefaultWorkingBranchID { get; set; }
        public string? UserEmail { get; set; }
        public Dictionary<string, SecurityPermissionItem>? Permissions { get; set; }

        public CustomerDM? SelectedCustomer { get; set; }
        public List<string> AvailableBranches { get; set; } = new();
        public string SelectedBranchID { get; set; } = string.Empty;
        public BranchItem? CurrentBranch { get; set; }
        public string SelectedBranchGroupID { get; set; } = string.Empty;
        public BranchItem? TaxTypeID { get; set; }
        public BranchItem? TaxTypeName { get; set; }
        public BranchItem? DefaultSalesTaxCodeID { get; set; }

        public decimal SalesToday { get; set; } = 0m;
        public event Action? OnSalesTodayChanged;
        public void NotifySalesTodayChanged() => OnSalesTodayChanged?.Invoke();
        public Doc_CashSales? CurrentOrder { get; set; }
        public bool IsOutstandingPaymentMode { get; set; }
        public decimal OutstandingPaymentAmount { get; set; }
        public Doc_CashSales_POSPaymentLineTypeDM? SelectedPaymentMethod { get; set; }
        public List<PaymentLine> AppliedPayments { get; set; } = new();
        public decimal LastPaidAmount { get; set; }
        public string LastSaleDocumentId { get; set; } = string.Empty;
        public string LastReceiptNo { get; set; } = string.Empty;
        public string LastPaymentMethod { get; set; } = string.Empty;
        public Doc_CashSales? LastCompletedOrder { get; set; }
        public void ResetPaymentState()
        {
            SelectedPaymentMethod = null;
            AppliedPayments.Clear();
            LastSaleDocumentId = string.Empty;
            LastReceiptNo = string.Empty;
            LastCompletedOrder = null;

        }
        public DefaultAccountDM? objDefaultAccountDM { get; set; }
        public DefaultAccount_CentralisedDM? objDefaultAccountCentralisedDM { get; set; }
        public Dictionary<string, GSTTaxCodeDM> lstGSTTaxCode { get; set; } = new();
        public InventoryDM? objServiceChargeItem { get; set; } = new();
        public Dictionary<string, InventoryDM> lstAllSalesItems { get; set; } = new();
        public Dictionary<string, MembershipTypeDM>? lstMembershipTypes { get; set; }
    }
}
