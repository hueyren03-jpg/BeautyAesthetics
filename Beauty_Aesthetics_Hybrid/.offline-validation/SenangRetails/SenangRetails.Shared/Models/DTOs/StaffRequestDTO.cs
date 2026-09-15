using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    public class StaffRequestDTO
    {
        [JsonPropertyName("isSelected")]
        public bool IsSelected { get; set; }

        [JsonPropertyName("isLoading")]
        public bool IsLoading { get; set; }

        [JsonPropertyName("masterAccountID")]
        public string MasterAccountId { get; set; }

        [JsonPropertyName("alphaCode")]
        public string AlphaCode { get; set; }

        [JsonPropertyName("numericCode")]
        public long NumericCode { get; set; }

        [JsonPropertyName("displayCode")]
        public string DisplayCode { get; set; }

        [JsonPropertyName("accountName")]
        public string AccountName { get; set; }

        [JsonPropertyName("accountTypeID")]
        public long AccountTypeId { get; set; }

        [JsonPropertyName("createdby")]
        public string Createdby { get; set; }

        [JsonPropertyName("createdDateTime")]
        public DateTimeOffset? CreatedDateTime { get; set; }

        [JsonPropertyName("modifiedby")]
        public string Modifiedby { get; set; }

        [JsonPropertyName("modifiedDateTime")]
        public DateTimeOffset? ModifiedDateTime { get; set; }

        [JsonPropertyName("birthdayYear")]
        public int? BirthdayYear { get; set; }

        [JsonPropertyName("birthdayMonth")]
        public int? BirthdayMonth { get; set; }

        [JsonPropertyName("birthdayDay")]
        public int? BirthdayDay { get; set; }

        [JsonPropertyName("gender")]
        public string Gender { get; set; }

        [JsonPropertyName("nric")]
        public string Nric { get; set; }

        [JsonPropertyName("salesPersonCode")]
        public string SalesPersonCode { get; set; }

        [JsonPropertyName("jobTitle")]
        public string JobTitle { get; set; }

        [JsonPropertyName("dateHired")]
        public DateTimeOffset? DateHired { get; set; }

        [JsonPropertyName("dateResigned")]
        public DateTimeOffset? DateResigned { get; set; }

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; }

        [JsonPropertyName("employeeTypeID")]
        public string EmployeeTypeId { get; set; }

        [JsonPropertyName("employeeTypeName")]
        public string EmployeeTypeName { get; set; }

        [JsonPropertyName("commissionAutoAllocationGroupID")]
        public string CommissionAutoAllocationGroupId { get; set; }

        [JsonPropertyName("commissionAutoAllocationGroupName")]
        public string CommissionAutoAllocationGroupName { get; set; }

        [JsonPropertyName("maxDiscountLimit")]
        public long MaxDiscountLimit { get; set; }

        [JsonPropertyName("workingShift")]
        public string WorkingShift { get; set; }

        [JsonPropertyName("workingShiftName")]
        public string WorkingShiftName { get; set; }

        [JsonPropertyName("monthlyPurchaseLimit")]
        public long MonthlyPurchaseLimit { get; set; }

        [JsonPropertyName("accountStatus")]
        public string AccountStatus { get; set; }

        [JsonPropertyName("updateTimeStamp")]
        public DateTimeOffset? UpdateTimeStamp { get; set; }

        [JsonPropertyName("branchID")]
        public string BranchId { get; set; }

        [JsonPropertyName("commissionSchemeID")]
        public string CommissionSchemeId { get; set; }

        [JsonPropertyName("commissionSchemeName")]
        public string CommissionSchemeName { get; set; }

        [JsonPropertyName("isNotSalesPerson")]
        public bool IsNotSalesPerson { get; set; }

        [JsonPropertyName("fingerPrint")]
        public string FingerPrint { get; set; }

        [JsonPropertyName("isSalesPerson")]
        public bool IsSalesPerson { get; set; }

        [JsonPropertyName("basicPay")]
        public long BasicPay { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("remarks")]
        public string Remarks { get; set; }

        [JsonPropertyName("deviceID")]
        public string DeviceId { get; set; }

        [JsonPropertyName("devicePlatform")]
        public string DevicePlatform { get; set; }

        [JsonPropertyName("deviceModel")]
        public string DeviceModel { get; set; }

        [JsonPropertyName("appFirstLoginDate")]
        public DateTimeOffset? AppFirstLoginDate { get; set; }

        [JsonPropertyName("saveAction")]
        public string SaveAction { get; set; }

        [JsonPropertyName("isDirty")]
        public bool IsDirty { get; set; }
    }
}