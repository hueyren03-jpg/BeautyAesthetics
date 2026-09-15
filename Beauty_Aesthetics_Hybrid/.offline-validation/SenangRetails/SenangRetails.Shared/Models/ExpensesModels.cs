using System;

namespace SenangRetails.Shared.Model
{
    public class ExpenditureItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Initialize all strings to empty to satisfy CS8618
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string Staff { get; set; } = string.Empty;
        public string InvoiceTitle { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Remark { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class StaffMember
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class SubCategoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}