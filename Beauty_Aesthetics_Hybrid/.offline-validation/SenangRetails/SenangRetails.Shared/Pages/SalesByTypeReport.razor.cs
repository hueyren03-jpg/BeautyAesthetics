using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.FileDownloadService;
using static SenangRetails.Shared.Pages.DashboardReport;

namespace SenangRetails.Shared.Pages
{
    public partial class SalesByTypeReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        static SalesByTypeReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private string selectedTypeTab = "All";
        private List<SalesItemTypeByItemDetail> items = new();
        private List<SalesItemTypeByItemDetail> filteredItems = new();
        private decimal totalAmount = 0;
        private decimal totalQuantity = 0;
        private string topSalesItemName = "-";
        private decimal topSalesItemAmount = 0;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";

        // Filter properties
        private string itemNameFilter = string.Empty;
        private string quantityFilter = string.Empty;
        private string amountFilter = string.Empty;

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

        private DateTime GetStartDateTime(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
        }

        private DateTime GetEndDateTime(DateTime date)
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
                var branchId = GetBranchDisplay();

                // Handle "All" types specially
                if (selectedTypeTab == "All")
                {
                    // Define all inventory types to include
                    var inventoryTypes = new[] { 1, 3, 5, 7 }; // Product, Service, Package, TopUp
                    var allItems = new List<SalesItemTypeByItemDetail>();

                    foreach (var inventoryTypeId in inventoryTypes)
                    {
                        var startDateTime = GetStartDateTime(startDate);
                        var endDateTime = GetEndDateTime(endDate);

                        var apiResponse = await ReportService.GetSalesItemTypeByItemDetailByDateAsync(
                            startDateTime,
                            endDateTime,
                            branchId,
                            inventoryTypeId);

                        if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                        {
                            allItems.AddRange(apiResponse.Result);
                        }
                    }

                    // Combine and group by ItemName
                    items = allItems
                        .GroupBy(x => x.ItemName)
                        .Select(g => new SalesItemTypeByItemDetail
                        {
                            ItemName = g.Key,
                            Quantity = g.Sum(x => x.Quantity),
                            Amount = g.Sum(x => x.Amount)
                        })
                        .OrderByDescending(x => x.Amount)
                        .ToList();
                }
                else
                {
                    var inventoryTypeId = GetInventoryTypeId(selectedTypeTab);
                    var startDateTime = GetStartDateTime(startDate);
                    var endDateTime = GetEndDateTime(endDate);

                    var apiResponse = await ReportService.GetSalesItemTypeByItemDetailByDateAsync(
                        startDateTime,
                        endDateTime,
                        branchId,
                        inventoryTypeId);

                    if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                    {
                        items = apiResponse.Result
                            .OrderByDescending(x => x.Amount)
                            .ToList();
                    }
                    else
                    {
                        items = new List<SalesItemTypeByItemDetail>();
                    }
                }

                totalAmount = items.Sum(x => x.Amount);
                totalQuantity = items.Sum(x => x.Quantity);

                // Calculate top sales item
                if (items.Any())
                {
                    var topItem = items.OrderByDescending(x => x.Amount).FirstOrDefault();
                    if (topItem != null)
                    {
                        topSalesItemName = topItem.ItemName ?? "-";
                        topSalesItemAmount = topItem.Amount;
                    }
                    else
                    {
                        topSalesItemName = "-";
                        topSalesItemAmount = 0;
                    }
                }
                else
                {
                    topSalesItemName = "-";
                    topSalesItemAmount = 0;
                }

                // Apply filters after loading data
                ApplyFilters();

