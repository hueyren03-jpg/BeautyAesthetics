namespace Beauty_Aesthetics_WebPos.Components.Models.Employee
{
    public class Employee
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SalesPersonCode { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public bool IsCashier { get; set; }
        public bool IsServiceStaff { get; set; }
        public string EmployeeLevel { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string MobilePhone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Nric { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public decimal MaxDiscountLimit { get; set; }
        public string WorkingShift { get; set; } = string.Empty;
        public decimal BasicPay { get; set; }
        public DateTime? DateHired { get; set; }
        public DateTime? DateResigned { get; set; }
        public string AutoAllocationGroup { get; set; } = string.Empty;
        public string CommissionScheme { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;

        // Branch Control
        public bool Available { get; set; }

        // Services Record
        public DateTime EffectiveDate { get; set; } = DateTime.Now;
        public decimal DailyBasic { get; set; }
        public string CommMethod { get; set; } = string.Empty;
        public decimal CommAmount { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;

        // Staff Purchase
        public decimal MaxPurchaseAmount { get; set; }
        public int AvailableDayOfMonth { get; set; }
        public int MaxBillCount { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public int MaxQtyPerItem { get; set; }

        // Complementary Credit
        public decimal ComplementaryCreditAmount { get; set; }
        public string ComplementaryCreditInterval { get; set; } = string.Empty;

        // --- Monthly Target Section ---
        public decimal? MonthlyTargetProduct { get; set; }
        public decimal? MonthlyTargetService { get; set; }
        public decimal? MonthlyTargetPackage { get; set; }
        public decimal? MonthlyTargetTopUp { get; set; }


        // --- Remark property ---
        public string Remark { get; set; } = string.Empty;
    }
}
