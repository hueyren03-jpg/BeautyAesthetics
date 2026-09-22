using Beauty_Aesthetics_WebPos.Components.Models.Voucher;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels
{
    public class VoucherDetailsViewModel
    {
        private string _searchTerm = "";
        private string _selectedStatus = "";

        public Voucher? CurrentVoucher { get; set; }
        public ObservableCollection<VoucherItem> Items { get; private set; } = new();

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

        private readonly NavigationManager _nav;

        public VoucherDetailsViewModel(NavigationManager nav)
        {
            _nav = nav;
        }

        public Task DeleteVoucherItemAsync(string code)
        {
            var item = Items.FirstOrDefault(v => v.Code == code);
            if (item != null)
            {
                Items.Remove(item);
            }

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            return Task.CompletedTask;
        }

        public async Task LoadVoucherDetailsAsync(string name)
        {
            await Task.Delay(150);

            CurrentVoucher = new Voucher
            {
                Name = name,
                StartDate = DateTime.Parse("2025-01-16"),
                EndDate = DateTime.Parse("2025-01-17"),
                TotalQuantity = 50,
                ExpiryDays = 30,
                DiscountRange = "10 - 20",
                AmountRange = "10.00 - 50.00",
                Available = 20,
                Sold = 20,
                Redeemed = 10,
                Expired = 0
            };

            Items = new ObservableCollection<VoucherItem>
            {
                new VoucherItem("A000001","Available",50,10),
                new VoucherItem("A000002","Sold",10,10),
                new VoucherItem("A000003","Redeemed",30,10),
                new VoucherItem("A000004","Redeemed",10,10),
                new VoucherItem("A000005","Redeemed",10,10),
            };
        }

        private IEnumerable<VoucherItem> FilteredQuery()
        {
            var query = Items.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
                query = query.Where(i => i.Code.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedStatus))
                query = query.Where(i => i.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase));

            return query;
        }

        public IEnumerable<VoucherItem> FilteredItems()
        {
            return FilteredQuery()
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize);
        }

        public int FilteredCount => FilteredQuery().Count();
        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)FilteredCount / PageSize));

        public void GoToPage(int page)
        {
            if (page >= 1 && page <= TotalPages)
                CurrentPage = page;
        }

        public void NextPage() => GoToPage(CurrentPage + 1);
        public void PrevPage() => GoToPage(CurrentPage - 1);
    }

    public class VoucherItem
    {
        public string Code { get; set; }
        public string Status { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int Discount { get; set; }
        public decimal Amount { get; set; }

        public VoucherItem(string code, string status, int discount, decimal amount)
        {
            Code = code;
            Status = status;
            Discount = discount;
            Amount = amount;
            ExpiryDate = DateTime.Parse("2025-01-20");
        }
    }
}