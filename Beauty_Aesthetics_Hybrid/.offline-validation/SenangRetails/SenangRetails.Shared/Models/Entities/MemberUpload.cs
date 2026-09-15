using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration;
using System.Globalization;

namespace SenangRetails.Shared.Models.Entities
{
    public class MemberUploadMap : ClassMap<MemberUploadModel>
    {
        public MemberUploadMap()
        {
            Map(m => m.AccountName).Name("Name");
            Map(m => m.Phone).Name("Contact");
            Map(m => m.BirthdayYear).Name("Birthday Year");
            Map(m => m.BirthdayMonth).Name("Birthday Month");
            Map(m => m.BirthdayDay).Name("Birthday Day");
            Map(m => m.Gender).Name("Gender");
            Map(m => m.Email).Name("Email");
            Map(m => m.NRIC).Name("NRIC");
            Map(m => m.RaceName).Name("Ethnicity");
            Map(m => m.MaritalStatus).Name("Marital Status");
            Map(m => m.CustomerSourceName).Name("Source");
            Map(m => m.MembershipTypeName).Name("Member Tier");
            Map(m => m.Comment).Name("Remarks");
            Map(m => m.Address1).Name("Address 1");
            Map(m => m.Address2).Name("Address 2");
            Map(m => m.ZipCode).Name("Postal Code");
            Map(m => m.City).Name("City");
            Map(m => m.CountryState).Name("State");
            Map(m => m.Country).Name("Country");
        }
    }

    public class MemberUploadModel
    {
        public string AccountName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int? BirthdayYear { get; set; }
        public int? BirthdayMonth { get; set; }
        public int? BirthdayDay { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NRIC { get; set; } = string.Empty;
        public string RaceName { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string CustomerSourceName { get; set; } = string.Empty;
        public string MembershipTypeName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Address1 { get; set; } = string.Empty;
        public string Address2 { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CountryState { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
