using SenangRetails.Shared.Enums;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Components.History
{
    public partial class CollectionHistory : ComponentBase
    {
        [Parameter] public HistoryView View { get; set; }


        protected override void OnParametersSet()
        {
            LoadData();
        }

        private void LoadData()
        {
            StateHasChanged(); 
        }

        private int DataCount => View == HistoryView.CollectionTNG ? 2 : 1;

        private string GetViewTitle() => View switch
        {
            HistoryView.CollectionCash => "Cash",
            HistoryView.CollectionDebitCard => "Debit Card",
            HistoryView.CollectionCreditCard => "Credit Card",
            HistoryView.CollectionTNG => "Touch 'n Go",
            HistoryView.CollectionOnline => "Online Transfer",
            HistoryView.CollectionQRPay => "QR Pay",
            _ => "Other Payment"
        };

        private string GetBadgePrefix() => View switch
        {
            HistoryView.CollectionTNG => "TNG",
            HistoryView.CollectionCash => "CASH",
            HistoryView.CollectionOnline => "ONLINE",
            HistoryView.CollectionCreditCard => "CC",
            HistoryView.CollectionDebitCard => "DC",
            HistoryView.CollectionQRPay => "QR Pay",
            _ => "PAID"
        };

        private int GetFilteredTotal() => View switch
        {
            HistoryView.CollectionTNG => 2, 
            HistoryView.CollectionCash => 10, 
            _ => 0
        };

        private List<RecordModel> GetMockRecords()
        {
            var count = GetFilteredTotal();
            var list = new List<RecordModel>();
            for (int i = 1; i <= count; i++)
            {
                list.Add(new RecordModel
                {
                    Id = $"#00000000{i:D2}",
                    Amount = "209.00",
                    Time = "05:41 PM, 06/02/2026"
                });
            }
            return list;
        }

        public class RecordModel
        {
            public string Id { get; set; }
            public string Amount { get; set; }
            public string Time { get; set; }
        }

        bool ShowReceiptModal;

        void OpenReceipt() => ShowReceiptModal = true;
        void CloseReceipt() => ShowReceiptModal = false;
    }
}
