using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.FileDownloadService;

namespace SenangRetails.Shared.Pages
{
    public partial class SalesByItemReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        [Parameter]
        public EventCallback<SalesBySKUItem> OnItemSelected { get; set; }

        private List<SalesBySKUItem> salesItems = new();
        private List<SalesBySKUItem> filteredItems = new();
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private string selectedTypeTab = "All";
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";

        // Filter properties
        private string itemNameFilter = string.Empty;
        private string uomFilter = string.Empty;
        private string quantityFilter = string.Empty;
        private string unitCostFilter = string.Empty;
        private string unitPriceFilter = string.Empty;
        private string grossAmountFilter = string.Empty;
        private string discountFilter = string.Empty;
        private string subtotalBTFilter = string.Empty;
        private string taxFilter = string.Empty;
        private string subtotalFilter = string.Empty;
        private string totalCostFilter = string.Empty;
        private string profitFilter = string.Empty;

        // Summary totals
        private decimal totalSubtotal = 0;
        private decimal totalProfit = 0;
        private decimal totalQuantity = 0;

        // Dropdown state
        private bool showExportDropdown = false;

        // Initialize QuestPDF license
        static SalesByItemReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private int GetInventoryTypeId(string type)
        {
            return type switch
            {
                "All" => 0,
                "Service" => 3,
                "Product" => 1,
                "Package" => 5,
                "TopUp" => 7,
                _ => 0
            };
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            exportMessage = null;
            StateHasChanged();

            try
            {
                var branchId = GetBranchDisplay();
                var startDateTime = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);
                var endDateTime = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 00);

                if (selectedTypeTab == "All")
                {
                    var inventoryTypes = new[] { 1, 3, 5, 7 };
                    var allItems = new List<SalesBySKUItem>();

                    foreach (var inventoryTypeId in inventoryTypes)
                    {
                        var apiResponse = await ReportService.GetSalesByItemAsync(
                            startDateTime,
                            endDateTime,
                            branchId,
                            inventoryTypeId);

                        if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                        {
                            allItems.AddRange(apiResponse.Result);
                        }
                    }

                    salesItems = allItems
                        .OrderByDescending(x => x.SubTotal)
                        .ToList();
                }
                else
                {
                    var inventoryTypeId = GetInventoryTypeId(selectedTypeTab);

                    var apiResponse = await ReportService.GetSalesByItemAsync(
                        startDateTime,
                        endDateTime,
                        branchId,
                        inventoryTypeId);

                    if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                    {
                        salesItems = apiResponse.Result
                            .OrderByDescending(x => x.SubTotal)
                            .ToList();
                    }
                    else
                    {
                        salesItems = new List<SalesBySKUItem>();
                    }
                }

                // Calculate totals
                totalSubtotal = salesItems.Sum(x => x.SubTotal);
                totalProfit = salesItems.Sum(x => x.Profit);
                totalQuantity = salesItems.Sum(x => x.Quantity);

                // Apply filters after loading data
                ApplyFilters();
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                salesItems = new List<SalesBySKUItem>();
                filteredItems = new List<SalesBySKUItem>();
                totalSubtotal = 0;
                totalProfit = 0;
                totalQuantity = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (salesItems == null || !salesItems.Any())
            {
                filteredItems = new List<SalesBySKUItem>();
                return;
            }

            var query = salesItems.AsEnumerable();

