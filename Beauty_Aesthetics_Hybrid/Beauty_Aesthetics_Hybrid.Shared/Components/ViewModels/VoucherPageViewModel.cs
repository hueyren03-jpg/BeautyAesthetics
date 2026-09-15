using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Components.Models.Voucher;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels
{
    public class VoucherPageViewModel
    {
        private string _searchTerm = "";
        private string _selectedStatus = "";

        public ObservableCollection<Voucher> Vouchers { get; private set; } = new();

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                CurrentPage = 1;
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                CurrentPage = 1;
            }
        }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 5;

        private readonly NavigationManager _navManager;

        public VoucherPageViewModel(NavigationManager navManager)
        {
            _navManager = navManager;
        }

        public async Task LoadVouchersAsync()
        {
            await Task.Delay(200);

            Vouchers = new ObservableCollection<Voucher>
            {
                new Voucher { Name="DISCOUNT ABC", StartDate=DateTime.Parse("2025-07-16"), EndDate=DateTime.Parse("2025-07-17"), TotalQuantity=50, ExpiryDays=30, DiscountRange="10 - 15", AmountRange="50.00 - 51.00", Available=20, Sold=10, Redeemed=20 },
                new Voucher { Name="SPECIAL PROMOTION", StartDate=DateTime.Parse("2025-07-16"), EndDate=DateTime.Parse("2025-07-17"), TotalQuantity=10, ExpiryDays=30, DiscountRange="10", AmountRange="10.00", Expired=10 },
                new Voucher { Name="RM 10 DISCOUNT", StartDate=DateTime.Parse("2025-07-16"), EndDate=DateTime.Parse("2025-07-17"), TotalQuantity=1, ExpiryDays=30, DiscountRange="10", AmountRange="30.00", Available=1 },
                new Voucher { Name="NEW YEAR DISCOUNT", StartDate=DateTime.Parse("2025-07-16"), EndDate=DateTime.Parse("2025-07-17"), TotalQuantity=10, ExpiryDays=30, DiscountRange="10", AmountRange="10.00", Redeemed=5, Expired=5 },
                new Voucher { Name="CHRISTMAS DISCOUNT", StartDate=DateTime.Parse("2025-07-16"), EndDate=DateTime.Parse("2025-07-17"), TotalQuantity=10, ExpiryDays=30, DiscountRange="10", AmountRange="10.00", Sold=5, Redeemed=5 }
            };
        }

        private IEnumerable<Voucher> FilteredQuery()
        {
            var query = Vouchers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                query = query.Where(v => v.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SelectedStatus))
            {
                query = query.Where(v => GetStatus(v).Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        public IEnumerable<Voucher> FilteredVouchers()
        {
            return FilteredQuery()
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize);
        }

        public int FilteredCount => FilteredQuery().Count();
        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)FilteredCount / PageSize));

        public static string GetStatus(Voucher voucher)
        {
            if (voucher.TotalQuantity > 0 && voucher.Expired >= voucher.TotalQuantity)
                return "Expired";
            if (voucher.Available > 0)
                return "Available";
            if (voucher.Redeemed > 0)
                return "Redeemed";
            if (voucher.Sold > 0)
                return "Sold";

            return "Unavailable";
        }

        public void ClearFilters()
        {
            _searchTerm = "";
            _selectedStatus = "";
            CurrentPage = 1;
        }

        public void DeleteVoucher(string voucherName)
        {
            var voucher = Vouchers.FirstOrDefault(v =>
                string.Equals(v.Name, voucherName, StringComparison.OrdinalIgnoreCase));

            if (voucher is null)
            {
                return;
            }

            Vouchers.Remove(voucher);
            CurrentPage = Math.Min(CurrentPage, TotalPages);
        }

        public void GoToPage(int page)
        {
            if (page >= 1 && page <= TotalPages)
                CurrentPage = page;
        }

        public void NextPage() => GoToPage(CurrentPage + 1);
        public void PrevPage() => GoToPage(CurrentPage - 1);
    }
}
