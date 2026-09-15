using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.FileDownloadService;

namespace SenangRetails.Shared.Components.Reports
{
    public partial class StockMovementSummaryReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Branch selection state
        private bool showBranchModal = false;
        private string selectedBranchDisplay = string.Empty;
        private List<string> selectedBranchIDs = new();
        private List<string> stagedBranchIDs = new();
        private string _branchSearchTerm = string.Empty;
        private List<string> masterBranchList = new();
        private List<string> filteredBranchList = new();
        private bool selectAllBranches = false;

        // Data
        private List<StockMovementSummary> movementData = new();
        private List<StockMovementSummary> filteredMovementData = new();

        // Filter properties
        private string branchIdFilter = string.Empty;
        private string itemNameFilter = string.Empty;
        private string groupNameFilter = string.Empty;
        private string itemCodeFilter = string.Empty;
        private string uomFilter = string.Empty;
        private string openingQtyFilter = string.Empty;
        private string purchaseQtyFilter = string.Empty;
        private string purchaseReturnFilter = string.Empty;
        private string salesQtyFilter = string.Empty;
        private string salesReturnFilter = string.Empty;
        private string conversionInFilter = string.Empty;
        private string conversionOutFilter = string.Empty;
        private string transferredInFilter = string.Empty;
        private string transferredOutFilter = string.Empty;
        private string adjustmentFilter = string.Empty;
        private string closingQtyFilter = string.Empty;
        private string unitCostFilter = string.Empty;
        private string closingValueFilter = string.Empty;

        // Summary totals
        private int totalItems = 0;
        private decimal totalOpeningQty = 0;
        private decimal totalPurchasesQty = 0;
        private decimal totalSalesQty = 0;
        private decimal totalClosingQty = 0;
        private decimal totalClosingValue = 0;

        static StockMovementSummaryReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            if (!CanSelectBranch())
            {
                return AppState.SelectedBranchID ?? "HQ";
            }

