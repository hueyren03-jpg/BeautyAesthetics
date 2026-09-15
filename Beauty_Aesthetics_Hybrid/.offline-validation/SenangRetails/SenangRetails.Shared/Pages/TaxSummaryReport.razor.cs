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

namespace SenangRetails.Shared.Pages
{
    public partial class TaxSummaryReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        private List<TaxPayableSummaryItem> taxPayableData = new();
        private List<TaxPayableSummaryItem> filteredData = new();
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Filter properties
        private string dateFilter = string.Empty;
        private string taxTypeFilter = string.Empty;
        private string taxCategoryFilter = string.Empty;
        private string taxCodeFilter = string.Empty;
        private string percentageFilter = string.Empty;
        private string beforeTaxFilter = string.Empty;
        private string taxAmountFilter = string.Empty;
        private string afterTaxFilter = string.Empty;

        // Summary totals
        private decimal totalBeforeTax = 0;
        private decimal taxAmount = 0;
        private decimal totalAfterTax = 0;
        private decimal roundingAmount = 0;

        static TaxSummaryReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private string GetTaxTypeIdDisplay()
        {
            return AppState.CurrentBranch?.TaxTypeID ?? "";
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
                var taxGroupId = GetTaxTypeIdDisplay();

                var apiResponse = await ReportService.GetGSTPayableSummaryByDateAsync(
                    startDateTime,
                    endDateTime,
                    branchId,
                    taxGroupId);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    taxPayableData = apiResponse.Result
                        .OrderBy(x => x.TransDate)
                        .ThenBy(x => x.TaxCodeID)
                        .ToList();

                    totalBeforeTax = taxPayableData.Sum(x => x.TotalBeforeGST);
                    taxAmount = taxPayableData.Sum(x => x.TaxAmount);
                    totalAfterTax = totalBeforeTax + taxAmount;
                    roundingAmount = 0;

                    ApplyFilters();
                }
                else
                {
                    taxPayableData = new List<TaxPayableSummaryItem>();
                    filteredData = new List<TaxPayableSummaryItem>();
                    totalBeforeTax = 0;
                    taxAmount = 0;
                    totalAfterTax = 0;
                    roundingAmount = 0;

                    if (apiResponse?.Message != null)
                    {
                        exportMessage = apiResponse.Message;
                        alertType = "warning";
                    }
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                taxPayableData = new List<TaxPayableSummaryItem>();
                filteredData = new List<TaxPayableSummaryItem>();
                totalBeforeTax = 0;
                taxAmount = 0;
                totalAfterTax = 0;
                roundingAmount = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (taxPayableData == null || !taxPayableData.Any())
            {
                filteredData = new List<TaxPayableSummaryItem>();
                return;
            }

            var query = taxPayableData.AsEnumerable();

            // Date filter
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                query = query.Where(x => x.TransDate.ToString("dd/MM/yyyy").Contains(dateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Tax Type filter
            if (!string.IsNullOrWhiteSpace(taxTypeFilter))
            {
                query = query.Where(x => x.GSTTypeID != null && x.GSTTypeID.Contains(taxTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Tax Category filter
            if (!string.IsNullOrWhiteSpace(taxCategoryFilter))
            {
                query = query.Where(x => x.TaxCategory != null && x.TaxCategory.Contains(taxCategoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Tax Code filter
            if (!string.IsNullOrWhiteSpace(taxCodeFilter))
            {
                query = query.Where(x => x.TaxCodeID != null && x.TaxCodeID.Contains(taxCodeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Percentage filter
            if (!string.IsNullOrWhiteSpace(percentageFilter))
            {
                var percentageConditions = ParseNumericFilter(percentageFilter);
                if (percentageConditions.HasValue)
                {
                    var percentageValue = percentageConditions.Value;
                    if (percentageValue.min.HasValue)
                        query = query.Where(x => (x.TaxPercentage * 100) >= percentageValue.min.Value);
                    if (percentageValue.max.HasValue)
                        query = query.Where(x => (x.TaxPercentage * 100) <= percentageValue.max.Value);
                }
            }

            // Before Tax filter
            if (!string.IsNullOrWhiteSpace(beforeTaxFilter))
            {
                var beforeTaxConditions = ParseNumericFilter(beforeTaxFilter);
                if (beforeTaxConditions.HasValue)
                {
                    if (beforeTaxConditions.Value.min.HasValue)
                        query = query.Where(x => x.TotalBeforeGST >= beforeTaxConditions.Value.min.Value);
                    if (beforeTaxConditions.Value.max.HasValue)
                        query = query.Where(x => x.TotalBeforeGST <= beforeTaxConditions.Value.max.Value);
                }
            }

            // Tax Amount filter
            if (!string.IsNullOrWhiteSpace(taxAmountFilter))
            {
                var taxAmountConditions = ParseNumericFilter(taxAmountFilter);
                if (taxAmountConditions.HasValue)
                {
                    if (taxAmountConditions.Value.min.HasValue)
                        query = query.Where(x => x.TaxAmount >= taxAmountConditions.Value.min.Value);
                    if (taxAmountConditions.Value.max.HasValue)
                        query = query.Where(x => x.TaxAmount <= taxAmountConditions.Value.max.Value);
                }
            }

            // After Tax filter
            if (!string.IsNullOrWhiteSpace(afterTaxFilter))
            {
                var afterTaxConditions = ParseNumericFilter(afterTaxFilter);
                if (afterTaxConditions.HasValue)
                {
                    if (afterTaxConditions.Value.min.HasValue)
                        query = query.Where(x => (x.TotalBeforeGST + x.TaxAmount) >= afterTaxConditions.Value.min.Value);
                    if (afterTaxConditions.Value.max.HasValue)
                        query = query.Where(x => (x.TotalBeforeGST + x.TaxAmount) <= afterTaxConditions.Value.max.Value);
                }
            }

            filteredData = query.ToList();
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
                   !string.IsNullOrWhiteSpace(taxTypeFilter) ||
                   !string.IsNullOrWhiteSpace(taxCategoryFilter) ||
                   !string.IsNullOrWhiteSpace(taxCodeFilter) ||
                   !string.IsNullOrWhiteSpace(percentageFilter) ||
                   !string.IsNullOrWhiteSpace(beforeTaxFilter) ||
                   !string.IsNullOrWhiteSpace(taxAmountFilter) ||
                   !string.IsNullOrWhiteSpace(afterTaxFilter);
        }

        private void ClearFilters()
        {
            dateFilter = string.Empty;
            taxTypeFilter = string.Empty;
            taxCategoryFilter = string.Empty;
            taxCodeFilter = string.Empty;
            percentageFilter = string.Empty;
            beforeTaxFilter = string.Empty;
            taxAmountFilter = string.Empty;
            afterTaxFilter = string.Empty;
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

            var dataToExport = filteredData.Any() ? filteredData : taxPayableData;

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
            csv.AppendLine($"\"Total Before Tax\",,{totalBeforeTax.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Tax Amount\",,{taxAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total After Tax\",,{totalAfterTax.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Rounding Amount\",,{roundingAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(dateFilter))
                    csv.AppendLine($"\"  - Date Filter\",\"{EscapeCsvValue(dateFilter)}\",");
                if (!string.IsNullOrWhiteSpace(taxTypeFilter))
                    csv.AppendLine($"\"  - Tax Type Filter\",\"{EscapeCsvValue(taxTypeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(taxCategoryFilter))
                    csv.AppendLine($"\"  - Tax Category Filter\",\"{EscapeCsvValue(taxCategoryFilter)}\",");
                if (!string.IsNullOrWhiteSpace(taxCodeFilter))
                    csv.AppendLine($"\"  - Tax Code Filter\",\"{EscapeCsvValue(taxCodeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(percentageFilter))
                    csv.AppendLine($"\"  - Percentage Filter\",\"{EscapeCsvValue(percentageFilter)}\",");
                if (!string.IsNullOrWhiteSpace(beforeTaxFilter))
                    csv.AppendLine($"\"  - Before Tax Filter\",\"{EscapeCsvValue(beforeTaxFilter)}\",");
                if (!string.IsNullOrWhiteSpace(taxAmountFilter))
                    csv.AppendLine($"\"  - Tax Amount Filter\",\"{EscapeCsvValue(taxAmountFilter)}\",");
                if (!string.IsNullOrWhiteSpace(afterTaxFilter))
                    csv.AppendLine($"\"  - After Tax Filter\",\"{EscapeCsvValue(afterTaxFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"{LangSvc.GetText("Date")}\",\"{LangSvc.GetText("Tax Type ID")}\",\"{LangSvc.GetText("Tax Category")}\",\"{LangSvc.GetText("Tax Code ID")}\",\"{LangSvc.GetText("Tax Percentage")}\",\"{LangSvc.GetText("Total Before Tax")}\",\"{LangSvc.GetText("Tax Amount")}\",\"{LangSvc.GetText("Total After Tax")}\"");

            foreach (var item in dataToExport)
            {
                var totalAfterTaxItem = item.TotalBeforeGST + item.TaxAmount;
                var taxPercentage = (item.TaxPercentage * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                csv.AppendLine($"\"{item.TransDate:dd/MM/yyyy}\",\"{EscapeCsvValue(item.GSTTypeID)}\",\"{EscapeCsvValue(item.TaxCategory)}\",\"{EscapeCsvValue(item.TaxCodeID)}\",{taxPercentage},{item.TotalBeforeGST.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.TaxAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{totalAfterTaxItem.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"TaxSummary_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredData.Any() ? filteredData : taxPayableData;

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
                var fileName = $"TaxSummary_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<TaxPayableSummaryItem> dataToExport)
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
                        .Text("Tax Summary Report")
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
                                text.Span($"Date Range: {GetDateRangeText()}");
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
                                if (!string.IsNullOrWhiteSpace(taxTypeFilter))
                                    column.Item().Text($"  - Tax Type: {taxTypeFilter}");
                                if (!string.IsNullOrWhiteSpace(taxCategoryFilter))
                                    column.Item().Text($"  - Tax Category: {taxCategoryFilter}");
                                if (!string.IsNullOrWhiteSpace(taxCodeFilter))
                                    column.Item().Text($"  - Tax Code: {taxCodeFilter}");
                                if (!string.IsNullOrWhiteSpace(percentageFilter))
                                    column.Item().Text($"  - Percentage: {percentageFilter}");
                                if (!string.IsNullOrWhiteSpace(beforeTaxFilter))
                                    column.Item().Text($"  - Before Tax: {beforeTaxFilter}");
                                if (!string.IsNullOrWhiteSpace(taxAmountFilter))
                                    column.Item().Text($"  - Tax Amount: {taxAmountFilter}");
                                if (!string.IsNullOrWhiteSpace(afterTaxFilter))
                                    column.Item().Text($"  - After Tax: {afterTaxFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Before Tax: {totalBeforeTax:N2} | ");
                                text.Span($"Tax Amount: {taxAmount:N2} | ");
                                text.Span($"Total After Tax: {totalAfterTax:N2} | ");
                                text.Span($"Rounding Amount: {roundingAmount:N2}");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).Text("Date").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Tax Type ID").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).Text("Tax Category").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Tax Code ID").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Tax %").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Before Tax").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Tax Amt").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("After Tax").FontColor(Colors.White).Bold().FontSize(8);
                            });

                            foreach (var item in dataToExport)
                            {
                                var totalAfterTaxItem = item.TotalBeforeGST + item.TaxAmount;
                                var taxPercentage = (item.TaxPercentage * 100).ToString("N2") + "%";

                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.TransDate.ToString("dd/MM/yyyy")).FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.GSTTypeID).FontSize(8);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.TaxCategory).FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.TaxCodeID).FontSize(8);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text(taxPercentage).FontSize(8);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.TotalBeforeGST:N2}").FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.TaxAmount:N2}").FontSize(8);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{totalAfterTaxItem:N2}").FontSize(8);
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
    }

    public class TaxPayableSummaryItem
    {
        public string GSTTypeID { get; set; } = string.Empty;
        public string TaxCategory { get; set; } = string.Empty;
        public string TaxCodeID { get; set; } = string.Empty;
        public decimal TaxPercentage { get; set; }
        public DateTime TransDate { get; set; }
        public string BranchID { get; set; } = string.Empty;
        public decimal TotalBeforeGST { get; set; }
        public decimal TaxAmount { get; set; }
    }
}