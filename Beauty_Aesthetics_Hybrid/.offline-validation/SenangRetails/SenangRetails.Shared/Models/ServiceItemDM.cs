using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class ServiceItemDM
    {
        // 项目类型 (例如: "Credit", "Package", "Bundle", "Discount", "PayoutStanding", "Point")
        //过后要加一个就是based on category去layout(不要全部放在service的label下面)
        public string Category { get; set; } = "";

        public string PhotoUrl { get; set; } = "";

        public string ItemName { get; set; } = "-";

        public decimal Price { get; set; } = 0.00m;

        public int Quantity { get; set; } = 1;

        public decimal OutstandingAmount { get; set; } = 0.00m;

        //这里应该还有一个是for customer pay了多少钱， 不然哪里来的outstandingamount

        // 把 List<string> 改为 List<StaffDetails>
        public List<StaffDetailsDM> ServedByStaffs { get; set; } = new List<StaffDetailsDM>();

        public decimal Subtotal => Price * Quantity;
    }
}
