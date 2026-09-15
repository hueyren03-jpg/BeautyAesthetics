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
    public partial class StaffSalesReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        private List<EmployeeSalesByItemType> employeeSalesByItemType = new();
        private List<EmployeeSalesByItemType> filteredEmployeeSales = new();
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Filter properties
        private string staffNameFilter = string.Empty;
        private string billCountFilter = string.Empty;
        private string allSalesFilter = string.Empty;
        private string serviceSalesFilter = string.Empty;
        private string productSalesFilter = string.Empty;
        private string packageSalesFilter = string.Empty;
        private string topUpSalesFilter = string.Empty;

        // Summary totals
        private decimal totalAllSales = 0;
        private int totalBillCount = 0;
        private int staffCount = 0;

        // Initialize QuestPDF license
        static StaffSalesReport()
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

                var apiResponse = await ReportService.GetEmployeeSalesByItemTypeAsync(
                    startDateTime,
                    endDateTime,
                    branchId);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    employeeSalesByItemType = apiResponse.Result;

                    totalAllSales = employeeSalesByItemType.Sum(x => x.AllSales);
                    totalBillCount = employeeSalesByItemType.Sum(x => x.BillCount);
                    staffCount = employeeSalesByItemType.Count;

                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResponse?.Message ?? "Failed to load data";
                    alertType = "error";
                    employeeSalesByItemType = new List<EmployeeSalesByItemType>();
                    filteredEmployeeSales = new List<EmployeeSalesByItemType>();
                    totalAllSales = 0;
                    totalBillCount = 0;
                    staffCount = 0;
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                employeeSalesByItemType = new List<EmployeeSalesByItemType>();
                filteredEmployeeSales = new List<EmployeeSalesByItemType>();
                totalAllSales = 0;
                totalBillCount = 0;
                staffCount = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (employeeSalesByItemType == null || !employeeSalesByItemType.Any())
            {
                filteredEmployeeSales = new List<EmployeeSalesByItemType>();
                return;
            }

            var query = employeeSalesByItemType.AsEnumerable();

            // Staff name filter
            if (!string.IsNullOrWhiteSpace(staffNameFilter))
            {
                query = query.Where(x => x.EmployeeName != null && x.EmployeeName.Contains(staffNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Bill count filter
            if (!string.IsNullOrWhiteSpace(billCountFilter))
            {
                var conditions = ParseNumericFilter(billCountFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.BillCount >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.BillCount <= conditions.Value.max.Value);
                }
            }

            // All sales filter
            if (!string.IsNullOrWhiteSpace(allSalesFilter))
            {
                var conditions = ParseNumericFilter(allSalesFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.AllSales >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.AllSales <= conditions.Value.max.Value);
                }
            }

            // Service sales filter
            if (!string.IsNullOrWhiteSpace(serviceSalesFilter))
            {
                var conditions = ParseNumericFilter(serviceSalesFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.ServiceSales >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.ServiceSales <= conditions.Value.max.Value);
                }
            }

            // Product sales filter
            if (!string.IsNullOrWhiteSpace(productSalesFilter))
            {
                var conditions = ParseNumericFilter(productSalesFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.ProductSales >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.ProductSales <= conditions.Value.max.Value);
                }
            }

            // Package sales filter
            if (!string.IsNullOrWhiteSpace(packageSalesFilter))
            {
                var conditions = ParseNumericFilter(packageSalesFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.PackageSales >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.PackageSales <= conditions.Value.max.Value);
                }
            }

            // Top up sales filter
            if (!string.IsNullOrWhiteSpace(topUpSalesFilter))
            {
                var conditions = ParseNumericFilter(topUpSalesFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.CreditSales >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.CreditSales <= conditions.Value.max.Value);
                }
            }

            filteredEmployeeSales = query.ToList();
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
            return !string.IsNullOrWhiteSpace(staffNameFilter) ||
                   !string.IsNullOrWhiteSpace(billCountFilter) ||
                   !string.IsNullOrWhiteSpace(allSalesFilter) ||
                   !string.IsNullOrWhiteSpace(serviceSalesFilter) ||
                   !string.IsNullOrWhiteSpace(productSalesFilter) ||
                   !string.IsNullOrWhiteSpace(packageSalesFilter) ||
                   !string.IsNullOrWhiteSpace(topUpSalesFilter);
        }

        private void ClearFilters()
        {
            staffNameFilter = string.Empty;
            billCountFilter = string.Empty;
            allSalesFilter = string.Empty;
            serviceSalesFilter = string.Empty;
            productSalesFilter = string.Empty;
            packageSalesFilter = string.Empty;
            topUpSalesFilter = string.Empty;
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


            var dataToExport = filteredEmployeeSales.Any() ? filteredEmployeeSales : employeeSalesByItemType;

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
            csv.AppendLine($"\"Total Sales\",,{totalAllSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Bills\",,{totalBillCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Active Staff\",,{staffCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(staffNameFilter))
                    csv.AppendLine($"\"  - Staff Name Filter\",\"{EscapeCsvValue(staffNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(billCountFilter))
                    csv.AppendLine($"\"  - Bill Count Filter\",\"{EscapeCsvValue(billCountFilter)}\",");
                if (!string.IsNullOrWhiteSpace(allSalesFilter))
                    csv.AppendLine($"\"  - All Sales Filter\",\"{EscapeCsvValue(allSalesFilter)}\",");
                if (!string.IsNullOrWhiteSpace(serviceSalesFilter))
                    csv.AppendLine($"\"  - Service Sales Filter\",\"{EscapeCsvValue(serviceSalesFilter)}\",");
                if (!string.IsNullOrWhiteSpace(productSalesFilter))
                    csv.AppendLine($"\"  - Product Sales Filter\",\"{EscapeCsvValue(productSalesFilter)}\",");
                if (!string.IsNullOrWhiteSpace(packageSalesFilter))
                    csv.AppendLine($"\"  - Package Sales Filter\",\"{EscapeCsvValue(packageSalesFilter)}\",");
                if (!string.IsNullOrWhiteSpace(topUpSalesFilter))
                    csv.AppendLine($"\"  - Top Up Sales Filter\",\"{EscapeCsvValue(topUpSalesFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"{LangSvc.GetText("Staff Name")}\",\"{LangSvc.GetText("Bill Count")}\",\"{LangSvc.GetText("All Sales")}\",\"{LangSvc.GetText("Service Sales")}\",\"{LangSvc.GetText("Product Sales")}\",\"{LangSvc.GetText("Package Sales")}\",\"{LangSvc.GetText("Top Up Sales")}\"");

            foreach (var item in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(item.EmployeeName)}\",{item.BillCount},{item.AllSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.ServiceSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.ProductSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.PackageSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.CreditSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"StaffSalesReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredEmployeeSales.Any() ? filteredEmployeeSales : employeeSalesByItemType;

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
                var fileName = $"StaffSalesReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<EmployeeSalesByItemType> dataToExport)
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
                    page.Margin(0.8f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(9));

                    page.Header()
                        .AlignCenter()
                        .Text("Staff Sales Report")
                        .SemiBold().FontSize(18).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(8)
                        .Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Report Summary").Bold().FontSize(11);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Branch: {branchName} | ");
                                text.Span($"Date Range: {GetDateRangeText()}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(staffNameFilter))
                                    column.Item().Text($"  - Staff Name: {staffNameFilter}");
                                if (!string.IsNullOrWhiteSpace(billCountFilter))
                                    column.Item().Text($"  - Bill Count: {billCountFilter}");
                                if (!string.IsNullOrWhiteSpace(allSalesFilter))
                                    column.Item().Text($"  - All Sales: {allSalesFilter}");
                                if (!string.IsNullOrWhiteSpace(serviceSalesFilter))
                                    column.Item().Text($"  - Service Sales: {serviceSalesFilter}");
                                if (!string.IsNullOrWhiteSpace(productSalesFilter))
                                    column.Item().Text($"  - Product Sales: {productSalesFilter}");
                                if (!string.IsNullOrWhiteSpace(packageSalesFilter))
                                    column.Item().Text($"  - Package Sales: {packageSalesFilter}");
                                if (!string.IsNullOrWhiteSpace(topUpSalesFilter))
                                    column.Item().Text($"  - Top Up Sales: {topUpSalesFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Sales: RM {totalAllSales:N2} | ");
                                text.Span($"Total Bills: {totalBillCount:N0} | ");
                                text.Span($"Active Staff: {staffCount} | ");
                                text.Span($"Total Staff: {dataToExport.Count}");
                            });

                            column.Item().PaddingVertical(8).LineHorizontal(1);

                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1.5f).Padding(6).Background(Colors.Brown.Medium).Text("Staff Name").FontColor(Colors.White).Bold().FontSize(9);
                                row.RelativeItem(0.8f).Padding(6).Background(Colors.Brown.Medium).AlignRight().Text("Bill Count").FontColor(Colors.White).Bold().FontSize(9);
                                row.RelativeItem(1.2f).Padding(6).Background(Colors.Brown.Medium).AlignRight().Text("All Sales (RM)").FontColor(Colors.White).Bold().FontSize(9);
                                row.RelativeItem(1.1f).Padding(6).Background(Colors.Brown.Medium).AlignRight().Text("Service (RM)").FontColor(Colors.White).Bold().FontSize(9);
                                row.RelativeItem(1.1f).Padding(6).Background(Colors.Brown.Medium).AlignRight().Text("Product (RM)").FontColor(Colors.White).Bold().FontSize(9);
                                row.RelativeItem(1.1f).Padding(6).Background(Colors.Brown.Medium).AlignRight().Text("Package (RM)").FontColor(Colors.White).Bold().FontSize(9);
                                row.RelativeItem(1.1f).Padding(6).Background(Colors.Brown.Medium).AlignRight().Text("Top Up (RM)").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1.5f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(item.EmployeeName).FontSize(9);
                                    row.RelativeItem(0.8f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.BillCount:N0}").FontSize(9);
                                    row.RelativeItem(1.2f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.AllSales:N2}").FontSize(9);
                                    row.RelativeItem(1.1f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.ServiceSales:N2}").FontSize(9);
                                    row.RelativeItem(1.1f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.ProductSales:N2}").FontSize(9);
                                    row.RelativeItem(1.1f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.PackageSales:N2}").FontSize(9);
                                    row.RelativeItem(1.1f).Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{item.CreditSales:N2}").FontSize(9);
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

        public class EmployeeSalesByItemType
        {
            public string EmployeeName { get; set; } = string.Empty;
            public int BillCount { get; set; }
            public decimal AllSales { get; set; }
            public decimal ProductSales { get; set; }
            public decimal ServiceSales { get; set; }
            public decimal PackageSales { get; set; }
            public decimal CreditSales { get; set; }
        }
    }
}