                if (items.Count == 0 && !string.IsNullOrEmpty(exportMessage) == false)
                {
                    // No error message, just no data
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                items = new List<SalesItemTypeByItemDetail>();
                filteredItems = new List<SalesItemTypeByItemDetail>();
                totalAmount = 0;
                totalQuantity = 0;
                topSalesItemName = "-";
                topSalesItemAmount = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (items == null || !items.Any())
            {
                filteredItems = new List<SalesItemTypeByItemDetail>();
                return;
            }

            var query = items.AsEnumerable();

            // Apply item name filter (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(itemNameFilter))
            {
                query = query.Where(x => x.ItemName != null &&
                    x.ItemName.Contains(itemNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply quantity filter (supports min, max, or range)
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

            // Apply amount filter (supports min, max, or range)
            if (!string.IsNullOrWhiteSpace(amountFilter))
            {
                var amountConditions = ParseNumericFilter(amountFilter);
                if (amountConditions.HasValue)
                {
                    if (amountConditions.Value.min.HasValue)
                        query = query.Where(x => x.Amount >= amountConditions.Value.min.Value);
                    if (amountConditions.Value.max.HasValue)
                        query = query.Where(x => x.Amount <= amountConditions.Value.max.Value);
                }
            }

            filteredItems = query.ToList();
            StateHasChanged();
        }

        private (decimal? min, decimal? max)? ParseNumericFilter(string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return null;

            // Check for range format: "min-max" or "min - max"
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
            // Check for "min" only (e.g., ">100" or "100+")
            else if (filterText.Contains('>') || filterText.Contains('+'))
            {
                var valuePart = filterText.Replace(">", "").Replace("+", "").Trim();
                if (decimal.TryParse(valuePart, out decimal min))
                {
                    return (min, null);
                }
            }
            // Check for "max" only (e.g., "<100")
            else if (filterText.Contains('<'))
            {
                var valuePart = filterText.Replace("<", "").Trim();
                if (decimal.TryParse(valuePart, out decimal max))
                {
                    return (null, max);
                }
            }
            // Single value - treat as exact match
            else if (decimal.TryParse(filterText, out decimal exactValue))
            {
                return (exactValue, exactValue);
            }

            return null;
        }

        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(itemNameFilter) ||
                   !string.IsNullOrWhiteSpace(quantityFilter) ||
                   !string.IsNullOrWhiteSpace(amountFilter);
        }

        private void ClearFilters()
        {
            itemNameFilter = string.Empty;
            quantityFilter = string.Empty;
            amountFilter = string.Empty;
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
                case "Custom":
                    startDate = today;
                    endDate = today;
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
            // Clear filters when changing tabs
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
                "Custom" => $"Custom ({startDateFormatted} - {endDateFormatted})",
                _ => $"{startDateFormatted} - {endDateFormatted}"
            };
        }

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredItems.Any() ? filteredItems : items;

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

            // Report Summary Section (at the top)
            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Report Type\",\"{selectedTypeTab}\",");
            csv.AppendLine($"\"Total Amount\",,{totalAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Quantity\",,{totalQuantity.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Top Sales Item\",\"{topSalesItemName}\",{topSalesItemAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Items\",,{dataToExport.Count.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            // Add filter information if filters are active
            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                    csv.AppendLine($"\"  - Item Name Filter\",\"{EscapeCsvValue(itemNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(quantityFilter))
                    csv.AppendLine($"\"  - Quantity Filter\",\"{EscapeCsvValue(quantityFilter)}\",");
                if (!string.IsNullOrWhiteSpace(amountFilter))
                    csv.AppendLine($"\"  - Amount Filter\",\"{EscapeCsvValue(amountFilter)}\",");
            }

            // Empty line separator
            csv.AppendLine();

            // Data Header
            csv.AppendLine($"\"{LangSvc.GetText("ReportItemName")}\",\"{LangSvc.GetText("ReportQuantity")}\",\"{LangSvc.GetText("ReportAmount")}\"");

            // Data rows
            foreach (var item in dataToExport)
            {
                var quantity = item.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var amount = item.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                csv.AppendLine($"\"{EscapeCsvValue(item.ItemName)}\",{quantity},{amount}");
            }

            var fileName = $"SalesByType_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredItems.Any() ? filteredItems : items;

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
                var fileName = $"SalesByType_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<SalesItemTypeByItemDetail> dataToExport)
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
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(10));

                    page.Header()
                        .AlignCenter()
                        .Text("Sales By Type Report")
                        .SemiBold().FontSize(20).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(10)
                        .Column(column =>
                        {
                            // Report Info - Summary at the top
                            column.Item().Text(text =>
                            {
                                text.Span("Report Summary").Bold().FontSize(12);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span("Branch: ").Bold();
                                text.Span(branchName);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span("Report Type: ").Bold();
                                text.Span(selectedTypeTab);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Date Range: {GetDateRangeText()}");
                                text.Line("");
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
                                if (!string.IsNullOrWhiteSpace(quantityFilter))
                                    column.Item().Text($"  - Quantity: {quantityFilter}");
                                if (!string.IsNullOrWhiteSpace(amountFilter))
                                    column.Item().Text($"  - Amount: {amountFilter}");

                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Amount: RM ").Bold();
                                text.Span($"{totalAmount:N2}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Quantity: ").Bold();
                                text.Span($"{totalQuantity:N0}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Top Sales Item: ").Bold();
                                text.Span($"{topSalesItemName} (RM {topSalesItemAmount:N2})");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Items: ").Bold();
                                text.Span($"{dataToExport.Count:N0}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Generated On: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                                text.Line("");
                            });

                            column.Item().PaddingVertical(10).LineHorizontal(1);

                            // Table Header
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(2).Padding(8).Background(Colors.Brown.Medium).Text("Item Name / SKU").FontColor(Colors.White).Bold();
                                row.RelativeItem(1).Padding(8).Background(Colors.Brown.Medium).AlignRight().Text("Quantity").FontColor(Colors.White).Bold();
                                row.RelativeItem(1).Padding(8).Background(Colors.Brown.Medium).AlignRight().Text("Amount (RM)").FontColor(Colors.White).Bold();
                            });

                            // Table Rows
                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(2).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(item.ItemName ?? "");
                                    row.RelativeItem(1).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.Quantity:N2}");
                                    row.RelativeItem(1).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.Amount:N2}");
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

        private string EscapeHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&#39;");
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            SetDateRange(selectedDateRange);
        }

        public class SalesItemTypeByItemDetail
        {
            public string? BranchID { get; set; }
            public string? ItemName { get; set; }
            public decimal Quantity { get; set; }
            public decimal Amount { get; set; }
        }

        private bool showExportDropdown = false;

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
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
    }
}