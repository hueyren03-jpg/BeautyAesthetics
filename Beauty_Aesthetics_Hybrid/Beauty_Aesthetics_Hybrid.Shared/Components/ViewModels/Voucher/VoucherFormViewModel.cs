using Beauty_Aesthetics_WebPos.Components.Models.Voucher;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Beauty_Aesthetics_WebPos.ViewModels
{
    public class VoucherFormViewModel
    {
        private NavigationManager? _navigationManager;

        public bool IsEditMode { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public Voucher Voucher { get; set; } = new Voucher();

        // Dynamic dropdown lists
        public List<string> VoucherTypes { get; set; } = new() { "Cash Voucher", "Discount Voucher", "Gift Voucher" };
        public List<string> ExpiryTypes { get; set; } = new() { "Day From Purchase", "Fixed Date" };

        public string Prefix { get; set; } = string.Empty;
        public string NumberCreationMode { get; set; } = "Auto";
        public int RangeFrom { get; set; } = 1;
        public int RangeTo { get; set; }
        public int VoucherNumberLength { get; set; } = 10;
        public string SelectedExpiryType { get; set; } = "Day From Purchase";

        // Branch dropdown (mock for now)
        public List<string> BranchList { get; set; } = new()
        {
            "All Branches",
            "Main Branch",
            "Outlet 1",
            "Outlet 2"
        };

        public void SetNavigationManager(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public ObservableCollection<GeneratedVoucherItem> GeneratedList { get; set; } = new();

        public async Task LoadAsync()
        {
            await Task.Delay(10);

            Voucher = new Voucher
            {
                Name = "",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(1),
                TotalQuantity = 0,
                ExpiryDays = 0,
                DiscountType = VoucherTypes[0],
                CanSellBranch = BranchList[0],
                CanRedeemBranch = BranchList[0]
            };
        }

        // backend
        public async Task<bool> SaveAsync()
        {
            await Task.Delay(10);

            if (string.IsNullOrWhiteSpace(Voucher.Name))
            {
                ErrorMessage = "Voucher Name is required.";
                return false;
            }

            if (Voucher.TotalQuantity <= 0)
            {
                ErrorMessage = "Total vouchers to generate must be greater than zero.";
                return false;
            }

            if (Voucher.EndDate.Date < Voucher.StartDate.Date)
            {
                ErrorMessage = "Expiry date must be on or after the effective date.";
                return false;
            }

            Voucher.AmountRange = Voucher.VoucherAmount.ToString("N2");
            Voucher.DiscountRange = Voucher.DiscountValue.ToString();
            ErrorMessage = "";
            return true;
        }

        // backend
        public void Cancel()
        {
            _navigationManager?.NavigateTo("/voucher", forceLoad: true);
        }

        public void GenerateVouchers()
        {
            GeneratedList.Clear();

            if (Voucher.TotalQuantity <= 0)
            {
                return;
            }

            var safePrefix = Prefix?.Trim().ToUpperInvariant() ?? string.Empty;
            var numericLength = Math.Max(1, VoucherNumberLength - safePrefix.Length);

            for (int i = 0; i < Voucher.TotalQuantity; i++)
            {
                var sequence = (Math.Max(0, RangeFrom) + i).ToString().PadLeft(numericLength, '0');
                GeneratedList.Add(new GeneratedVoucherItem
                {
                    VoucherNo = $"{safePrefix}{sequence}",
                    Name = Voucher.Name,
                    SellableAt = Voucher.CanSellBranch,
                    RedeemableAt = Voucher.CanRedeemBranch,
                    ExpiryDays = Voucher.ExpiryDays,
                    EffectiveDate = Voucher.StartDate.ToString("dd/MM/yyyy"),
                    ExpiryDate = Voucher.EndDate.ToString("dd/MM/yyyy")
                });
            }

            RangeTo = Math.Max(0, RangeFrom) + Voucher.TotalQuantity - 1;
        }

        public void RemoveAllGenerated() => GeneratedList.Clear();
    }

    public class GeneratedVoucherItem
    {
        public string VoucherNo { get; set; } = "";
        public string Name { get; set; } = "";
        public string SellableAt { get; set; } = "";
        public string RedeemableAt { get; set; } = "";
        public int ExpiryDays { get; set; }
        public string EffectiveDate { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
    }
}
