using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;

namespace Beauty_Aesthetics_WebPos.Components.Services;

public sealed class AppState
{
    public string? CurrentUserJson { get; private set; }
    public string? UserEmail { get; private set; }
    public DateTimeOffset? SignedInAt { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentUserJson);
    public IReadOnlyList<BranchLookupItem> AvailableBranches { get; private set; } = [];
    public BranchLookupItem? CurrentBranch { get; private set; }
    public Customer? SelectedCustomer { get; private set; }
    public string? SelectedBranchID => CurrentBranch?.Id;

    // Completed payment state, matching the Senang Retails complete-sales workflow.
    public string LastSaleDocumentId { get; set; } = string.Empty;
    public string LastReceiptNo { get; set; } = string.Empty;
    public decimal LastPaidAmount { get; set; }
    public string LastPaymentMethod { get; set; } = string.Empty;
    public Transaction? LastCompletedTransaction { get; set; }
    public string LastPaymentReturnUrl { get; set; } = "/case-notes";

    // Outstanding settlement completion state. Senang Retail routes a
    // successful AR Receipt payment through the same complete-sales screen,
    // but only exposes receipt printing and Close for Outstanding.
    public bool IsOutstandingPaymentMode { get; set; }
    public event Action? AuthenticationChanged;
    public event Action? BranchChanged;
    public event Action? CustomerChanged;

    public void SetAuthenticatedUser(string currentUserJson, string? userEmail = null)
    {
        CurrentUserJson = currentUserJson;

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            UserEmail = userEmail;
        }

        SignedInAt = DateTimeOffset.UtcNow;
        AuthenticationChanged?.Invoke();
    }

    public void ClearAuthentication()
    {
        CurrentUserJson = null;
        UserEmail = null;
        SignedInAt = null;
        ClearBranchSession();
        AuthenticationChanged?.Invoke();
    }

    public void SetAvailableBranches(IReadOnlyList<BranchLookupItem> branches)
    {
        AvailableBranches = branches;
        if (CurrentBranch is not null && !branches.Any(branch =>
                string.Equals(branch.Id, CurrentBranch.Id, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentBranch = null;
        }

        BranchChanged?.Invoke();
    }

    public void SelectBranch(BranchLookupItem branch)
    {
        CurrentBranch = branch;
        BranchChanged?.Invoke();
    }

    public void SelectCustomer(Customer customer)
    {
        SelectedCustomer = customer;
        CustomerChanged?.Invoke();
    }

    public void ClearSelectedCustomer()
    {
        SelectedCustomer = null;
        CustomerChanged?.Invoke();
    }

    public void ResetPaymentState()
    {
        LastSaleDocumentId = string.Empty;
        LastReceiptNo = string.Empty;
        LastPaidAmount = 0m;
        LastPaymentMethod = string.Empty;
        LastCompletedTransaction = null;
        LastPaymentReturnUrl = "/case-notes";
        IsOutstandingPaymentMode = false;
    }

    public void ClearBranchSession()
    {
        AvailableBranches = [];
        CurrentBranch = null;
        SelectedCustomer = null;
        ResetPaymentState();
        BranchChanged?.Invoke();
        CustomerChanged?.Invoke();
    }
}
