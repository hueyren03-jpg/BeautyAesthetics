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
    public partial class StockBalanceWithCostReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime selectedDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Branch selection state
        private bool showBranchModal = false;
        private string selectedBranchDisplay = string.Empty; // Display text for the button
        private List<string> selectedBranchIDs = new(); // List of selected branch IDs
        private List<string> stagedBranchIDs = new(); // Temporary selection in modal
        private string _branchSearchTerm = string.Empty;
        private List<string> masterBranchList = new();
        private List<string> filteredBranchList = new();
        private bool selectAllBranches = false;

        // Data
        private List<StockBalanceWithCost> stockData = new();
        private List<StockBalanceWithCost> filteredStockData = new();

        // Filter properties
        private string branchIDFilter = string.Empty;
        private string accountNameFilter = string.Empty;
        private string itemGroupFilter = string.Empty;
        private string displayCodeFilter = string.Empty;
        private string uomFilter = string.Empty;
        private string balanceQtyFilter = string.Empty;
        private string unitCostFilter = string.Empty;
        private string balanceValueFilter = string.Empty;
        private string salesPriceBalanceValueFilter = string.Empty;

        // Summary totals
        private int totalItems = 0;
        private decimal totalBalanceQty = 0;
        private decimal totalBalanceValue = 0;
        private decimal totalSalesPriceBalanceValue = 0;

        static StockBalanceWithCostReport()
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

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
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

            // Populate branch list from AppState
            if (AppState.AvailableBranches != null && AppState.AvailableBranches.Any())
            {
                masterBranchList = AppState.AvailableBranches.Select(b => b.ToString()).ToList();
            }
            else
            {
                // Fallback - use current selection or default
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

            // Copy current selections to staged
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

            // If all branches are selected, set selectAllBranches flag
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

        private void ApplyFilters()
        {
            if (stockData == null || !stockData.Any())
            {
                filteredStockData = new List<StockBalanceWithCost>();
                return;
            }

            var query = stockData.AsEnumerable();

            // Branch ID filter
            if (!string.IsNullOrWhiteSpace(branchIDFilter))
            {
                query = query.Where(x => x.BranchID != null &&
                    x.BranchID.Contains(branchIDFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Account Name filter
            if (!string.IsNullOrWhiteSpace(accountNameFilter))
            {
                query = query.Where(x => x.AccountName != null &&
                    x.AccountName.Contains(accountNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Item Group Name filter
            if (!string.IsNullOrWhiteSpace(itemGroupFilter))
            {
                query = query.Where(x => x.ItemGroupName != null &&
                    x.ItemGroupName.Contains(itemGroupFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Display Code filter
            if (!string.IsNullOrWhiteSpace(displayCodeFilter))
            {
                query = query.Where(x => x.DisplayCode != null &&
                    x.DisplayCode.Contains(displayCodeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // UOM filter
            if (!string.IsNullOrWhiteSpace(uomFilter))
            {
                query = query.Where(x => x.UOMName != null &&
                    x.UOMName.Contains(uomFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Balance Qty filter
            if (!string.IsNullOrWhiteSpace(balanceQtyFilter))
            {
                var conditions = ParseNumericFilter(balanceQtyFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.BalanceQty >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.BalanceQty <= conditions.Value.max.Value);
                }
            }

            // Unit Cost filter
            if (!string.IsNullOrWhiteSpace(unitCostFilter))
            {
                var conditions = ParseNumericFilter(unitCostFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.UnitCost >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.UnitCost <= conditions.Value.max.Value);
                }
            }

            // Balance Value filter
            if (!string.IsNullOrWhiteSpace(balanceValueFilter))
            {
                var conditions = ParseNumericFilter(balanceValueFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.BalanceValue >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.BalanceValue <= conditions.Value.max.Value);
                }
            }

            // Sales Price Balance Value filter
            if (!string.IsNullOrWhiteSpace(salesPriceBalanceValueFilter))
            {
                var conditions = ParseNumericFilter(salesPriceBalanceValueFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.SalesPriceBalanceValue >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.SalesPriceBalanceValue <= conditions.Value.max.Value);
                }
            }

            filteredStockData = query.ToList();
            StateHasChanged();
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
            return !string.IsNullOrWhiteSpace(branchIDFilter) ||
                   !string.IsNullOrWhiteSpace(accountNameFilter) ||
                   !string.IsNullOrWhiteSpace(itemGroupFilter) ||
                   !string.IsNullOrWhiteSpace(displayCodeFilter) ||
                   !string.IsNullOrWhiteSpace(uomFilter) ||
                   !string.IsNullOrWhiteSpace(balanceQtyFilter) ||
                   !string.IsNullOrWhiteSpace(unitCostFilter) ||
                   !string.IsNullOrWhiteSpace(balanceValueFilter) ||
                   !string.IsNullOrWhiteSpace(salesPriceBalanceValueFilter);
        }

        private void ClearFilters()
        {
            branchIDFilter = string.Empty;
            accountNameFilter = string.Empty;
            itemGroupFilter = string.Empty;
            displayCodeFilter = string.Empty;
            uomFilter = string.Empty;
            balanceQtyFilter = string.Empty;
            unitCostFilter = string.Empty;
            balanceValueFilter = string.Empty;
            salesPriceBalanceValueFilter = string.Empty;
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
                var endDate = GetCutOffDateTime(selectedDate);
                var inventoryIDs = "All";

                var apiResult = await ReportService.GetStockBalanceWithCostAsync(
                    branchIDs, endDate, inventoryIDs);

                if (apiResult != null && apiResult.StatusCode == 200 && apiResult.Result != null)
                {
                    stockData = apiResult.Result;

                    totalItems = stockData.Count;
                    totalBalanceQty = stockData.Sum(x => x.BalanceQty);
                    totalBalanceValue = stockData.Sum(x => x.BalanceValue);
                    totalSalesPriceBalanceValue = stockData.Sum(x => x.SalesPriceBalanceValue);

                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResult?.Message ?? "No data available";
                    alertType = "warning";
                    stockData = new List<StockBalanceWithCost>();
                    filteredStockData = new List<StockBalanceWithCost>();
                    ResetTotals();
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                stockData = new List<StockBalanceWithCost>();
                filteredStockData = new List<StockBalanceWithCost>();
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
            totalBalanceQty = 0;
            totalBalanceValue = 0;
            totalSalesPriceBalanceValue = 0;
        }

        private async Task RefreshDataAsync()
        {
            await LoadDataAsync();
        }

        private void SetDateRange(string range)
        {
            selectedDateRange = range;
            var today = DateTime.Today;

            switch (range)
            {
                case "Today":
                    selectedDate = today;
                    break;
                case "Week":
                    int daysUntilEndOfWeek = (7 - (int)today.DayOfWeek + (int)DayOfWeek.Saturday) % 7;
                    selectedDate = today.AddDays(daysUntilEndOfWeek);
                    break;
                case "Month":
                    selectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;
                case "Year":
                    selectedDate = new DateTime(today.Year, 12, 31);
                    break;
            }

            StateHasChanged();
            _ = LoadDataAsync();
        }

        private string GetDateText()
        {
            return selectedDate.ToString("dd/MM/yyyy");
        }

        private async Task ExportToCSV()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredStockData.Any() ? filteredStockData : stockData;

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
            csv.AppendLine($"\"End Date\",\"{GetDateText()}\",");
            csv.AppendLine($"\"Total Items\",,{totalItems.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Balance Qty\",,{totalBalanceQty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Balance Value\",,{totalBalanceValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Sales Price Value\",,{totalSalesPriceBalanceValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(accountNameFilter))
                    csv.AppendLine($"\"  - Item Name Filter\",\"{EscapeCsvValue(accountNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(displayCodeFilter))
                    csv.AppendLine($"\"  - Display Code Filter\",\"{EscapeCsvValue(displayCodeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(uomFilter))
                    csv.AppendLine($"\"  - UOM Filter\",\"{EscapeCsvValue(uomFilter)}\",");
                if (!string.IsNullOrWhiteSpace(balanceQtyFilter))
                    csv.AppendLine($"\"  - Balance Qty Filter\",\"{EscapeCsvValue(balanceQtyFilter)}\",");
                if (!string.IsNullOrWhiteSpace(unitCostFilter))
                    csv.AppendLine($"\"  - Unit Cost Filter\",\"{EscapeCsvValue(unitCostFilter)}\",");
                if (!string.IsNullOrWhiteSpace(balanceValueFilter))
                    csv.AppendLine($"\"  - Balance Value Filter\",\"{EscapeCsvValue(balanceValueFilter)}\",");
                if (!string.IsNullOrWhiteSpace(salesPriceBalanceValueFilter))
                    csv.AppendLine($"\"  - Sales Price Value Filter\",\"{EscapeCsvValue(salesPriceBalanceValueFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Branch ID\",\"Item Name\",\"Item Group\",\"Display Code\",\"UOM\",\"Balance Qty\",\"Unit Cost\",\"Balance Value\",\"Sales Price Balance Value\"");

            foreach (var item in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(item.BranchID)}\",\"{EscapeCsvValue(item.AccountName)}\",\"{EscapeCsvValue(item.ItemGroupName)}\",\"{EscapeCsvValue(item.DisplayCode)}\",\"{EscapeCsvValue(item.UOMName)}\",{item.BalanceQty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.UnitCost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.BalanceValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.SalesPriceBalanceValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"StockBalanceWithCost_{selectedDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredStockData.Any() ? filteredStockData : stockData;

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
                var fileName = $"StockBalanceWithCost_{selectedDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<StockBalanceWithCost> dataToExport)
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
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(8));

                    page.Header()
                        .AlignCenter()
                        .Text("Stock Balance with Cost Report")
                        .SemiBold().FontSize(16).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(5f)
                        .Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Report Summary").Bold().FontSize(10);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Branch(es): {branchDisplay} | ");
                                text.Span($"Branch IDs: {branchIDs} | ");
                                text.Span($"End Date: {selectedDate:dd/MM/yyyy}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Items: {totalItems:N0} | ");
                                text.Span($"Total Balance Qty: {totalBalanceQty:N2} | ");
                                text.Span($"Total Balance Value: {totalBalanceValue:N2}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Sales Price Value: {totalSalesPriceBalanceValue:N2}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(accountNameFilter))
                                    column.Item().Text($"  - Item Name: {accountNameFilter}");
                                if (!string.IsNullOrWhiteSpace(displayCodeFilter))
                                    column.Item().Text($"  - Display Code: {displayCodeFilter}");
                                if (!string.IsNullOrWhiteSpace(uomFilter))
                                    column.Item().Text($"  - UOM: {uomFilter}");
                                if (!string.IsNullOrWhiteSpace(balanceQtyFilter))
                                    column.Item().Text($"  - Balance Qty: {balanceQtyFilter}");
                                if (!string.IsNullOrWhiteSpace(unitCostFilter))
                                    column.Item().Text($"  - Unit Cost: {unitCostFilter}");
                                if (!string.IsNullOrWhiteSpace(balanceValueFilter))
                                    column.Item().Text($"  - Balance Value: {balanceValueFilter}");
                                if (!string.IsNullOrWhiteSpace(salesPriceBalanceValueFilter))
                                    column.Item().Text($"  - Sales Price Value: {salesPriceBalanceValueFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header 
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).Text("Branch ID").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.5f).Padding(4f).Background(Colors.Brown.Medium).Text("Item Name").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).Text("Item Group").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Display Code").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).Text("UOM").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Balance Qty").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Unit Cost").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Balance Value").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Sales Price Value").FontColor(Colors.White).Bold().FontSize(7);
                            });

                            // Table Rows
                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.BranchID) ? "" : item.BranchID).FontSize(7);
                                    row.RelativeItem(1.5f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.AccountName) ? "" : item.AccountName).FontSize(7);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.ItemGroupName) ? "" : item.ItemGroupName).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.DisplayCode) ? "" : item.DisplayCode).FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.UOMName) ? "" : item.UOMName).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.BalanceQty:N2}").FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.UnitCost:N2}").FontSize(7);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.BalanceValue:N2}").FontSize(7).Bold();
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.SalesPriceBalanceValue:N2}").FontSize(7).Bold();
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

            // Check if branch selection is allowed (only for HQ)
            if (CanSelectBranch())
            {
                // Set default branch from AppState
                if (!string.IsNullOrEmpty(AppState.SelectedBranchID))
                {
                    // Initially select the current branch
                    selectedBranchIDs.Add(AppState.SelectedBranchID);
                    selectedBranchDisplay = AppState.SelectedBranchID;

                    // Try to get the branch name from AvailableBranches
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
                    // Default to All Branches if no branch selected
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
                // Not HQ - show only the current branch, no selection allowed
                selectedBranchIDs = new List<string> { AppState.SelectedBranchID ?? "HQ" };
                selectedBranchDisplay = AppState.SelectedBranchID ?? "HQ";
                selectAllBranches = false;
            }

            await LoadDataAsync();
        }

        private bool CanSelectBranch()
        {
            // Only allow branch selection if the current branch is HQ
            return AppState.SelectedBranchID == "HQ" || AppState.SelectedBranchID == "HQ";
        }

        private bool IsHQMode => AppState.SelectedBranchID == "HQ";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
        }

        public class StockBalanceWithCost
        {

            public string BranchID { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public string DisplayCode { get; set; } = string.Empty;
            public string ItemGroupName { get; set; } = string.Empty;
            public string UOMName { get; set; } = string.Empty;
            public decimal BalanceQty { get; set; }
            public decimal UnitCost { get; set; }
            public decimal BalanceValue { get; set; }
            public decimal SalesPriceBalanceValue { get; set; }
        }
    }
}