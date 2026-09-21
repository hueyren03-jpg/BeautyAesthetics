using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using System.Collections.Generic;
using System.Linq;

namespace Beauty_Aesthetics_WebPos.Components.Pages
{
    public partial class ReportMenu
    {
        private readonly NavigationManager nav;
        private readonly AppFeedbackService feedback;

        public ReportMenu(NavigationManager nav, AppFeedbackService feedback)
        {
            this.nav = nav;
            this.feedback = feedback;
        }

        // ─── SIDEBAR ─────────────────────────────────────────────────────
        private bool isCategoryBarOpen = false;          // starts COLLAPSED

        // ─── CATEGORY / SEARCH ───────────────────────────────────────────
        private string? SelectedCategory = null;         // null = Favourites home
        private List<ReportItem> DisplayedReports = new();
        private string SearchTerm = "";

        // ─── MODAL ───────────────────────────────────────────────────────
        private bool isSelectedReportParameterOpen = false;
        private bool isSelectedGroupParameterOpen = false;
        private ReportItem? SelectedReport = null;

        // ─── SPEED DIAL ──────────────────────────────────────────────────
        private bool isSpeedDialOpen = false;
        private bool isAddFavouriteSheetOpen = false;

        // ─── FAVOURITES ──────────────────────────────────────────────────
        private List<ReportItem> FavouriteReports = new();

        // ─── ADD-FAV SHEET ───────────────────────────────────────────────
        private string FavSheetSearchTerm = "";
        private HashSet<string> OpenSheetCategories = new();

        // ─── LIFECYCLE ───────────────────────────────────────────────────
        protected override void OnInitialized()
        {
            // All sheet categories start expanded
            foreach (var cat in AllReports.Select(r => r.Category).Distinct())
                OpenSheetCategories.Add(cat);
        }

        // ─── SIDEBAR ─────────────────────────────────────────────────────
        private void ToggleCategoryBarOpen() => isCategoryBarOpen = !isCategoryBarOpen;

        private void SelectCategory(string? category)
        {
            SelectedCategory = category;
            SearchTerm = "";
            if (category != null) UpdateReportList();
        }

        // ─── REPORT LIST ─────────────────────────────────────────────────
        private void OnSearchInput(ChangeEventArgs e)
        {
            SearchTerm = e.Value?.ToString() ?? "";
            UpdateReportList();
        }

