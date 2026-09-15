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
    public partial class TransactionReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        private List<TransactionDetails> transactions = new();
        private List<TransactionDetails> filteredTransactions = new();
        private decimal totalSales = 0;
        private int totalCustomers = 0;
        private int totalOrders = 0;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool showExportDropdown = false;

        // Filter properties
        private string invoiceFilter = string.Empty;
        private string dateFilter = string.Empty;
        private string memberFilter = string.Empty;
        private string beforeTaxFilter = string.Empty;
        private string taxFilter = string.Empty;
        private string roundingFilter = string.Empty;
        private string afterTaxFilter = string.Empty;
        private string einvoiceTypeFilter = string.Empty;
        private string einvoiceStatusFilter = string.Empty;

        // Initialize QuestPDF license
        static TransactionReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private string GetEInvoiceTypeText(int eInvoiceTypeID)
        {
            return eInvoiceTypeID switch
            {
                0 => "Haven't Submit",
                1 => "Individual E-Invoice",
                2 => "Consolidated E-Invoice",
                _ => "Unknown"
            };
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

                var apiResponse = await ReportService.GetTransactionAsync(
                    branchId,
                    startDateTime,
                    endDateTime);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    transactions = apiResponse.Result.ToList();

                    totalSales = transactions.Sum(x => x.TotalAfterTax);
                    totalCustomers = transactions.Select(x => x.AccountID).Distinct().Count();
                    totalOrders = transactions.Count;

                    ApplyFilters();
                }
                else
                {
                    transactions = new List<TransactionDetails>();
                    filteredTransactions = new List<TransactionDetails>();
                    totalSales = 0;
                    totalCustomers = 0;
                    totalOrders = 0;

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
                transactions = new List<TransactionDetails>();
                filteredTransactions = new List<TransactionDetails>();
                totalSales = 0;
                totalCustomers = 0;
                totalOrders = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (transactions == null || !transactions.Any())
            {
                filteredTransactions = new List<TransactionDetails>();
                return;
            }

            var query = transactions.AsEnumerable();

            // Invoice filter
            if (!string.IsNullOrWhiteSpace(invoiceFilter))
            {
                query = query.Where(x => x.DisplayCode != null && x.DisplayCode.Contains(invoiceFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Date filter
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                query = query.Where(x => x.FinancialDate.ToString("dd/MM/yyyy").Contains(dateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Member filter
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                query = query.Where(x => (x.AccountName ?? "Cash Customer").Contains(memberFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Before Tax filter
            if (!string.IsNullOrWhiteSpace(beforeTaxFilter))
            {
                var conditions = ParseNumericFilter(beforeTaxFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.TotalBeforeTax >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.TotalBeforeTax <= conditions.Value.max.Value);
                }
            }

            // Tax filter
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

            // Rounding filter
            if (!string.IsNullOrWhiteSpace(roundingFilter))
            {
                var conditions = ParseNumericFilter(roundingFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.RoundingAmount >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.RoundingAmount <= conditions.Value.max.Value);
                }
            }

            // After Tax filter
            if (!string.IsNullOrWhiteSpace(afterTaxFilter))
            {
                var conditions = ParseNumericFilter(afterTaxFilter);
                if (conditions.HasValue)
                {
                    if (conditions.Value.min.HasValue)
                        query = query.Where(x => x.TotalAfterTax >= conditions.Value.min.Value);
                    if (conditions.Value.max.HasValue)
                        query = query.Where(x => x.TotalAfterTax <= conditions.Value.max.Value);
                }
            }

            // E-Invoice Type filter
            if (!string.IsNullOrWhiteSpace(einvoiceTypeFilter))
            {
                query = query.Where(x => GetEInvoiceTypeText(x.eInvoiceTypeID).Contains(einvoiceTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // E-Invoice Status filter
            if (!string.IsNullOrWhiteSpace(einvoiceStatusFilter))
            {
                query = query.Where(x => (x.eInvoiceStatus ?? "No Status").Contains(einvoiceStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            filteredTransactions = query.ToList();
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
            return !string.IsNullOrWhiteSpace(invoiceFilter) ||
                   !string.IsNullOrWhiteSpace(dateFilter) ||
                   !string.IsNullOrWhiteSpace(memberFilter) ||
                   !string.IsNullOrWhiteSpace(beforeTaxFilter) ||
                   !string.IsNullOrWhiteSpace(taxFilter) ||
                   !string.IsNullOrWhiteSpace(roundingFilter) ||
                   !string.IsNullOrWhiteSpace(afterTaxFilter) ||
                   !string.IsNullOrWhiteSpace(einvoiceTypeFilter) ||
                   !string.IsNullOrWhiteSpace(einvoiceStatusFilter);
        }

        private void ClearFilters()
        {
            invoiceFilter = string.Empty;
            dateFilter = string.Empty;
            memberFilter = string.Empty;
            beforeTaxFilter = string.Empty;
            taxFilter = string.Empty;
            roundingFilter = string.Empty;
            afterTaxFilter = string.Empty;
            einvoiceTypeFilter = string.Empty;
            einvoiceStatusFilter = string.Empty;
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

            var dataToExport = filteredTransactions.Any() ? filteredTransactions : transactions;

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
            csv.AppendLine($"\"Total Sales\",,{totalSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Customers\",,{totalCustomers.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Orders\",,{totalOrders.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(invoiceFilter))
                    csv.AppendLine($"\"  - Invoice Filter\",\"{EscapeCsvValue(invoiceFilter)}\",");
                if (!string.IsNullOrWhiteSpace(dateFilter))
                    csv.AppendLine($"\"  - Date Filter\",\"{EscapeCsvValue(dateFilter)}\",");
                if (!string.IsNullOrWhiteSpace(memberFilter))
                    csv.AppendLine($"\"  - Member Filter\",\"{EscapeCsvValue(memberFilter)}\",");
                if (!string.IsNullOrWhiteSpace(beforeTaxFilter))
                    csv.AppendLine($"\"  - Before Tax Filter\",\"{EscapeCsvValue(beforeTaxFilter)}\",");
                if (!string.IsNullOrWhiteSpace(taxFilter))
                    csv.AppendLine($"\"  - Tax Filter\",\"{EscapeCsvValue(taxFilter)}\",");
                if (!string.IsNullOrWhiteSpace(roundingFilter))
                    csv.AppendLine($"\"  - Rounding Filter\",\"{EscapeCsvValue(roundingFilter)}\",");
                if (!string.IsNullOrWhiteSpace(afterTaxFilter))
                    csv.AppendLine($"\"  - After Tax Filter\",\"{EscapeCsvValue(afterTaxFilter)}\",");
                if (!string.IsNullOrWhiteSpace(einvoiceTypeFilter))
                    csv.AppendLine($"\"  - E-Invoice Type Filter\",\"{EscapeCsvValue(einvoiceTypeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(einvoiceStatusFilter))
                    csv.AppendLine($"\"  - E-Invoice Status Filter\",\"{EscapeCsvValue(einvoiceStatusFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Invoice No\",\"Date\",\"Member\",\"Phone\",\"Total Before Tax\",\"Tax\",\"Rounding\",\"Total After Tax\",\"E-Invoice Type\",\"E-Invoice Status\"");

            foreach (var transaction in dataToExport)
            {
                var memberName = string.IsNullOrEmpty(transaction.AccountName) ? "Cash Customer" : transaction.AccountName;
                var eInvoiceStatus = string.IsNullOrEmpty(transaction.eInvoiceStatus) ? "No Status" : transaction.eInvoiceStatus;

                csv.AppendLine($"\"{EscapeCsvValue(transaction.DisplayCode)}\",\"{transaction.FinancialDate:dd/MM/yyyy}\",\"{EscapeCsvValue(memberName)}\",\"{transaction.Phone}\",{transaction.TotalBeforeTax.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{transaction.TaxAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{transaction.RoundingAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{transaction.TotalAfterTax.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},\"{GetEInvoiceTypeText(transaction.eInvoiceTypeID)}\",\"{EscapeCsvValue(eInvoiceStatus)}\"");
            }

            var fileName = $"TransactionReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredTransactions.Any() ? filteredTransactions : transactions;

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
                var fileName = $"TransactionReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<TransactionDetails> dataToExport)
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
                        .Text("Transaction Report")
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
                                if (!string.IsNullOrWhiteSpace(invoiceFilter))
                                    column.Item().Text($"  - Invoice: {invoiceFilter}");
                                if (!string.IsNullOrWhiteSpace(dateFilter))
                                    column.Item().Text($"  - Date: {dateFilter}");
                                if (!string.IsNullOrWhiteSpace(memberFilter))
                                    column.Item().Text($"  - Member: {memberFilter}");
                                if (!string.IsNullOrWhiteSpace(beforeTaxFilter))
                                    column.Item().Text($"  - Before Tax: {beforeTaxFilter}");
                                if (!string.IsNullOrWhiteSpace(taxFilter))
                                    column.Item().Text($"  - Tax: {taxFilter}");
                                if (!string.IsNullOrWhiteSpace(roundingFilter))
                                    column.Item().Text($"  - Rounding: {roundingFilter}");
                                if (!string.IsNullOrWhiteSpace(afterTaxFilter))
                                    column.Item().Text($"  - After Tax: {afterTaxFilter}");
                                if (!string.IsNullOrWhiteSpace(einvoiceTypeFilter))
                                    column.Item().Text($"  - E-Invoice Type: {einvoiceTypeFilter}");
                                if (!string.IsNullOrWhiteSpace(einvoiceStatusFilter))
                                    column.Item().Text($"  - E-Invoice Status: {einvoiceStatusFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Sales: {totalSales:N2} | ");
                                text.Span($"Total Customers: {totalCustomers:N0} | ");
                                text.Span($"Total Orders: {totalOrders:N0} | ");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).Text("Invoice No").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Date").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Member").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Before Tax").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Tax").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Rounding").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("After Tax").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("E-Invoice Type").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("E-Invoice Status").FontColor(Colors.White).Bold().FontSize(8);
                            });

                            foreach (var transaction in dataToExport)
                            {
                                var memberName = string.IsNullOrEmpty(transaction.AccountName) ? "Cash Customer" : transaction.AccountName;
                                var eInvoiceStatus = string.IsNullOrEmpty(transaction.eInvoiceStatus) ? "No Status" : transaction.eInvoiceStatus;

                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(transaction.DisplayCode).FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text($"{transaction.FinancialDate:dd/MM/yyyy}").FontSize(8);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(memberName).FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{transaction.TotalBeforeTax:N2}").FontSize(8);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{transaction.TaxAmount:N2}").FontSize(8);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{transaction.RoundingAmount:N2}").FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{transaction.TotalAfterTax:N2}").FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(GetEInvoiceTypeText(transaction.eInvoiceTypeID)).FontSize(8);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(eInvoiceStatus).FontSize(8);
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

        public class TransactionDetails
        {
            public string DisplayCode { get; set; } = string.Empty;
            public DateTime FinancialDate { get; set; }
            public string AccountID { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public decimal TotalBeforeTax { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal RoundingAmount { get; set; }
            public decimal TotalAfterTax { get; set; }
            public string Phone { get; set; } = string.Empty;
            public int eInvoiceTypeID { get; set; }
            public string eInvoiceStatus { get; set; } = string.Empty;
        }
    }
}