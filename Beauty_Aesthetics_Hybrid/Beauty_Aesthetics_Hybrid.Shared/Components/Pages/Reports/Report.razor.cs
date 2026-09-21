using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages
{
    public partial class Report
    {
        private bool SetupModalIsOpen = false;
        private string SearchTerm = "";

        // Data Models
        public class ReportItem
        {
            public string Name { get; set; }
            public string IconPath { get; set; } = "icons/default-report.png"; // Default placeholder
        }

        public class ReportCategory
        {
            public string Name { get; set; }
            public List<ReportItem> Reports { get; set; } = new();
        }

        private List<ReportCategory> AllCategories = new();

        private void OpenSetupModal()
        {
            SetupModalIsOpen = !SetupModalIsOpen;
        }

        protected override void OnInitialized()
        {
            // 1. Stock Reports
            AllCategories.Add(new ReportCategory
            {
                Name = "Stock",
                Reports = new List<ReportItem>
                {
                    new() { Name = "Stock Balance" },
                    new() { Name = "Stock Balance - Batch Item" },
                    new() { Name = "Stock Movement" },
                    new() { Name = "Stock Movement (GRD)" },
                    new() { Name = "Stock Balance with Cost" },
                    new() { Name = "Stock Below Reorder Point" },
                    new() { Name = "Stock Movement Summary" },
                    new() { Name = "Stock Turnover Analysis" }
                }
            });

            // 2. Sales Reports
            AllCategories.Add(new ReportCategory
            {
                Name = "Sales",
                Reports = new List<ReportItem>
                {
                    new() { Name = "Debtor Aging - Detailed" },
                    new() { Name = "Debtor Aging - Summary" },
                    new() { Name = "Debtor Statement" },
                    new() { Name = "Debtor Knock-Off Detail-By Bill" },
                    new() { Name = "Transaction Listing - Sales Order" },
                    new() { Name = "Customer Payment - SST Collection" }
                }
            });

            // 3. Purchase Reports
            AllCategories.Add(new ReportCategory
            {
                Name = "Purchases",
                Reports = new List<ReportItem>
                {
                    new() { Name = "Creditor Aging - Detailed" },
                    new() { Name = "Creditor Aging - Summary" },
                    new() { Name = "Creditor Knock-Off Detail-By Bill" },
                    new() { Name = "Transaction Listing - Cash Purchase" }
                }
            });

            // 4. Point of Sales
            AllCategories.Add(new ReportCategory
            {
                Name = "Point of Sales",
                Reports = new List<ReportItem>
                {
                    new() { Name = "End of Day Till Balance" },
                    new() { Name = "Daily Summary" },
                    new() { Name = "Monthly Summary" },
                    new() { Name = "Sales Summary - By Brand" },
                    new() { Name = "Sales Summary - By Customer Group" },
                    new() { Name = "Sales Summary - By Group" },
                    new() { Name = "Sales Summary - By Item" },
                    new() { Name = "Sales Summary - By Employee" },
                    new() { Name = "Month To Date Sales Summary" },
                    new() { Name = "Inter-Branch Charges" },
                    new() { Name = "Sales Material Cost Summary" },
                    new() { Name = "Sales Summary - By Item with Profit" },
                    new() { Name = "Sales By Employee and Type" },
                    new() { Name = "Member Credit Movement Summary" },
                    new() { Name = "Sales Summary - By Item with Bundle" },
                    new() { Name = "Transaction Listing - Cash Sales" },
                    new() { Name = "Sales Summary - By Supplier" },
                    new() { Name = "End of Day - Z Report" },
                    new() { Name = "Profit Summary - By Day" },
                    new() { Name = "Profit Summary - By Group" },
                    new() { Name = "Sales Summary - Top Sales" },
                    new() { Name = "Revenue Report" },
                    new() { Name = "Daily Sales Performance" },
                    new() { Name = "Monthly Sales Performance" },
                    new() { Name = "Sales By Item Group" },
                    new() { Name = "Package Balance" },
                    new() { Name = "Member Credit Balance" },
                    new() { Name = "Member Credit Movement" },
                    new() { Name = "Member Package Movement" },
                    new() { Name = "Customer Return Rate" },
                    new() { Name = "Daily Commission Summary 2" },
                    new() { Name = "Customer Deposit Movement" },
                    new() { Name = "Customer Last Visit Record" },
                    new() { Name = "Point Balance" },
                    new() { Name = "Member Time Balance" },
                    new() { Name = "Member Time Movement" },
                    new() { Name = "Member Point Movement" }
                }
            });

            // 5. Staff Reports
            AllCategories.Add(new ReportCategory
            {
                Name = "Staff",
                Reports = new List<ReportItem>
                {
                    new() { Name = "Commission Allocation Detail" },
                    new() { Name = "Commission Allocation Detail - By Collection" },
                    new() { Name = "Time Attendance" },
                    new() { Name = "Daily Commission Summary" }
                }
            });

            // 6. Tax Reports
            AllCategories.Add(new ReportCategory
            {
                Name = "Tax",
                Reports = new List<ReportItem>
                {
                    new() { Name = "Tax Detail" },
                    new() { Name = "Tax Summary" }
                }
            });
        }

        // Logic to filter reports based on Search Bar
        private IEnumerable<ReportCategory> FilteredCategories =>
            string.IsNullOrWhiteSpace(SearchTerm)
            ? AllCategories
            : AllCategories
                .Select(c => new ReportCategory
                {
                    Name = c.Name,
                    Reports = c.Reports.Where(r => r.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)).ToList()
                })
                .Where(c => c.Reports.Any());

        private string GetCategoryTranslationKey(string categoryName)
        {
            return categoryName switch
            {
                "Point of Sales" => "POS",
                _ => categoryName
            };
        }
    }
}