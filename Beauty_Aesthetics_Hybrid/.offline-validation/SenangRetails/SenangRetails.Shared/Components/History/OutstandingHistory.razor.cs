using SenangRetails.Shared.Enums;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Components.History
{
    public partial class OutstandingHistory : ComponentBase
    {
        [Parameter] public HistoryView View { get; set; }

        public class OutstandingRecord
        {
            public string OrderNo { get; set; }
            public string Time { get; set; }
            public string CustomerName { get; set; }
            public string CustomerPhone { get; set; }
            public string PaidAmount { get; set; }
            public string OwedAmount { get; set; }
            public int ItemCount { get; set; }
        }

        private int GetFilteredTotal() => View switch
        {
            HistoryView.OutstandingTotal => 5,
            HistoryView.OutstandingCollection => 3,
            _ => 1 
        };

        private List<OutstandingRecord> GetMockOrders()
        {
            var count = GetFilteredTotal();
            var list = new List<OutstandingRecord>();

            for (int i = 1; i <= count; i++)
            {
                list.Add(new OutstandingRecord
                {
                    OrderNo = $"00000000{16 + i}",
                    Time = "02:30 PM, 09/02/2026",
                    CustomerName = "WK",
                    CustomerPhone = "•••••••3528",
                    PaidAmount = "9.00",
                    OwedAmount = "80.00",
                    ItemCount = 1
                });
            }
            return list;
        }

        bool ShowReceiptModal;
        private OutstandingRecord SelectedRecord { get; set; }

        void OpenReceipt(OutstandingRecord record)
        {
            SelectedRecord = record;
            ShowReceiptModal = true;
        }

        void CloseReceipt() => ShowReceiptModal = false;
    }


}