            if (selectAllBranches || selectedBranchIDs.Count == 0)
                return "All Branches";
            if (selectedBranchIDs.Count == 1)
                return selectedBranchDisplay;
            return $"{selectedBranchIDs.Count} Branches";
        }

        private string GetBranchIDsForAPI()
        {
            // If not in HQ mode, return the specific branch only
            if (!CanSelectBranch())
            {
                return AppState.SelectedBranchID ?? "HQ";
            }

            // If all branches are selected OR no branches are selected, return ALL branch IDs with commas
            if (selectAllBranches || selectedBranchIDs.Count == 0)
            {
                // Get all branch IDs from the master list or available branches
                if (AppState.AvailableBranches != null && AppState.AvailableBranches.Any())
                {
                    return string.Join(",", AppState.AvailableBranches.Select(b => b.ToString()));
                }
                else if (masterBranchList.Any())
                {
                    return string.Join(",", masterBranchList);
                }
                else
                {
                    return "HQ"; // Fallback
                }
            }

            return string.Join(",", selectedBranchIDs);
        }

        private bool CanSelectBranch()
        {
            return AppState.SelectedBranchID == "HQ";
        }

        // Branch Selection Methods
        private string BranchSearchTerm
        {
            get => _branchSearchTerm;
            set
            {
                if (_branchSearchTerm != value)
                {
                    _branchSearchTerm = value;
                    FilterBranchModalList();
                }
            }
        }

        private void OpenBranchSelectionModal()
        {
            _branchSearchTerm = string.Empty;

            if (AppState.AvailableBranches != null && AppState.AvailableBranches.Any())
            {
                masterBranchList = AppState.AvailableBranches.Select(b => b.ToString()).ToList();
            }
            else
            {
                if (selectedBranchIDs.Any())
                {
                    masterBranchList = new List<string>(selectedBranchIDs);
                }
                else
                {
                    masterBranchList = new List<string> { "HQ" };
                }
            }

            filteredBranchList = new List<string>(masterBranchList);
            stagedBranchIDs = new List<string>(selectedBranchIDs);
            selectAllBranches = selectedBranchIDs.Count == 0 || selectedBranchIDs.Count == masterBranchList.Count;

            showBranchModal = true;
            StateHasChanged();
        }

        private void CloseBranchModal()
        {
            showBranchModal = false;
            stagedBranchIDs.Clear();
            _branchSearchTerm = string.Empty;
        }

        private void FilterBranchModalList()
        {
            if (string.IsNullOrWhiteSpace(_branchSearchTerm))
            {
                filteredBranchList = new List<string>(masterBranchList);
            }
            else
            {
                filteredBranchList = masterBranchList
                    .Where(b => b.Contains(_branchSearchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            StateHasChanged();
        }

        private void ToggleBranchSelection(string branchName)
        {
            if (stagedBranchIDs.Contains(branchName))
            {
                stagedBranchIDs.Remove(branchName);
            }
            else
            {
                stagedBranchIDs.Add(branchName);
            }

            if (stagedBranchIDs.Count == masterBranchList.Count && masterBranchList.Count > 0)
            {
                selectAllBranches = true;
            }
            else
            {
                selectAllBranches = false;
            }

            StateHasChanged();
        }

        private void ToggleSelectAllBranches()
        {
            selectAllBranches = !selectAllBranches;

            if (selectAllBranches)
            {
                stagedBranchIDs = new List<string>(masterBranchList);
            }
            else
            {
                stagedBranchIDs.Clear();
            }

            StateHasChanged();
        }

        private bool IsBranchSelected(string branchName)
        {
            if (selectAllBranches)
                return true;
            return stagedBranchIDs.Contains(branchName);
        }

        private async Task ConfirmBranchSelection()
        {
            if (selectAllBranches)
            {
                // Get all branch IDs from the master list
                if (masterBranchList.Any())
                {
                    selectedBranchIDs = new List<string>(masterBranchList);
                }
                else if (AppState.AvailableBranches != null)
                {
                    selectedBranchIDs = AppState.AvailableBranches.Select(b => b.ToString()).ToList();
                }
                else
                {
                    selectedBranchIDs = new List<string> { "HQ" };
                }
                selectedBranchDisplay = "All Branches";
            }
            else
            {
                selectedBranchIDs = new List<string>(stagedBranchIDs);
                if (selectedBranchIDs.Count == 1)
                {
                    selectedBranchDisplay = selectedBranchIDs.First();
                }
                else if (selectedBranchIDs.Count > 1)
                {
                    selectedBranchDisplay = $"{selectedBranchIDs.Count} Branches";
                }
                else
                {
                    selectedBranchDisplay = "No Branch Selected";
                }
            }

            showBranchModal = false;
            _branchSearchTerm = string.Empty;
            stagedBranchIDs.Clear();

            // Refresh data with new branch selection
            await LoadDataAsync();
            StateHasChanged();
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private void ApplyFilters()
        {
            if (movementData == null || !movementData.Any())
            {
                filteredMovementData = new List<StockMovementSummary>();
                return;
            }

            var query = movementData.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(branchIdFilter))
            {
                query = query.Where(x => x.BranchID != null &&
                    x.BranchID.Contains(branchIdFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(itemNameFilter))
            {
                query = query.Where(x => x.ItemName != null &&
                    x.ItemName.Contains(itemNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(groupNameFilter))
            {
                query = query.Where(x => x.GroupName != null &&
                    x.GroupName.Contains(groupNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(itemCodeFilter))
            {
                query = query.Where(x => x.ItemCode != null &&
                    x.ItemCode.Contains(itemCodeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(uomFilter))
            {
                query = query.Where(x => x.UOM != null &&
                    x.UOM.Contains(uomFilter, StringComparison.OrdinalIgnoreCase));
            }

            query = ApplyNumericFilter(query, x => x.QtyOpening, openingQtyFilter);
            query = ApplyNumericFilter(query, x => x.QtyPurchase, purchaseQtyFilter);
            query = ApplyNumericFilter(query, x => x.QtyPurchaseReturn, purchaseReturnFilter);
            query = ApplyNumericFilter(query, x => x.QtySales, salesQtyFilter);
            query = ApplyNumericFilter(query, x => x.QtySalesReturn, salesReturnFilter);
            query = ApplyNumericFilter(query, x => x.QtyConversionIn, conversionInFilter);
            query = ApplyNumericFilter(query, x => x.QtyConversionOut, conversionOutFilter);
            query = ApplyNumericFilter(query, x => x.QtyTransferredIn, transferredInFilter);
            query = ApplyNumericFilter(query, x => x.QtyTransferredOut, transferredOutFilter);
            query = ApplyNumericFilter(query, x => x.QtyAdjustment, adjustmentFilter);
            query = ApplyNumericFilter(query, x => x.QtyClosing, closingQtyFilter);
            query = ApplyNumericFilter(query, x => x.UnitCost, unitCostFilter);
            query = ApplyNumericFilter(query, x => x.ClosingCostValue, closingValueFilter);

            filteredMovementData = query.ToList();
            StateHasChanged();
        }

        private IEnumerable<StockMovementSummary> ApplyNumericFilter(
            IEnumerable<StockMovementSummary> query,
            Func<StockMovementSummary, decimal> selector,
            string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return query;

            var conditions = ParseNumericFilter(filterText);
            if (conditions.HasValue)
            {
                if (conditions.Value.min.HasValue)
                    query = query.Where(x => selector(x) >= conditions.Value.min.Value);
                if (conditions.Value.max.HasValue)
                    query = query.Where(x => selector(x) <= conditions.Value.max.Value);
            }
            return query;
        }

        private (decimal? min, decimal? max)? ParseNumericFilter(string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return null;

            if (filterText.Contains('-'))
            {
                var parts = filterText.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    if (decimal.TryParse(parts[0].Trim(), out decimal min) &&
                        decimal.TryParse(parts[1].Trim(), out decimal max))
                    {
                        return (min, max);
                    }
                }
            }
            else if (filterText.Contains('>') || filterText.Contains('+'))
            {
                var valuePart = filterText.Replace(">", "").Replace("+", "").Trim();
                if (decimal.TryParse(valuePart, out decimal min))
                {
                    return (min, null);
                }
            }
            else if (filterText.Contains('<'))
            {
                var valuePart = filterText.Replace("<", "").Trim();
                if (decimal.TryParse(valuePart, out decimal max))
                {
                    return (null, max);
                }
            }
            else if (decimal.TryParse(filterText, out decimal exactValue))
            {
                return (exactValue, exactValue);
            }

            return null;
        }

        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(branchIdFilter) ||
                   !string.IsNullOrWhiteSpace(itemNameFilter) ||
                   !string.IsNullOrWhiteSpace(groupNameFilter) ||
                   !string.IsNullOrWhiteSpace(itemCodeFilter) ||
                   !string.IsNullOrWhiteSpace(uomFilter) ||
                   !string.IsNullOrWhiteSpace(openingQtyFilter) ||
                   !string.IsNullOrWhiteSpace(purchaseQtyFilter) ||
                   !string.IsNullOrWhiteSpace(purchaseReturnFilter) ||
                   !string.IsNullOrWhiteSpace(salesQtyFilter) ||
                   !string.IsNullOrWhiteSpace(salesReturnFilter) ||
                   !string.IsNullOrWhiteSpace(conversionInFilter) ||
                   !string.IsNullOrWhiteSpace(conversionOutFilter) ||
                   !string.IsNullOrWhiteSpace(transferredInFilter) ||
                   !string.IsNullOrWhiteSpace(transferredOutFilter) ||
                   !string.IsNullOrWhiteSpace(adjustmentFilter) ||
                   !string.IsNullOrWhiteSpace(closingQtyFilter) ||
                   !string.IsNullOrWhiteSpace(unitCostFilter) ||
                   !string.IsNullOrWhiteSpace(closingValueFilter);
        }

        private void ClearFilters()
        {
            branchIdFilter = string.Empty;
            itemNameFilter = string.Empty;
            groupNameFilter = string.Empty;
            itemCodeFilter = string.Empty;
            uomFilter = string.Empty;
            openingQtyFilter = string.Empty;
            purchaseQtyFilter = string.Empty;
            purchaseReturnFilter = string.Empty;
            salesQtyFilter = string.Empty;
            salesReturnFilter = string.Empty;
            conversionInFilter = string.Empty;
            conversionOutFilter = string.Empty;
            transferredInFilter = string.Empty;
            transferredOutFilter = string.Empty;
            adjustmentFilter = string.Empty;
            closingQtyFilter = string.Empty;
            unitCostFilter = string.Empty;
            closingValueFilter = string.Empty;
            ApplyFilters();
        }

        private DateTime GetCutOffDateTime(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 23, 59, 00);
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            exportMessage = null;
            StateHasChanged();

            try
            {
                var branchIDs = GetBranchIDsForAPI();
                var start = GetCutOffDateTime(startDate);
                var end = GetCutOffDateTime(endDate);
                bool combineBranchValues = false;

                var apiResult = await ReportService.GetStockMovementSummaryAsync(
                    branchIDs, start, end, combineBranchValues);

                if (apiResult != null && apiResult.StatusCode == 200 && apiResult.Result != null)
                {
                    movementData = apiResult.Result;

                    totalItems = movementData.Count;
                    totalOpeningQty = movementData.Sum(x => x.QtyOpening);
                    totalPurchasesQty = movementData.Sum(x => x.QtyPurchase);
                    totalSalesQty = movementData.Sum(x => x.QtySales);
                    totalClosingQty = movementData.Sum(x => x.QtyClosing);
                    totalClosingValue = movementData.Sum(x => x.ClosingCostValue);

                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResult?.Message ?? "No data available";
                    alertType = "warning";
                    movementData = new List<StockMovementSummary>();
                    filteredMovementData = new List<StockMovementSummary>();
                    ResetTotals();
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                movementData = new List<StockMovementSummary>();
                filteredMovementData = new List<StockMovementSummary>();
                ResetTotals();
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ResetTotals()
        {
            totalItems = 0;
            totalOpeningQty = 0;
            totalPurchasesQty = 0;
            totalSalesQty = 0;
            totalClosingQty = 0;
            totalClosingValue = 0;
        }

        private async Task RefreshDataAsync()
        {
            // Validate date range
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
                return;
            }

            await LoadDataAsync();
        }

        private void SetDateRange(string range)
        {
            selectedDateRange = range;
            var today = DateTime.Today;

            switch (range)
            {
                case "Today":
                    startDate = today;
                    endDate = today;
                    break;
                case "Week":
                    int daysUntilEndOfWeek = (7 - (int)today.DayOfWeek + (int)DayOfWeek.Saturday) % 7;
                    startDate = today.AddDays(-(int)today.DayOfWeek + 1);
                    endDate = today.AddDays(daysUntilEndOfWeek);
                    break;
                case "Month":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;
                case "Year":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = new DateTime(today.Year, 12, 31);
                    break;
            }

            // Validate after setting dates
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
                return;
            }

            StateHasChanged();
            _ = LoadDataAsync();
        }

        private string GetDateText()
        {
            return $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
        }

        private async Task ExportToCSV()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMovementData.Any() ? filteredMovementData : movementData;

            if (!dataToExport.Any())
            {
                exportMessage = LangSvc.GetText("No data to export");
                alertType = "warning";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
                return;
            }

            var csv = new StringBuilder();
            csv.Append("\uFEFF");

            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch(es)\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Branch IDs\",\"{GetBranchIDsForAPI()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateText()}\",");
            csv.AppendLine($"\"Total Items\",,{totalItems.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Opening Qty\",,{totalOpeningQty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Purchases Qty\",,{totalPurchasesQty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Sales Qty\",,{totalSalesQty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Closing Qty\",,{totalClosingQty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Closing Value\",,{totalClosingValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(branchIdFilter))
                    csv.AppendLine($"\"  - Branch ID Filter\",\"{EscapeCsvValue(branchIdFilter)}\",");
                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                    csv.AppendLine($"\"  - Item Name Filter\",\"{EscapeCsvValue(itemNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(groupNameFilter))
                    csv.AppendLine($"\"  - Group Name Filter\",\"{EscapeCsvValue(groupNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(itemCodeFilter))
                    csv.AppendLine($"\"  - Display Code Filter\",\"{EscapeCsvValue(itemCodeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(uomFilter))
                    csv.AppendLine($"\"  - UOM Filter\",\"{EscapeCsvValue(uomFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Branch ID\",\"Item Name\",\"Item Group\",\"Display Code\",\"UOM\",\"Opening Qty\",\"Purchase Qty\",\"Purchase Return\",\"Sales Qty\",\"Sales Return\",\"Conversion In\",\"Conversion Out\",\"Transferred In\",\"Transferred Out\",\"Adjustment Qty\",\"Closing Qty\",\"Unit Cost\",\"Closing Value\"");

            foreach (var item in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(item.BranchID)}\",\"{EscapeCsvValue(item.ItemName)}\",\"{EscapeCsvValue(item.GroupName)}\",\"{EscapeCsvValue(item.ItemCode)}\",\"{EscapeCsvValue(item.UOM)}\",{item.QtyOpening.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyPurchase.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyPurchaseReturn.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtySales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtySalesReturn.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyConversionIn.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyConversionOut.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyTransferredIn.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyTransferredOut.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyAdjustment.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.QtyClosing.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.UnitCost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.ClosingCostValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"StockMovementSummary_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.csv";
            var csvContent = csv.ToString();
            var csvBytes = Encoding.UTF8.GetBytes(csvContent);
            var base64Content = Convert.ToBase64String(csvBytes);

            await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, "text/csv");

            exportMessage = LangSvc.GetText("CSV export completed!");
            alertType = "success";
            StateHasChanged();
            await Task.Delay(3000);
            exportMessage = null;
            StateHasChanged();
        }

        private string EscapeCsvValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\"", "\"\"");
        }

        private async Task ExportToPDF()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMovementData.Any() ? filteredMovementData : movementData;

            if (!dataToExport.Any())
            {
                exportMessage = LangSvc.GetText("No data to export");
                alertType = "warning";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
                return;
            }

            try
            {
                var pdfBytes = GeneratePdfDocument(dataToExport);
                var fileName = $"StockMovementSummary_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf";

                await DownloadFile(fileName, "application/pdf", Convert.ToBase64String(pdfBytes));

                exportMessage = LangSvc.GetText("PDF export completed!");
                alertType = "success";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                exportMessage = $"{LangSvc.GetText("ErrorGeneratingPDF")}: {ex.Message}";
                alertType = "error";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
            }
        }

        private byte[] GeneratePdfDocument(List<StockMovementSummary> dataToExport)
        {
            var branchDisplay = GetBranchDisplay();
            var branchIDs = GetBranchIDsForAPI();

            string fontFamily;

            switch (AppState.Platform)
            {
                case "Android":
                    fontFamily = "Noto Sans SC";
                    break;
                case "iOS":
                case "MacCatalyst":
                    fontFamily = "Noto Sans SC";
                    break;
                case "Windows":
                    fontFamily = "Noto Sans SC";
                    break;
                default:
                    fontFamily = "Noto Sans SC";
                    break;
            }

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A3.Landscape());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(7));

                    page.Header()
                        .AlignCenter()
                        .Text("Stock Movement Summary Report")
                        .SemiBold().FontSize(14).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(5f)
                        .Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Report Summary").Bold().FontSize(9);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Branch(es): {branchDisplay} | ");
                                text.Span($"Branch IDs: {branchIDs} | ");
                                text.Span($"Date Range: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Items: {totalItems:N0} | ");
                                text.Span($"Total Opening Qty: {totalOpeningQty:N2} | ");
                                text.Span($"Total Purchases: {totalPurchasesQty:N2} | ");
                                text.Span($"Total Sales: {totalSalesQty:N2}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Closing Qty: {totalClosingQty:N2} | ");
                                text.Span($"Total Closing Value: {totalClosingValue:N2}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(branchIdFilter))
                                    column.Item().Text($"  - Branch ID: {branchIdFilter}");
                                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                                    column.Item().Text($"  - Item Name: {itemNameFilter}");
                                if (!string.IsNullOrWhiteSpace(groupNameFilter))
                                    column.Item().Text($"  - Group Name: {groupNameFilter}");
                                if (!string.IsNullOrWhiteSpace(itemCodeFilter))
                                    column.Item().Text($"  - Display Code: {itemCodeFilter}");
                                if (!string.IsNullOrWhiteSpace(uomFilter))
                                    column.Item().Text($"  - UOM: {uomFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header - 18 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(0.6f).Padding(3f).Background(Colors.Brown.Medium).Text("Branch ID").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(1.2f).Padding(3f).Background(Colors.Brown.Medium).Text("Item Name").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.8f).Padding(3f).Background(Colors.Brown.Medium).Text("Group").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.6f).Padding(3f).Background(Colors.Brown.Medium).Text("Code").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.5f).Padding(3f).Background(Colors.Brown.Medium).Text("UOM").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Opening").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Purchase").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("P.Return").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Sales").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("S.Return").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Conv In").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Conv Out").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Tfr In").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Tfr Out").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Adjust").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Closing").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.7f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Unit Cost").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.8f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Closing Value").FontColor(Colors.White).Bold().FontSize(6);
                            });

                            // Table Rows
                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(0.6f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.BranchID) ? "" : item.BranchID).FontSize(6);
                                    row.RelativeItem(1.2f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.ItemName) ? "" : item.ItemName).FontSize(6);
                                    row.RelativeItem(0.8f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.GroupName) ? "" : item.GroupName).FontSize(6);
                                    row.RelativeItem(0.6f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.ItemCode) ? "" : item.ItemCode).FontSize(6);
                                    row.RelativeItem(0.5f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.UOM) ? "" : item.UOM).FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyOpening:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyPurchase:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyPurchaseReturn:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtySales:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtySalesReturn:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyConversionIn:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyConversionOut:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyTransferredIn:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyTransferredOut:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyAdjustment:N2}").FontSize(6);
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.QtyClosing:N2}").FontSize(6).Bold();
                                    row.RelativeItem(0.7f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.UnitCost:N2}").FontSize(6);
                                    row.RelativeItem(0.8f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.ClosingCostValue:N2}").FontSize(6).Bold();
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("This is a system generated report. ");
                            x.Span($"Printed on {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                        });
                });
            }).GeneratePdf();
        }

        private async Task DownloadFile(string fileName, string mimeType, string base64Content)
        {
            try
            {
                await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, mimeType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                throw;
            }
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            if (CanSelectBranch())
            {
                if (!string.IsNullOrEmpty(AppState.SelectedBranchID))
                {
                    selectedBranchIDs.Add(AppState.SelectedBranchID);
                    selectedBranchDisplay = AppState.SelectedBranchID;

                    if (AppState.AvailableBranches != null)
                    {
                        var matchingBranch = AppState.AvailableBranches
                            .FirstOrDefault(b => b.ToString() == AppState.SelectedBranchID);
                        if (matchingBranch != null)
                        {
                            selectedBranchDisplay = matchingBranch.ToString();
                        }
                    }
                }
                else
                {
                    selectAllBranches = true;
                    if (AppState.AvailableBranches != null)
                    {
                        selectedBranchIDs = AppState.AvailableBranches.Select(b => b.ToString()).ToList();
                    }
                    else
                    {
                        selectedBranchIDs = new List<string> { "HQ" };
                    }
                    selectedBranchDisplay = "All Branches";
                }
            }
            else
            {
                selectedBranchIDs = new List<string> { AppState.SelectedBranchID ?? "HQ" };
                selectedBranchDisplay = AppState.SelectedBranchID ?? "HQ";
                selectAllBranches = false;
            }

            await LoadDataAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
        }

        private bool showAlertDialogModal = false;
        private string alertDialogTitle = string.Empty;
        private string alertDialogMessage = string.Empty;

        private void ShowAlertDialog(string title, string message)
        {
            alertDialogTitle = title;
            alertDialogMessage = message;
            showAlertDialogModal = true;
            StateHasChanged();
        }

        private void CloseAlertDialog()
        {
            showAlertDialogModal = false;
            StateHasChanged();
        }

        private void ShowNotification(string message)
        {
            ShowAlertDialog("Notification", message);
        }

        public class StockMovementSummary
        {
            public string BranchID { get; set; } = string.Empty;
            public string ItemName { get; set; } = string.Empty;
            public string GroupName { get; set; } = string.Empty;
            public string ItemCode { get; set; } = string.Empty;
            public string UOM { get; set; } = string.Empty;
            public decimal QtyOpening { get; set; }
            public decimal QtyPurchase { get; set; }
            public decimal QtyPurchaseReturn { get; set; }
            public decimal QtySales { get; set; }
            public decimal QtySalesReturn { get; set; }
            public decimal QtyConversionIn { get; set; }
            public decimal QtyConversionOut { get; set; }
            public decimal QtyTransferredIn { get; set; }
            public decimal QtyTransferredOut { get; set; }
            public decimal QtyAdjustment { get; set; }
            public decimal QtyClosing { get; set; }
            public decimal UnitCost { get; set; }
            public decimal ClosingCostValue { get; set; }
        }
    }
}