using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Entities
{
    public class Customer
    {
        public bool IsLoading { get; set; }
        public string MasterAccountID { get; set; } = string.Empty;
        //public string AlphaCode { get; set; } = string.Empty;
        //public int NumericCode { get; set; }
        //public string DisplayCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        //public int AccountTypeID { get; set; }
        //public decimal OpeningBalanceDebit { get; set; }
        //public decimal OpeningBalanceCredit { get; set; }
        //public string Createdby { get; set; } = string.Empty;
        public DateTime CreatedDateTime { get; set; }
        //public string Modifiedby { get; set; } = string.Empty;
        public DateTime ModifiedDateTime { get; set; }
        //public decimal CreditLimit { get; set; }
        //public string CustomerGroupID { get; set; } = string.Empty;
        //public string CustomerGroupName { get; set; } = string.Empty;
        //public string SalesPersonID { get; set; } = string.Empty;
        //public string SalesPersonName { get; set; } = string.Empty;
        //public string ShippingMethodID { get; set; } = string.Empty;
        //public string ShippingMethodName { get; set; } = string.Empty;
        //public string PaymentTermID { get; set; } = string.Empty;
        //public string PaymentTermName { get; set; } = string.Empty;
        public int? BirthdayYear { get; set; }
        public int? BirthdayMonth { get; set; }
        public int? BirthdayDay { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string NRIC { get; set; } = string.Empty;
        //public bool Prestigon { get; set; }
        //public string Refreshment { get; set; } = string.Empty;
        //public string PreferredEmployee { get; set; } = string.Empty;
        //public string EmployeeID { get; set; } = string.Empty;
        //public string MembershipID { get; set; } = string.Empty;
        //public DateTime MembershipSince { get; set; }
        //public string MembershipTypeID { get; set; } = string.Empty;
        //public DateTime MembershipValidFrom { get; set; }
        //public DateTime MembershipValidTo { get; set; }
        //public string BranchID { get; set; } = string.Empty;
        //public bool IsOutlet { get; set; }
        //public string OutletID { get; set; } = string.Empty;
        //public string Referer { get; set; } = string.Empty;
        //public string RaceID { get; set; } = string.Empty;
        public string RaceName { get; set; } = string.Empty;
        //public string LanguageID { get; set; } = string.Empty;
        //public string LanguageName { get; set; } = string.Empty;
        //public string MemberPassword { get; set; } = string.Empty;
        //public bool BlockCustomerHistory { get; set; }
        //public string MemberCardSerialNo { get; set; } = string.Empty;
        public string Address1 { get; set; } = string.Empty;
        public string Address2 { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CountryState { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        //public string Contact { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        //public string Fax { get; set; } = string.Empty;
        //public DateTime AccountSince { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        //public string Alert { get; set; } = string.Empty;
        //public string BRN { get; set; } = string.Empty;
        //public DateTime DateGSTStatusVerified { get; set; }
        //public string GSTNo { get; set; } = string.Empty;
        //public string TaxGroupID { get; set; } = string.Empty;
        //public string TaxCodeID { get; set; } = string.Empty;
        //public string PriceGroupID { get; set; } = string.Empty;
        //public bool BlockOutletAccess { get; set; }
        //public string CustomerBank { get; set; } = string.Empty;
        //public string BankAccountNumber { get; set; } = string.Empty;
        //public DateTime UpdateTimeStamp { get; set; }
        //public string CurrencyID { get; set; } = string.Empty;
        //public string CurrencyName { get; set; } = string.Empty;
        //public string FinancialAccountID { get; set; } = string.Empty;
        //public string FinancialAccountName { get; set; } = string.Empty;
        public string MembershipTypeName { get; set; } = string.Empty;
        public string VisibleToBranch { get; set; } = string.Empty;
        //public string CustomerSourceID { get; set; } = string.Empty;
        public string CustomerSourceName { get; set; } = string.Empty;
        //public string PreferredContactMethod { get; set; } = string.Empty;
        //public string LocalCode { get; set; } = string.Empty;
        //public string ImagePath { get; set; } = string.Empty;
        //public string ImageFileName { get; set; } = string.Empty;
        //public string TIN { get; set; } = string.Empty;
        //public string CountryStateCode { get; set; } = string.Empty;
        //public string CountryCode { get; set; } = string.Empty;
        //public string NRICType { get; set; } = string.Empty;
        //public string PeppolID { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        //public string ExperiencedServiceBrand { get; set; } = string.Empty;
        //public string ExperiencedProductBrand { get; set; } = string.Empty;
        //public string HomeProductUsed { get; set; } = string.Empty;
        //public bool IsAllergy { get; set; }
        //public string CurrentCondition { get; set; } = string.Empty;
        //public int SaveAction { get; set; }
        //public bool IsDirty { get; set; }
    }

    public class CustomerCreateSuccess
    {
        public string Id { get; set; } = string.Empty;
        public string? DisplayCode { get; set; }
        public string SuccessMessage { get; set; } = string.Empty;
    }
    public class CustomerUpdateDTO
    {
        public string MasterAccountID { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string NRIC { get; set; } = string.Empty;
        public int BirthdayYear { get; set; }
        public int BirthdayMonth { get; set; }
        public int BirthdayDay { get; set; }
        public string RaceName { get; set; } = string.Empty;
        public string CustomerSourceName { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string MembershipTypeName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Address1 { get; set; } = string.Empty;
        public string Address2 { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CountryState { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;

        public string SaveAction { get; set; } = "Changed"; // Default value
        public bool IsLoading { get; set; } = false;
    }
}
