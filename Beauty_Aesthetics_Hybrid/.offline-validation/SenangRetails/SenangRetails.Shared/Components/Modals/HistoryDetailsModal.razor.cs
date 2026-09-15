using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;
using SenangRetails.Shared.Models;


namespace SenangRetails.Shared.Components.Modals
{
    public partial class HistoryDetailsModal
    {

        [Parameter] public ReceiptDM ReceiptModel { get; set; } = new();

        private bool IsNumpadVisible = false;
        private bool IsEditingPayment = false;
        private bool IsSelectingCashier = false;
        private bool IsBackdateSaleEditing = false;
        private bool showConfirmButtons = false;

        private ServiceItemDM SelectedService = null;
        private PaymentMethodDM SelectedPaymentForNumpad = null;
        private string NumpadBuffer = "";


        private List<string> AvailableMethods = new List<string> { "Cash", "TNG", "Credit Card", "GrabPay" };
        private List<StaffDetailsDM> FilteredStaffs = new List<StaffDetailsDM>
        {
        new StaffDetailsDM { Name = "Bella", PhoneNumber = "011-222333", PhotoUrl = "" },
        new StaffDetailsDM { Name = "Chris", PhoneNumber = "011-444555", PhotoUrl = "" }
        };

        private StaffDetailsDM CurrentStaff = new StaffDetailsDM { Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 };


        private bool IsSelectStaffModalOpen = false;
        private string StaffSearchKeyword = "";

        private List<StaffDetailsDM> AllStaffs = new List<StaffDetailsDM>
        {
            new StaffDetailsDM{ Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 },
            new StaffDetailsDM{ Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 },
            new StaffDetailsDM{ Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 },
            new StaffDetailsDM{ Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 },
            new StaffDetailsDM{ Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 },
            new StaffDetailsDM{ Name = "YJ", PhoneNumber = "(6019) 731-2802", EffortPercentage = 100, Value = 62.50m, HofPercentage = 0 }
        };
        private void OpenSelectStaffModal()
        {
            StaffSearchKeyword = "";
            IsSelectStaffModalOpen = true;

        }

        private void SelectStaff(StaffDetailsDM staff)
        {
            if (SelectedService != null)
            {
                if (!SelectedService.ServedByStaffs.Any(s => s.Name == staff.Name))
                {
                    SelectedService.ServedByStaffs.Add(staff);

                }
            }
            IsSelectStaffModalOpen = false;
        }
        private void RemoveStaffFromItem(StaffDetailsDM staff)
        {
            if (SelectedService != null && SelectedService.ServedByStaffs.Contains(staff))
            {
                SelectedService.ServedByStaffs.Remove(staff);
                StateHasChanged();
            }
        }


        protected override void OnInitialized()
        {

        }

        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }

        private void EnterEditPayment() => IsEditingPayment = true;
        private void EnterSelectCashier() => IsSelectingCashier = true;
        private void OpenNumpad(PaymentMethodDM pay) { SelectedPaymentForNumpad = pay; NumpadBuffer = pay.Amount.ToString(); IsNumpadVisible = true; }

        private void SelectCashier(StaffDetailsDM? staff)
        {
            if (ReceiptModel != null)
            {
                if (staff == null) 
                {
                    ReceiptModel.CashierName = " ";
                }
                else
                {
                    ReceiptModel.CashierName = staff?.Name ?? "No Cashier";
                }
            }
            IsSelectingCashier = false; 
            StateHasChanged();
        }
        private void OnConfirmVoid() { ReceiptModel.IsVoided = true; ReceiptModel.VoidedDate = DateTime.Now; showConfirmButtons = false; }
        private void SaveNumpadValue() { IsNumpadVisible = false; }
        private decimal PaymentDifference => ReceiptModel.TotalSales - ReceiptModel.Payments.Sum(x => x.Amount);


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