            // Apply item name filter (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(itemNameFilter))
            {
                query = query.Where(x => x.Description != null &&
                    x.Description.Contains(itemNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply UOM filter (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(uomFilter))
            {
                query = query.Where(x => x.UOM != null &&
                    x.UOM.Contains(uomFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply quantity filter
            if (!string.IsNullOrWhiteSpace(quantityFilter))
            {
                var quantityConditions = ParseNumericFilter(quantityFilter);
                if (quantityConditions.HasValue)
                {
                    if (quantityConditions.Value.min.HasValue)
                        query = query.Where(x => x.Quantity >= quantityConditions.Value.min.Value);
                    if (quantityConditions.Value.max.HasValue)
                        query = query.Where(x => x.Quantity <= quantityConditions.Value.max.Value);
                }
            }

            // Apply unit cost filter
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

            // Apply unit price filter
            if (!string.IsNullOrWhiteSpace(unitPriceFilter))
            {
                var conditions = ParseNumericFilter(unitPriceFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.UnitSalesPrice >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.UnitSalesPrice <= conditions.Value.max.Value);
                }
            }

            // Apply gross amount filter
            if (!string.IsNullOrWhiteSpace(grossAmountFilter))
            {
                var conditions = ParseNumericFilter(grossAmountFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.SalesGrossAmount >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.SalesGrossAmount <= conditions.Value.max.Value);
                }
            }

            // Apply discount filter
            if (!string.IsNullOrWhiteSpace(discountFilter))
            {
                var conditions = ParseNumericFilter(discountFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.SalesDiscount >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.SalesDiscount <= conditions.Value.max.Value);
                }
            }

            // Apply subtotal before tax filter
            if (!string.IsNullOrWhiteSpace(subtotalBTFilter))
            {
                var conditions = ParseNumericFilter(subtotalBTFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.SubtotalBeforeGST >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.SubtotalBeforeGST <= conditions.Value.max.Value);
                }
            }

            // Apply tax filter
            if (!string.IsNullOrWhiteSpace(taxFilter))
            {
                var conditions = ParseNumericFilter(taxFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.TaxAmount >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.TaxAmount <= conditions.Value.max.Value);
                }
            }

            // Apply subtotal filter
            if (!string.IsNullOrWhiteSpace(subtotalFilter))
            {
                var subtotalConditions = ParseNumericFilter(subtotalFilter);
                if (subtotalConditions.HasValue)
                {
                    if (subtotalConditions.Value.min.HasValue)
                        query = query.Where(x => x.SubTotal >= subtotalConditions.Value.min.Value);
                    if (subtotalConditions.Value.max.HasValue)
                        query = query.Where(x => x.SubTotal <= subtotalConditions.Value.max.Value);
                }
            }

            // Apply total cost filter
            if (!string.IsNullOrWhiteSpace(totalCostFilter))
            {
                var conditions = ParseNumericFilter(totalCostFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.TotalCost >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.TotalCost <= conditions.Value.max.Value);
                }
            }

            // Apply profit filter
            if (!string.IsNullOrWhiteSpace(profitFilter))
            {
                var profitConditions = ParseNumericFilter(profitFilter);
                if (profitConditions.HasValue)
                {
                    if (profitConditions.Value.min.HasValue)
                        query = query.Where(x => x.Profit >= profitConditions.Value.min.Value);
                    if (profitConditions.Value.max.HasValue)
                        query = query.Where(x => x.Profit <= profitConditions.Value.max.Value);
                }
            }

