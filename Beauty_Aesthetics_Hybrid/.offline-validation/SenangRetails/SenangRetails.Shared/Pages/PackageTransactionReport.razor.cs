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
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.FileDownloadService;

namespace SenangRetails.Shared.Components.Reports
{
    public partial class PackageTransactionReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private string selectedTransactionType = "All";
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Data collections
        private List<PackageMovement> packageMovements = new();
        private List<PackageMovement> filteredMovements = new();

        // Filter properties
        private string dateFilter = string.Empty;
        private string docNoFilter = string.Empty;
        private string customerFilter = string.Empty;
        private string descriptionFilter = string.Empty;
        private string packageFilter = string.Empty;
        private string quantityFilter = string.Empty;
        private string valueFilter = string.Empty;

        // Summary totals
        private int totalTransactions = 0;
        private decimal totalQuantity = 0;
        private decimal totalValue = 0;

        // Initialize QuestPDF license
        static PackageTransactionReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private void ApplyFilters()
        {
            if (packageMovements == null || !packageMovements.Any())
            {
                filteredMovements = new List<PackageMovement>();
                return;
            }

            var query = packageMovements.AsEnumerable();

            // Date filter
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                query = query.Where(x => x.FinancialDate.ToString("dd/MM/yyyy").Contains(dateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Document No filter
            if (!string.IsNullOrWhiteSpace(docNoFilter))
            {
                query = query.Where(x => x.DisplayCode != null && x.DisplayCode.Contains(docNoFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Customer filter
            if (!string.IsNullOrWhiteSpace(customerFilter))
            {
                query = query.Where(x => x.CustomerName != null && x.CustomerName.Contains(customerFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Description filter
            if (!string.IsNullOrWhiteSpace(descriptionFilter))
            {
                query = query.Where(x => x.Description != null && x.Description.Contains(descriptionFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Package filter
            if (!string.IsNullOrWhiteSpace(packageFilter))
            {
                query = query.Where(x => x.PackageName != null && x.PackageName.Contains(packageFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Quantity filter
            if (!string.IsNullOrWhiteSpace(quantityFilter))
            {
                var qtyConditions = ParseNumericFilter(quantityFilter);
                if (qtyConditions.HasValue)
                {
                    if (qtyConditions.Value.min.HasValue)
                        query = query.Where(x => x.Quantity >= qtyConditions.Value.min.Value);
                    if (qtyConditions.Value.max.HasValue)
                        query = query.Where(x => x.Quantity <= qtyConditions.Value.max.Value);
                }
            }

            // Value filter
            if (!string.IsNullOrWhiteSpace(valueFilter))
            {
                var valueConditions = ParseNumericFilter(valueFilter);
                if (valueConditions.HasValue)
                {
                    if (valueConditions.Value.min.HasValue)
                        query = query.Where(x => x.TotalPrice >= valueConditions.Value.min.Value);
                    if (valueConditions.Value.max.HasValue)
                        query = query.Where(x => x.TotalPrice <= valueConditions.Value.max.Value);
                }
            }

            filteredMovements = query.ToList();
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
            return !string.IsNullOrWhiteSpace(dateFilter) ||
                   !string.IsNullOrWhiteSpace(docNoFilter) ||
                   !string.IsNullOrWhiteSpace(customerFilter) ||
                   !string.IsNullOrWhiteSpace(descriptionFilter) ||
                   !string.IsNullOrWhiteSpace(packageFilter) ||
                   !string.IsNullOrWhiteSpace(quantityFilter) ||
                   !string.IsNullOrWhiteSpace(valueFilter);
        }

        private void ClearFilters()
        {
            dateFilter = string.Empty;
            docNoFilter = string.Empty;
            customerFilter = string.Empty;
            descriptionFilter = string.Empty;
            packageFilter = string.Empty;
            quantityFilter = string.Empty;
            valueFilter = string.Empty;
            ApplyFilters();
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

                var movementType = selectedTransactionType == "All" ? "All" : selectedTransactionType;
                var apiResponse = await ReportService.GetPackageMovementAsync(
                    "All",  // customerID = All
                    startDateTime,
                    endDateTime,
                    branchId,
                    movementType);

                if (apiResponse != null && apiResponse.Result != null)
                {
                    packageMovements = apiResponse.Result;

                    // Calculate totals
                    totalTransactions = packageMovements.Count;
                    totalQuantity = packageMovements.Sum(x => x.Quantity);
                    totalValue = packageMovements.Sum(x => x.TotalPrice);

                    ApplyFilters();
                }
                else
                {
                    packageMovements = new List<PackageMovement>();
                    filteredMovements = new List<PackageMovement>();
                    totalTransactions = 0;
                    totalQuantity = 0;
                    totalValue = 0;

                    if (apiResponse == null)
                    {
                        exportMessage = LangSvc.GetText("Failed to load data from API");
                        alertType = "error";
                    }
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                packageMovements = new List<PackageMovement>();
                filteredMovements = new List<PackageMovement>();
                totalTransactions = 0;
                totalQuantity = 0;
                totalValue = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task SelectTransactionType(string type)
        {
            selectedTransactionType = type;
            ClearFilters();
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

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMovements.Any() ? filteredMovements : packageMovements;

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
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Transaction Type\",\"{selectedTransactionType}\",");
            csv.AppendLine($"\"Total Transactions\",,{totalTransactions.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Quantity\",,{totalQuantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Value\",,{totalValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(dateFilter))
                    csv.AppendLine($"\"  - Date Filter\",\"{EscapeCsvValue(dateFilter)}\",");
                if (!string.IsNullOrWhiteSpace(docNoFilter))
                    csv.AppendLine($"\"  - Doc No Filter\",\"{EscapeCsvValue(docNoFilter)}\",");
                if (!string.IsNullOrWhiteSpace(customerFilter))
                    csv.AppendLine($"\"  - Customer Filter\",\"{EscapeCsvValue(customerFilter)}\",");
                if (!string.IsNullOrWhiteSpace(descriptionFilter))
                    csv.AppendLine($"\"  - Description Filter\",\"{EscapeCsvValue(descriptionFilter)}\",");
                if (!string.IsNullOrWhiteSpace(packageFilter))
                    csv.AppendLine($"\"  - Package Filter\",\"{EscapeCsvValue(packageFilter)}\",");
                if (!string.IsNullOrWhiteSpace(quantityFilter))
                    csv.AppendLine($"\"  - Quantity Filter\",\"{EscapeCsvValue(quantityFilter)}\",");
                if (!string.IsNullOrWhiteSpace(valueFilter))
                    csv.AppendLine($"\"  - Value Filter\",\"{EscapeCsvValue(valueFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Date\",\"Doc No\",\"Customer\",\"Description\",\"Package Name\",\"Quantity\",\"Value\"");

            foreach (var movement in dataToExport)
            {
                csv.AppendLine($"\"{movement.FinancialDate:dd/MM/yyyy}\",\"{EscapeCsvValue(movement.DisplayCode)}\",\"{EscapeCsvValue(movement.CustomerName)}\",\"{EscapeCsvValue(movement.Description)}\",\"{EscapeCsvValue(movement.PackageName)}\",{movement.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{movement.TotalPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"PackageTransaction_{selectedTransactionType}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
            var csvContent = csv.ToString();

            // Convert string content to Base64
            var csvBytes = Encoding.UTF8.GetBytes(csvContent);
            var base64Content = Convert.ToBase64String(csvBytes);

            // Use the file download service instead of JS
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
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMovements.Any() ? filteredMovements : packageMovements;

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
                var fileName = $"PackageTransaction_{selectedTransactionType}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<PackageMovement> dataToExport)
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
                        .Text("Package Transaction Report")
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
                                text.Span($"Branch: {branchName} | ");
                                text.Span($"Date Range: {GetDateRangeText()} | ");
                                text.Span($"Transaction Type: {selectedTransactionType}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(dateFilter))
                                    column.Item().Text($"  - Date: {dateFilter}");
                                if (!string.IsNullOrWhiteSpace(docNoFilter))
                                    column.Item().Text($"  - Doc No: {docNoFilter}");
                                if (!string.IsNullOrWhiteSpace(customerFilter))
                                    column.Item().Text($"  - Customer: {customerFilter}");
                                if (!string.IsNullOrWhiteSpace(descriptionFilter))
                                    column.Item().Text($"  - Description: {descriptionFilter}");
                                if (!string.IsNullOrWhiteSpace(packageFilter))
                                    column.Item().Text($"  - Package: {packageFilter}");
                                if (!string.IsNullOrWhiteSpace(quantityFilter))
                                    column.Item().Text($"  - Quantity: {quantityFilter}");
                                if (!string.IsNullOrWhiteSpace(valueFilter))
                                    column.Item().Text($"  - Value: {valueFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Transactions: {totalTransactions:N0} | ");
                                text.Span($"Total Quantity: {totalQuantity:N0} | ");
                                text.Span($"Total Value: RM {totalValue:N2} | ");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table header - 7 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Date").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Doc No").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Customer").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Description").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Package").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Qty").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.7f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Value").FontColor(Colors.White).Bold().FontSize(7);
                            });

                            foreach (var movement in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(movement.FinancialDate.ToString("dd/MM/yyyy")).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(movement.DisplayCode).FontSize(7);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(movement.CustomerName).FontSize(7);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(movement.Description).FontSize(7);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(movement.PackageName).FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text(movement.Quantity.ToString("N0")).FontSize(7);
                                    row.RelativeItem(0.7f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text(movement.TotalPrice.ToString("N2")).FontSize(7);
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

        public class PackageMovement
        {
            public DateTime FinancialDate { get; set; }
            public string DisplayCode { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string PackageName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal TotalPrice { get; set; }
        }
    }
}