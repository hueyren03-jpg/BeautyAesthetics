using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Components;
using SenangRetails.Shared.Enums;

namespace SenangRetails.Shared.Pages
{
    public partial class History
    {
        [Microsoft.AspNetCore.Components.Inject]
        private NavigationManager NavigationManager { get; set; }

        protected string Label_FilterDateRange => "01/02/2026 - 09/02/2026";


        protected override void OnInitialized()
        {
            CurrentView = HistoryView.CollectionTNG;
            activeRowId = "col-tng";
        }
        //protected string Label_FilterDateRange => "06/02/2026";

        protected string Label_CollectionAmount => "209.00";

        protected string Label_OrderCount => "1";
        protected string Label_ItemCount => "1";
        protected string Label_CustomerCount => "1";
        protected string Label_StaffCount => "1";


        protected string Label_TotalOutstandingAmount => "0.00";
        protected string Label_OutstandingCollectionAmount => "0.00";


        protected string Label_Voucher => "0.00";

        protected string Label_Point => "0 pts";


        protected string Label_CreditAmount => "0.00";



        private bool isPrintMenuVisible = false;

        private void TogglePrintMenu()
        {
            isPrintMenuVisible = !isPrintMenuVisible;
        }

        private void ExportPDF() {  isPrintMenuVisible = false; }
        private void DirectPrint() {  isPrintMenuVisible = false; }




        private HistoryView CurrentView = HistoryView.None;

        void SelectView(HistoryView view)
        {
            CurrentView = view;
        }

        private string activeRowId = ""; 

        private void SelectRow(string rowId)
        {
            activeRowId = rowId;

            CurrentView = rowId switch
            {
                // -------- Collection --------
                "col-cash" => HistoryView.CollectionCash,
                "col-dc" => HistoryView.CollectionDebitCard,
                "col-cc" => HistoryView.CollectionCreditCard,
                "col-tng" => HistoryView.CollectionTNG,
                "col-online" => HistoryView.CollectionOnline,
                "col-qr" => HistoryView.CollectionQRPay,

                // -------- Work --------
                "work-order" => HistoryView.WorkOrder,
                "work-item" => HistoryView.WorkItem,
                "work-customer" => HistoryView.WorkCustomer,
                "work-staff" => HistoryView.WorkStaff,

                // -------- Outstanding --------
                "out-total" => HistoryView.OutstandingTotal,
                "out-coll" => HistoryView.OutstandingCollection,

                // -------- Signup --------
                "signup-credit" => HistoryView.SignupCredit,
                "signup-voucher" => HistoryView.SignupVoucher,
                "signup-point" => HistoryView.SignupPoint,

                // -------- Redeem --------
                "redeem-credit" => HistoryView.RedeemCredit,
                "redeem-voucher" => HistoryView.RedeemVoucher,
                "redeem-point" => HistoryView.RedeemPoint,

                _ => HistoryView.None
            };

            if (isSidebarExpanded)
            {
                isSidebarExpanded = false;
            }
        }



        private bool isSidebarExpanded = true;

        private void ToggleSidebar()
        {
            isSidebarExpanded = !isSidebarExpanded;
        }

        private void GoBack()
        {
            Nav.NavigateTo("/home");
        }




        /// <summary>
        /// 过后变成一个class
        /// </summary>
        public class PaymentMethod
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? LabelAmount { get; set; } // 对应你前端显示的金额变量名
            public bool IsVisible { get; set; } = true; // 预留 Logic：是否显示
        }

        // 这样写：加一个 = new() 或 = new List<PaymentMethod>()
        public List<PaymentMethod> CollectionMethods { get; set; } = new()
        {
                new PaymentMethod { Id = "col-tng", Name = "TNG", LabelAmount = "0.00" },
                new PaymentMethod { Id = "col-cc", Name = "Credit Card", LabelAmount = "0.00" },
                new PaymentMethod { Id = "col-cash", Name = "Cash", LabelAmount = "0.00" },
                new PaymentMethod { Id = "col-online", Name = "Online", LabelAmount = "0.00" },
                new PaymentMethod { Id = "col-dc", Name = "Debit Card", LabelAmount = "0.00" },
                new PaymentMethod { Id = "col-qr", Name = "QR Pay", LabelAmount = "0.00" }
        };


        private bool IsBackdateSaleEditing = false;
        private IEnumerable<DateTime?> GetCalendarDays()
        {
            var firstDayOfMonth = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(DisplayMonth.Year, DisplayMonth.Month);

            int offset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

            for (int i = 0; i < offset; i++) yield return null;
            for (int i = 1; i <= daysInMonth; i++) yield return new DateTime(DisplayMonth.Year, DisplayMonth.Month, i);
        }

        private DateTime DisplayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime? SelectedDate = DateTime.Today;
        private void EnterEditBackdateSale() => IsBackdateSaleEditing = true;
        private void SelectDate(DateTime date)
        {
            SelectedDate = date;
            IsBackdateSaleEditing = false;
        }

    }
}