        private void UpdateReportList()
        {
            if (SelectedCategory == null) return;
            var src = AllReports.Where(r => r.Category == SelectedCategory);
            DisplayedReports = string.IsNullOrWhiteSpace(SearchTerm)
                ? src.ToList()
                : src.Where(r => r.Title.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // ─── PARAMETER MODAL ─────────────────────────────────────────────
        private void OpenReportParameter(ReportItem report)
        {
            SelectedReport = report;
            isSelectedReportParameterOpen = true;
            feedback.Info($"Configure parameters for {report.Title}.", "Report selected", 2200);
        }

        private void CloseReportParameter()
        {
            isSelectedReportParameterOpen = false;
        }

        // Keep for backward compatibility with existing modals
        private void ToggleReportParameter() => isSelectedReportParameterOpen = !isSelectedReportParameterOpen;
        private void ToggleBranchParameter() => isSelectedGroupParameterOpen = !isSelectedGroupParameterOpen;
        private void NavigateToMonthlyReport()
        {
            feedback.Info(
                SelectedReport is null ? "Generating report view." : $"Generating {SelectedReport.Title}.",
                "Generate report",
                2200);
            nav.NavigateTo("/Report");
        }

        // ─── SPEED DIAL ──────────────────────────────────────────────────
        private void ToggleSpeedDial() => isSpeedDialOpen = !isSpeedDialOpen;
        private void CloseSpeedDial() => isSpeedDialOpen = false;

        private void OpenAddFavouriteSheet()
        {
            isAddFavouriteSheetOpen = true;
            isSpeedDialOpen = false;
        }

        private void CloseAddFavouriteSheet() => isAddFavouriteSheetOpen = false;

        private void OpenCategorySidebar()
        {
            isCategoryBarOpen = true;
            isSpeedDialOpen = false;
        }

        // ─── FAVOURITES ──────────────────────────────────────────────────
        private bool IsFavourite(ReportItem report) =>
            FavouriteReports.Any(f => f.Title == report.Title && f.Category == report.Category);

        private void ToggleFavourite(ReportItem report)
        {
            if (IsFavourite(report))
            {
                FavouriteReports.RemoveAll(f => f.Title == report.Title && f.Category == report.Category);
                feedback.Info($"{report.Title} removed from favourites.", "Favourite removed", 2200);
            }
            else
            {
                FavouriteReports.Add(report);
                feedback.Success($"{report.Title} added to favourites.", "Favourite added", 2600);
            }
        }

        private void RemoveFromFavourites(ReportItem report)
        {
            FavouriteReports.RemoveAll(f => f.Title == report.Title && f.Category == report.Category);
            feedback.Info($"{report.Title} removed from favourites.", "Favourite removed", 2200);
        }

        // ─── SHEET HELPERS ────────────────────────────────────────────────
        private void OnFavSheetSearch(ChangeEventArgs e) =>
            FavSheetSearchTerm = e.Value?.ToString() ?? "";

        private void ToggleSheetCategory(string cat)
        {
            if (OpenSheetCategories.Contains(cat)) OpenSheetCategories.Remove(cat);
            else OpenSheetCategories.Add(cat);
        }

        private bool IsSheetCategoryOpen(string cat) => OpenSheetCategories.Contains(cat);

        private List<ReportItem> GetFilteredSheetReports(string cat)
        {
            var items = AllReports.Where(r => r.Category == cat);
            if (!string.IsNullOrWhiteSpace(FavSheetSearchTerm))
                items = items.Where(r => r.Title.Contains(FavSheetSearchTerm, StringComparison.OrdinalIgnoreCase));
            return items.ToList();
        }

        private IEnumerable<string> GetFilteredSheetCategories()
        {
            if (string.IsNullOrWhiteSpace(FavSheetSearchTerm))
                return AllReports.Select(r => r.Category).Distinct();
            return AllReports
                .Where(r => r.Title.Contains(FavSheetSearchTerm, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Category)
                .Distinct();
        }

        // ─── DATA STRUCTURE ──────────────────────────────────────────────
        public class ReportItem
        {
            public string Title { get; set; }
            public string Category { get; set; }
            public string IconClass { get; set; } = "fa-solid fa-file";
        }

        // ─── DATA LIST ───────────────────────────────────────────────────
        private List<ReportItem> AllReports = new List<ReportItem>
        {
            // --- STOCK ---
            new ReportItem { Title = "Stock Balance",                   Category = "Stock" },
            new ReportItem { Title = "Stock Balance - Batch Item",      Category = "Stock" },
            new ReportItem { Title = "Stock Movement",                  Category = "Stock" },
            new ReportItem { Title = "Stock Movement (GRD)",            Category = "Stock" },
            new ReportItem { Title = "Stock Balance with Cost",         Category = "Stock" },
            new ReportItem { Title = "Stock Below Reorder Point",       Category = "Stock" },
            new ReportItem { Title = "Stock Movement Summary",          Category = "Stock" },
            new ReportItem { Title = "Stock Turnover Analysis",         Category = "Stock" },

            // --- SALES ---
            new ReportItem { Title = "Debtor Aging - Detailed",                    Category = "Sales" },
            new ReportItem { Title = "Debtor Aging - Summary",                     Category = "Sales" },
            new ReportItem { Title = "Debtor Statement",                           Category = "Sales" },
            new ReportItem { Title = "Debtor Knock-Off Detail-By Bill",            Category = "Sales" },
            new ReportItem { Title = "Transaction Listing - Sales Order",          Category = "Sales" },
            new ReportItem { Title = "Customer Payment - SST Collection",          Category = "Sales" },

            // --- STAFF ---
            new ReportItem { Title = "Commission Allocation Detail",               Category = "Staff" },
            new ReportItem { Title = "Commission Allocation Detail - By Collection", Category = "Staff" },
            new ReportItem { Title = "Time Attendance",                            Category = "Staff" },
            new ReportItem { Title = "Daily Commission Summary",                   Category = "Staff" },

            // --- PURCHASES ---
            new ReportItem { Title = "Creditor Aging - Detailed",                  Category = "Purchases" },
            new ReportItem { Title = "Creditor Aging - Summary",                   Category = "Purchases" },
            new ReportItem { Title = "Creditor Knock-Off Detail-By Bill",          Category = "Purchases" },
            new ReportItem { Title = "Transaction Listing - Cash Purchase",        Category = "Purchases" },

            // --- TAX ---
            new ReportItem { Title = "Tax Detail",  Category = "Tax" },
            new ReportItem { Title = "Tax Summary", Category = "Tax" },

            // --- POS ---
            new ReportItem { Title = "End of Day Till Balance - Thermal By Counter", Category = "POS" },
            new ReportItem { Title = "Daily Summary",                              Category = "POS" },
            new ReportItem { Title = "Monthly Summary",                            Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Brand",                   Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Customer Group",          Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Group",                   Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Item",                    Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Employee",                Category = "POS" },
            new ReportItem { Title = "Month To Date Sales Summary",                Category = "POS" },
            new ReportItem { Title = "Inter-Branch Charges",                       Category = "POS" },
            new ReportItem { Title = "Sales Material Cost Summary",                Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Item with Profit",        Category = "POS" },
            new ReportItem { Title = "Sales By Employee and Type",                 Category = "POS" },
            new ReportItem { Title = "Member Credit Movement Summary",             Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Item with Bundle",        Category = "POS" },
            new ReportItem { Title = "Transaction Listing - Cash Sales",           Category = "POS" },
            new ReportItem { Title = "Sales Summary - By Supplier",                Category = "POS" },
            new ReportItem { Title = "End of Day - Z Report",                      Category = "POS" },
            new ReportItem { Title = "Profit Summary - By Day",                    Category = "POS" },
            new ReportItem { Title = "Profit Summary - By Group",                  Category = "POS" },
            new ReportItem { Title = "Sales Summary - Top Sales",                  Category = "POS" },
            new ReportItem { Title = "Revenue Report",                             Category = "POS" },
            new ReportItem { Title = "Daily Sales Performance",                    Category = "POS" },
            new ReportItem { Title = "Monthly Sales Performance",                  Category = "POS" },
            new ReportItem { Title = "Sales By Item Group",                        Category = "POS" },
            new ReportItem { Title = "Package Balance",                            Category = "POS" },
            new ReportItem { Title = "Member Credit Balance",                      Category = "POS" },
            new ReportItem { Title = "Member Credit Movement",                     Category = "POS" },
            new ReportItem { Title = "Member Package Movement",                    Category = "POS" },
            new ReportItem { Title = "Customer Return Rate",                       Category = "POS" },
            new ReportItem { Title = "Daily Commission Summary 2",                 Category = "POS" },
            new ReportItem { Title = "Customer Deposit Movement",                  Category = "POS" },
            new ReportItem { Title = "Customer Last Visit Record",                 Category = "POS" },
            new ReportItem { Title = "Point Balance",                              Category = "POS" },
            new ReportItem { Title = "Member Time Balance",                        Category = "POS" },
            new ReportItem { Title = "Member Time Movement",                       Category = "POS" },
            new ReportItem { Title = "Member Point Movement",                      Category = "POS" },
        };
    }
}