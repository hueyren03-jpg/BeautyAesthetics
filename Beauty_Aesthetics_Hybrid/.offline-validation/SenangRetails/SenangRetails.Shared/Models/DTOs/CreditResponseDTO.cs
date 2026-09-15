using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CreditResponseDTO
    {
        public double PackageBalance { get; set; }
        public double CreditBalance { get; set; }
        public double PointBalance { get; set; }
        public double PointRebateBalance { get; set; }
        public int VoucherBalance { get; set; }
    }
}
