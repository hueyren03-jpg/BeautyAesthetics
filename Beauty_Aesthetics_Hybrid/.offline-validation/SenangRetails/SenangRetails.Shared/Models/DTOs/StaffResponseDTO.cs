using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models.DTOs
{
    public class StaffResponseDTO
    {
        public bool IsSelected { get; set; }
        public bool IsLoading { get; set; }
        public string MasterAccountID { get; set; }
        public string AlphaCode { get; set; }
        public int NumericCode { get; set; }
        public string DisplayCode { get; set; }
        public string AccountName { get; set; }
        public int AccountTypeID { get; set; }
        public string Createdby { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string Modifiedby { get; set; }
        public DateTime ModifiedDateTime { get; set; }
        public int BirthdayYear { get; set; }
        public int BirthdayMonth { get; set; }
        public int BirthdayDay { get; set; }
        public string Gender { get; set; }
        public object NRIC { get; set; }
        public string SalesPersonCode { get; set; }
        public string JobTitle { get; set; }
        public DateTime DateHired { get; set; }
        public DateTime DateResigned { get; set; }
        public string ImagePath { get; set; }
        public string EmployeeTypeID { get; set; }
        public string EmployeeTypeName { get; set; }
        public string CommissionAutoAllocationGroupID { get; set; }
        public string CommissionAutoAllocationGroupName { get; set; }
        public double MaxDiscountLimit { get; set; }
        public string WorkingShift { get; set; }
        public string WorkingShiftName { get; set; }
        public double MonthlyPurchaseLimit { get; set; }
        public string AccountStatus { get; set; }
        public DateTime UpdateTimeStamp { get; set; }
        public string BranchID { get; set; }
        public string CommissionSchemeID { get; set; }
        public string CommissionSchemeName { get; set; }
        public bool IsNotSalesPerson { get; set; }
        public string FingerPrint { get; set; }
        public bool IsSalesPerson { get; set; }
        public double BasicPay { get; set; }
        public string Phone { get; set; }
        public object Remarks { get; set; }
        public object DeviceID { get; set; }
        public object DevicePlatform { get; set; }
        public object DeviceModel { get; set; }
        public DateTime AppFirstLoginDate { get; set; }
        public int SaveAction { get; set; }
        public bool IsDirty { get; set; }
        public string DefaultImagePath { get; set; }
        public string EmployeeDisplayCodeAndEmployeeTypeName { get; set; }
    }
}
