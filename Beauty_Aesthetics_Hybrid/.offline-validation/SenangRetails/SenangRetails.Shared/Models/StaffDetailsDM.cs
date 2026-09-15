using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class StaffDetailsDM
    {
        public string StaffId { get; set; }
        public string Name { get; set; }                 
        public string PhoneNumber { get; set; }
        public decimal EffortPercentage { get; set; }
        public decimal Value { get; set; }
        public decimal HofPercentage { get; set; }
        public string PhotoUrl { get; set; }
        //public StaffDetailsDM(string name, string phoneNumber, decimal effortPercentage, decimal value, decimal hofPercentage)
        //{
        //    Name = name;
        //    PhoneNumber = phoneNumber;
        //    EffortPercentage = effortPercentage;
        //    Value = value;
        //    HofPercentage = hofPercentage;
        //}
    }
}
