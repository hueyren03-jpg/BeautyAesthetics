using SenangRetails.Shared.Enums;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Components.History
{
    public partial class SignupRedeemHistory : ComponentBase
    {

        [Parameter] public HistoryView View { get; set; }


        public enum TransactionMode
        {
            Signup,
            Redeem
        }

        public enum TransactionType
        {
            Credit,
            Voucher,
            Point
        }
        public class HistoryRecord
        {
            public string Id { get; set; } = "";
            public string Amount { get; set; } = "";
            public string Timestamp { get; set; } = "";
            public string Staff { get; set; } = "";
        }



        [Parameter] public TransactionMode Mode { get; set; }
        [Parameter] public TransactionType Type { get; set; }

        private List<HistoryRecord> Records = new();

        //protected override void OnParametersSet()
        //{
        //    LoadData();
        //}

        //private void LoadData()
        //{
        //    // 🔹 这里先用 mock data，之后你再换成 API
        //    Records = Enumerable.Range(1, 3).Select(i => new HistoryRecord
        //    {
        //        Id = $"{Mode}-{Type}-{i}",
        //        Amount = Type switch
        //        {
        //            TransactionType.Credit => "RM 10.00",
        //            TransactionType.Voucher => "1 Voucher",
        //            TransactionType.Point => "100 pts",
        //            _ => "-"
        //        },
        //        Timestamp = DateTime.Now.AddMinutes(-i * 5).ToString("yyyy-MM-dd HH:mm"),
        //        Staff = "Staff_01"
        //    }).ToList();
        //}

        //private string Title => $"{ModeText} {TypeText} History";

        private string ModeText =>
            Mode == TransactionMode.Signup ? "Sign Up" : "Redeem";

        private string TypeText => Type.ToString();






        private List<dynamic> MockRecords = new List<dynamic> { new { Amount = "209" } };

        private string GetViewTitle() => View.ToString().Contains("Signup") ? "Sign Up" : "Redeem";

        private string GetUnitName() => View switch
        {
            HistoryView.SignupPoint or HistoryView.RedeemPoint => "Point",
            HistoryView.SignupVoucher or HistoryView.RedeemVoucher => "Voucher",
            _ => "Credit"
        };
    }



}
