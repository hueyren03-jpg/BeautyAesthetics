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
    public partial class CollectionReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        private List<SalesCollectionSummaryItem> summaryData = new();
        private Dictionary<string, decimal> paymentTypeSummaries = new();
        private Dictionary<string, decimal> filteredPaymentTypes = new();
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private decimal totalAmount = 0;
        private bool showExportDropdown = false;

        // Filter properties
        private string paymentTypeFilter = string.Empty;
        private string amountFilter = string.Empty;

        // Initialize QuestPDF license
        static CollectionReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
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

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
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

                var apiResponse = await ReportService.GetSalesCollectionSummaryAsync(
                    startDateTime,
                    endDateTime,
                    branchId);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    summaryData = apiResponse.Result.ToList();

                    paymentTypeSummaries = new Dictionary<string, decimal>();
                    totalAmount = 0;

                    foreach (var item in summaryData)
                    {
                        if (!string.IsNullOrEmpty(item.POSPaymentTypeName))
                        {
                            var paymentType = item.POSPaymentTypeName;
                            var amount = item.Amount;

                            paymentTypeSummaries[paymentType] = amount;
                            totalAmount += amount;
                        }
                    }

                    paymentTypeSummaries = paymentTypeSummaries
                        .OrderBy(x => summaryData.FirstOrDefault(s => s.POSPaymentTypeName == x.Key)?.Sorting ?? 0)
                        .ToDictionary(x => x.Key, x => x.Value);

                    // Apply filters after loading data
                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResponse?.Message ?? "Failed to load collection data";
                    alertType = "warning";
                    paymentTypeSummaries = new Dictionary<string, decimal>();
                    filteredPaymentTypes = new Dictionary<string, decimal>();
                    totalAmount = 0;
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                paymentTypeSummaries = new Dictionary<string, decimal>();
                filteredPaymentTypes = new Dictionary<string, decimal>();
                totalAmount = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (paymentTypeSummaries == null || !paymentTypeSummaries.Any())
            {
                filteredPaymentTypes = new Dictionary<string, decimal>();
                return;
            }

            var query = paymentTypeSummaries.AsEnumerable();

            // Apply payment type filter (case-insensitive contains)
            if (!string.IsNullOrWhiteSpace(paymentTypeFilter))
            {
                query = query.Where(x => GetPaymentTypeDisplayName(x.Key) != null &&
                    GetPaymentTypeDisplayName(x.Key).Contains(paymentTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply amount filter
            if (!string.IsNullOrWhiteSpace(amountFilter))
            {
                var amountConditions = ParseNumericFilter(amountFilter);
                if (amountConditions.HasValue)
                {
                    if (amountConditions.Value.min.HasValue)
                        query = query.Where(x => x.Value >= amountConditions.Value.min.Value);
                    if (amountConditions.Value.max.HasValue)
                        query = query.Where(x => x.Value <= amountConditions.Value.max.Value);
                }
            }

            filteredPaymentTypes = query.ToDictionary(x => x.Key, x => x.Value);
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
            return !string.IsNullOrWhiteSpace(paymentTypeFilter) ||
                   !string.IsNullOrWhiteSpace(amountFilter);
        }

        private void ClearFilters()
        {
            paymentTypeFilter = string.Empty;
            amountFilter = string.Empty;
            ApplyFilters();
        }

        private string GetPaymentTypeDisplayName(string paymentType)
        {
            return paymentType switch
            {
                "Cash" => LangSvc.GetText("ReportCash"),
                "TNG" => LangSvc.GetText("ReportTNG"),
                "Touch n Go" => LangSvc.GetText("ReportTNG"),
                "Credit Card" => LangSvc.GetText("ReportCreditCard"),
                "Debit Card" => LangSvc.GetText("ReportDebitCard"),
                "Online Banking" => LangSvc.GetText("ReportOnlineBanking"),
                "QR Pay" => LangSvc.GetText("ReportQRPay"),
                "Visa" => "Visa",
                "Mastercard" => "Mastercard",
                "Amex" => "American Express",
                _ => paymentType
            };
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

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredPaymentTypes.Any() ? filteredPaymentTypes : paymentTypeSummaries;

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

            // Report Summary Section
            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Total Collection\",,{totalAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Payment Types\",,{dataToExport.Count.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            // Add filter information if filters are active
            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(paymentTypeFilter))
                    csv.AppendLine($"\"  - Payment Type Filter\",\"{EscapeCsvValue(paymentTypeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(amountFilter))
                    csv.AppendLine($"\"  - Amount Filter\",\"{EscapeCsvValue(amountFilter)}\",");
            }

            csv.AppendLine();

            // Data Header
            csv.AppendLine($"\"{LangSvc.GetText("Collection")}\",\"{LangSvc.GetText("ReportAmount")}\"");

            // Data rows
            foreach (var paymentType in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(GetPaymentTypeDisplayName(paymentType.Key))}\",{paymentType.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"CollectionReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredPaymentTypes.Any() ? filteredPaymentTypes : paymentTypeSummaries;

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
                var fileName = $"CollectionReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(Dictionary<string, decimal> dataToExport)
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
                    page.Size(PageSizes.A4.Portrait());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(10));

                    page.Header()
                        .AlignCenter()
                        .Text("Collection Report")
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

                                if (!string.IsNullOrWhiteSpace(paymentTypeFilter))
                                    column.Item().Text($"  - Payment Type: {paymentTypeFilter}");
                                if (!string.IsNullOrWhiteSpace(amountFilter))
                                    column.Item().Text($"  - Amount: {amountFilter}");

                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Collection: RM ").Bold();
                                text.Span($"{totalAmount:N2}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Payment Types: ").Bold();
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
                                row.RelativeItem(1f).Padding(8).Background(Colors.Brown.Medium).Text("Collection").FontColor(Colors.White).Bold();
                                row.RelativeItem(0.8f).Padding(8).Background(Colors.Brown.Medium).AlignRight().Text("Amount (RM)").FontColor(Colors.White).Bold();
                            });

                            // Table Rows
                            foreach (var paymentType in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1f).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text(GetPaymentTypeDisplayName(paymentType.Key));
                                    row.RelativeItem(0.8f).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{paymentType.Value:N2}");
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

        public class SalesCollectionSummaryItem
        {
            public string? POSPaymentTypeName { get; set; }
            public int Sorting { get; set; }
            public decimal Amount { get; set; }
            public string? CreditCardPaymentType { get; set; }
        }
    }
}