            filteredItems = query.ToList();
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
            return !string.IsNullOrWhiteSpace(itemNameFilter) ||
                   !string.IsNullOrWhiteSpace(uomFilter) ||
                   !string.IsNullOrWhiteSpace(quantityFilter) ||
                   !string.IsNullOrWhiteSpace(unitCostFilter) ||
                   !string.IsNullOrWhiteSpace(unitPriceFilter) ||
                   !string.IsNullOrWhiteSpace(grossAmountFilter) ||
                   !string.IsNullOrWhiteSpace(discountFilter) ||
                   !string.IsNullOrWhiteSpace(subtotalBTFilter) ||
                   !string.IsNullOrWhiteSpace(taxFilter) ||
                   !string.IsNullOrWhiteSpace(subtotalFilter) ||
                   !string.IsNullOrWhiteSpace(totalCostFilter) ||
                   !string.IsNullOrWhiteSpace(profitFilter);
        }

        private void ClearFilters()
        {
            itemNameFilter = string.Empty;
            uomFilter = string.Empty;
            quantityFilter = string.Empty;
            unitCostFilter = string.Empty;
            unitPriceFilter = string.Empty;
            grossAmountFilter = string.Empty;
            discountFilter = string.Empty;
            subtotalBTFilter = string.Empty;
            taxFilter = string.Empty;
            subtotalFilter = string.Empty;
            totalCostFilter = string.Empty;
            profitFilter = string.Empty;
            ApplyFilters();
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
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = today.AddDays(-diff).Date;
                    endDate = startDate.AddDays(6).Date;
                    break;
                case "Month":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
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

        private async Task RefreshData()
        {
            // Validate date range
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
                return;
            }

            await LoadDataAsync();
        }

        private async Task SelectTypeTab(string tab)
        {
            selectedTypeTab = tab;
            ClearFilters();
            await LoadDataAsync();
        }

        private string GetDateRangeText()
        {
            var startDateFormatted = startDate.ToString("dd/MM/yyyy");
            var endDateFormatted = endDate.ToString("dd/MM/yyyy");

            return selectedDateRange switch
            {
                "Today" => $"Today ({startDateFormatted})",
                "Week" => $"Week ({startDateFormatted} - {endDateFormatted})",
                "Month" => $"Month ({startDateFormatted} - {endDateFormatted})",
                "Year" => $"Year ({startDateFormatted} - {endDateFormatted})",
                _ => $"{startDateFormatted} - {endDateFormatted}"
            };
        }

        private async Task OnItemClick(SalesBySKUItem item)
        {
            if (OnItemSelected.HasDelegate)
            {
                await OnItemSelected.InvokeAsync(item);
            }
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredItems.Any() ? filteredItems : salesItems;

            if (!dataToExport.Any())
            {
                exportMessage = "No data to export";
                alertType = "warning";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
                return;
            }

            var csv = new StringBuilder();
            csv.Append("\uFEFF");

            // Report Summary Section
            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Report Type\",\"{selectedTypeTab}\",");
            csv.AppendLine($"\"Total Sales\",,{totalSubtotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Profit\",,{totalProfit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Quantity\",,{totalQuantity.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Items\",,{dataToExport.Count.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            // Add filter information if filters are active
            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                    csv.AppendLine($"\"  - Item Name Filter\",\"{EscapeCsvValue(itemNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(uomFilter))
                    csv.AppendLine($"\"  - UOM Filter\",\"{EscapeCsvValue(uomFilter)}\",");
                if (!string.IsNullOrWhiteSpace(quantityFilter))
                    csv.AppendLine($"\"  - Quantity Filter\",\"{EscapeCsvValue(quantityFilter)}\",");
                if (!string.IsNullOrWhiteSpace(unitCostFilter))
                    csv.AppendLine($"\"  - Unit Cost Filter\",\"{EscapeCsvValue(unitCostFilter)}\",");
                if (!string.IsNullOrWhiteSpace(unitPriceFilter))
                    csv.AppendLine($"\"  - Unit Price Filter\",\"{EscapeCsvValue(unitPriceFilter)}\",");
                if (!string.IsNullOrWhiteSpace(grossAmountFilter))
                    csv.AppendLine($"\"  - Gross Amount Filter\",\"{EscapeCsvValue(grossAmountFilter)}\",");
                if (!string.IsNullOrWhiteSpace(discountFilter))
                    csv.AppendLine($"\"  - Discount Filter\",\"{EscapeCsvValue(discountFilter)}\",");
                if (!string.IsNullOrWhiteSpace(subtotalBTFilter))
                    csv.AppendLine($"\"  - Subtotal (BT) Filter\",\"{EscapeCsvValue(subtotalBTFilter)}\",");
                if (!string.IsNullOrWhiteSpace(taxFilter))
                    csv.AppendLine($"\"  - Tax Filter\",\"{EscapeCsvValue(taxFilter)}\",");
                if (!string.IsNullOrWhiteSpace(subtotalFilter))
                    csv.AppendLine($"\"  - Subtotal Filter\",\"{EscapeCsvValue(subtotalFilter)}\",");
                if (!string.IsNullOrWhiteSpace(totalCostFilter))
                    csv.AppendLine($"\"  - Total Cost Filter\",\"{EscapeCsvValue(totalCostFilter)}\",");
                if (!string.IsNullOrWhiteSpace(profitFilter))
                    csv.AppendLine($"\"  - Profit Filter\",\"{EscapeCsvValue(profitFilter)}\",");
            }

            // Empty line separator
            csv.AppendLine();

            // Data Header
            csv.AppendLine($"\"Item Name\",\"Item Group\",\"Product Code\",\"UOM\",\"Quantity\",\"Unit Cost\",\"Unit Price\",\"Gross Amount\",\"Discount\",\"Subtotal (Before Tax)\",\"Tax\",\"Subtotal\",\"Total Cost\",\"Profit\"");

            // Data rows
            foreach (var item in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(item.Description)}\",\"{EscapeCsvValue(item.ItemGroupName)}\",\"{EscapeCsvValue(item.ProductCode)}\",\"{EscapeCsvValue(item.UOM)}\",{item.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.UnitCost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.UnitSalesPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.SalesGrossAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.SalesDiscount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.SubtotalBeforeGST.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.TaxAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.SubTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.TotalCost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.Profit.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"SalesByItem_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
            var csvContent = csv.ToString();

            // Convert string content to Base64
            var csvBytes = Encoding.UTF8.GetBytes(csvContent);
            var base64Content = Convert.ToBase64String(csvBytes);

            // Use the file download service instead of JS
            await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, "text/csv");

            exportMessage = "CSV export completed!";
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
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredItems.Any() ? filteredItems : salesItems;

            if (!dataToExport.Any())
            {
                exportMessage = "No data to export";
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
                var fileName = $"SalesBySKU_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

                await DownloadFile(fileName, "application/pdf", Convert.ToBase64String(pdfBytes));

                exportMessage = "PDF export completed!";
                alertType = "success";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                exportMessage = $"Error generating PDF: {ex.Message}";
                alertType = "error";
                StateHasChanged();
                await Task.Delay(3000);
                exportMessage = null;
                StateHasChanged();
            }
        }

        private byte[] GeneratePdfDocument(List<SalesBySKUItem> dataToExport)
        {
            var branchName = GetBranchDisplay();

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
                        .Text("Sales By Item Report")
                        .SemiBold().FontSize(16).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(5f)
                        .Column(column =>
                        {
                            // Report Summary
                            column.Item().Text(text =>
                            {
                                text.Span("Report Summary").Bold().FontSize(10);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Branch: {branchName} | ");
                                text.Span($"Report Type: {selectedTypeTab} | ");
                                text.Span($"Date Range: {GetDateRangeText()}");
                            });

                            // Show filter information if active
                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                                    column.Item().Text($"  - Item Name: {itemNameFilter}");
                                if (!string.IsNullOrWhiteSpace(uomFilter))
                                    column.Item().Text($"  - UOM: {uomFilter}");
                                if (!string.IsNullOrWhiteSpace(quantityFilter))
                                    column.Item().Text($"  - Quantity: {quantityFilter}");
                                if (!string.IsNullOrWhiteSpace(unitCostFilter))
                                    column.Item().Text($"  - Unit Cost: {unitCostFilter}");
                                if (!string.IsNullOrWhiteSpace(unitPriceFilter))
                                    column.Item().Text($"  - Unit Price: {unitPriceFilter}");
                                if (!string.IsNullOrWhiteSpace(grossAmountFilter))
                                    column.Item().Text($"  - Gross Amount: {grossAmountFilter}");
                                if (!string.IsNullOrWhiteSpace(discountFilter))
                                    column.Item().Text($"  - Discount: {discountFilter}");
                                if (!string.IsNullOrWhiteSpace(subtotalBTFilter))
                                    column.Item().Text($"  - Subtotal (BT): {subtotalBTFilter}");
                                if (!string.IsNullOrWhiteSpace(taxFilter))
                                    column.Item().Text($"  - Tax: {taxFilter}");
                                if (!string.IsNullOrWhiteSpace(subtotalFilter))
                                    column.Item().Text($"  - Subtotal: {subtotalFilter}");
                                if (!string.IsNullOrWhiteSpace(totalCostFilter))
                                    column.Item().Text($"  - Total Cost: {totalCostFilter}");
                                if (!string.IsNullOrWhiteSpace(profitFilter))
                                    column.Item().Text($"  - Profit: {profitFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Sales: {totalSubtotal:N2} | ");
                                text.Span($"Total Profit: {totalProfit:N2} | ");
                                text.Span($"Total Quantity: {totalQuantity:N0} | ");
                                text.Span($"Total Items: {dataToExport.Count}");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1.5f).Padding(4f).Background(Colors.Brown.Medium).Text("Item Name").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).Text("UOM").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.7f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Qty").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Unit Cost").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Unit Price").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.7f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Gross Amt").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Disc").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Subtotal (BT)").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.5f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Tax").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Subtotal").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.7f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Total Cost").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.9f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Profit").FontColor(Colors.White).Bold().FontSize(8);
                            });

                            // Table Rows
                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1.5f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.Description).FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.UOM).FontSize(7);
                                    row.RelativeItem(0.7f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.Quantity:N2}").FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.UnitCost:N2}").FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.UnitSalesPrice:N2}").FontSize(7);
                                    row.RelativeItem(0.7f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.SalesGrossAmount:N2}").FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.SalesDiscount:N2}").FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.SubtotalBeforeGST:N2}").FontSize(7);
                                    row.RelativeItem(0.5f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.TaxAmount:N2}").FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.SubTotal:N2}").FontSize(7);
                                    row.RelativeItem(0.7f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.TotalCost:N2}").FontSize(7);
                                    row.RelativeItem(0.9f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.Profit:N2}").FontSize(7);
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
            SetDateRange(selectedDateRange);
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

        public class SalesBySKUItem
        {
            public string Description { get; set; } = string.Empty;
            public string UOM { get; set; } = string.Empty;
            public string ItemGroupName { get; set; } = string.Empty;
            public string BrandName { get; set; } = string.Empty;
            public string ProductCode { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal UnitCost { get; set; }
            public decimal UnitSalesPrice { get; set; }
            public decimal SalesGrossAmount { get; set; }
            public decimal SalesDiscount { get; set; }
            public decimal SubtotalBeforeGST { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal SubTotal { get; set; }
            public decimal TotalCost { get; set; }
            public decimal Profit { get; set; }
        }
    }
}