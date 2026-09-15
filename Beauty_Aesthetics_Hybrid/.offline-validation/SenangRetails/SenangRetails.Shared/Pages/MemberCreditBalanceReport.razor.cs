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
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.FileDownloadService;

namespace SenangRetails.Shared.Components.Reports
{
    public partial class MemberCreditBalanceReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime selectedDate = DateTime.Today;
        private string selectedStatusTab = "ActiveCredits";
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Data
        private List<MemberCreditBalance> creditData = new();
        private List<MemberCreditBalance> filteredCreditData = new();

        // Filter properties
        private string creditAcFilter = string.Empty;
        private string memberTypeFilter = string.Empty;
        private string purchaseDateFilter = string.Empty;
        private string invoiceFilter = string.Empty;
        private string customerFilter = string.Empty;
        private string itemNameFilter = string.Empty;
        private string expiryDateFilter = string.Empty;
        private string pvFilter = string.Empty;

        // Summary totals
        private int totalCredits = 0;
        private string currentSummaryValue = "0";

        static MemberCreditBalanceReport()
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

        private string GetCurrentSummaryLabel()
        {
            return selectedStatusTab switch
            {
                "ActiveCredits" => LangSvc.GetText("Total Active PV"),
                "ExpiredCredits" => LangSvc.GetText("Total Expired PV"),
                "FullyRedeemedCredits" => LangSvc.GetText("Total Fully Redeemed"),
                _ => LangSvc.GetText("Total")
            };
        }

        private string GetExpiryDateClass(DateTime expiryDate)
        {
            var daysRemaining = (expiryDate - DateTime.Today).Days;
            if (daysRemaining < 0) return "expired";
            if (daysRemaining <= 7) return "expiring-soon";
            return "";
        }

        private void ApplyFilters()
        {
            if (creditData == null || !creditData.Any())
            {
                filteredCreditData = new List<MemberCreditBalance>();
                return;
            }

            var query = creditData.AsEnumerable();

            // Credit A/C filter
            if (!string.IsNullOrWhiteSpace(creditAcFilter))
            {
                query = query.Where(x => x.ARAPOutstandingID != null && x.ARAPOutstandingID.Contains(creditAcFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Member Type filter
            if (!string.IsNullOrWhiteSpace(memberTypeFilter))
            {
                query = query.Where(x => x.MemberTypeID != null && x.MemberTypeID.Contains(memberTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Purchase Date filter
            if (!string.IsNullOrWhiteSpace(purchaseDateFilter))
            {
                query = query.Where(x => x.PurchaseDate.ToString("dd/MM/yyyy").Contains(purchaseDateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Invoice filter
            if (!string.IsNullOrWhiteSpace(invoiceFilter))
            {
                query = query.Where(x => x.PurchaseInvoiceNo != null && x.PurchaseInvoiceNo.Contains(invoiceFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Customer filter
            if (!string.IsNullOrWhiteSpace(customerFilter))
            {
                query = query.Where(x => x.CustomerName != null && x.CustomerName.Contains(customerFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Item Name filter
            if (!string.IsNullOrWhiteSpace(itemNameFilter))
            {
                query = query.Where(x => x.Description != null && x.Description.Contains(itemNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Expiry Date filter
            if (!string.IsNullOrWhiteSpace(expiryDateFilter))
            {
                query = query.Where(x => x.ExpiryDate.ToString("dd/MM/yyyy").Contains(expiryDateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // PV Balance filter
            if (!string.IsNullOrWhiteSpace(pvFilter))
            {
                var pvConditions = ParseNumericFilter(pvFilter);
                if (pvConditions.HasValue)
                {
                    if (pvConditions.Value.min.HasValue)
                        query = query.Where(x => x.CreditPurchased >= pvConditions.Value.min.Value);
                    if (pvConditions.Value.max.HasValue)
                        query = query.Where(x => x.CreditPurchased <= pvConditions.Value.max.Value);
                }
            }

            filteredCreditData = query.ToList();
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
            return !string.IsNullOrWhiteSpace(creditAcFilter) ||
                   !string.IsNullOrWhiteSpace(memberTypeFilter) ||
                   !string.IsNullOrWhiteSpace(purchaseDateFilter) ||
                   !string.IsNullOrWhiteSpace(invoiceFilter) ||
                   !string.IsNullOrWhiteSpace(customerFilter) ||
                   !string.IsNullOrWhiteSpace(itemNameFilter) ||
                   !string.IsNullOrWhiteSpace(expiryDateFilter) ||
                   !string.IsNullOrWhiteSpace(pvFilter);
        }

        private void ClearFilters()
        {
            creditAcFilter = string.Empty;
            memberTypeFilter = string.Empty;
            purchaseDateFilter = string.Empty;
            invoiceFilter = string.Empty;
            customerFilter = string.Empty;
            itemNameFilter = string.Empty;
            expiryDateFilter = string.Empty;
            pvFilter = string.Empty;
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
                var customerID = "All";
                var cutOffDateTime = GetCutOffDateTime(selectedDate);
                var branches = GetBranchDisplay();

                ApiResponse<List<MemberCreditBalance>>? apiResponse = null;

                switch (selectedStatusTab)
                {
                    case "ActiveCredits":
                        apiResponse = await ReportService.GetMemberCreditBalanceSummary_WithExpiry_NonExpiredAsync(
                            customerID, cutOffDateTime, branches);
                        break;
                    case "ExpiredCredits":
                        apiResponse = await ReportService.GetMemberCreditBalanceSummary_WithExpiry_ExpiredAsync(
                            customerID, cutOffDateTime, branches);
                        break;
                    case "FullyRedeemedCredits":
                        apiResponse = await ReportService.GetMemberCreditBalanceSummary_WithExpiry_FullyRedeemedAsync(
                            customerID, cutOffDateTime, branches);
                        break;
                }

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    creditData = apiResponse.Result;
                    totalCredits = creditData.Count;

                    currentSummaryValue = selectedStatusTab switch
                    {
                        "ActiveCredits" => creditData.Sum(c => c.CreditPurchased).ToString("N0"),
                        "ExpiredCredits" => creditData.Sum(c => c.CreditPurchased).ToString("N0"),
                        "FullyRedeemedCredits" => creditData.Count.ToString("N0"),
                        _ => "0"
                    };

                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResponse?.Message ?? "No data available";
                    alertType = "warning";
                    creditData = new List<MemberCreditBalance>();
                    filteredCreditData = new List<MemberCreditBalance>();
                    totalCredits = 0;
                    currentSummaryValue = "0";
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                creditData = new List<MemberCreditBalance>();
                filteredCreditData = new List<MemberCreditBalance>();
                totalCredits = 0;
                currentSummaryValue = "0";
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task SelectStatusTab(string tab)
        {
            selectedStatusTab = tab;
            ClearFilters();
            await LoadDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            // Validate that the selected date is not in the future
            if (selectedDate > DateTime.Today)
            {
                ShowNotification("Selected Date cannot be in the future.");
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

            // Validate after setting date
            if (selectedDate > DateTime.Today)
            {
                ShowNotification("Selected Date cannot be in the future.");
                return;
            }

            StateHasChanged();
            _ = LoadDataAsync();
        }

        private string GetDateText()
        {
            return selectedDate.ToString("dd/MM/yyyy");
        }

        private string GetStatusFilterText()
        {
            return selectedStatusTab switch
            {
                "ActiveCredits" => "Active Credits",
                "ExpiredCredits" => "Expired Credits",
                "FullyRedeemedCredits" => "Fully Redeemed",
                _ => "Unknown"
            };
        }

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredCreditData.Any() ? filteredCreditData : creditData;

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

            // Use invariant culture for number formatting
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            // Parse currentSummaryValue to decimal for proper formatting
            decimal summaryValue = 0;
            if (!string.IsNullOrEmpty(currentSummaryValue))
            {
                decimal.TryParse(currentSummaryValue.Replace(",", ""), out summaryValue);
            }

            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date\",\"{GetDateText()}\",");
            csv.AppendLine($"\"Status Filter\",\"{GetStatusFilterText()}\",");
            csv.AppendLine($"\"Total Credits\",,{totalCredits.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"{GetCurrentSummaryLabel()}\",,{summaryValue.ToString("0.##", culture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(creditAcFilter))
                    csv.AppendLine($"\"  - Credit A/C Filter\",\"{EscapeCsvValue(creditAcFilter)}\",");
                if (!string.IsNullOrWhiteSpace(memberTypeFilter))
                    csv.AppendLine($"\"  - Member Type Filter\",\"{EscapeCsvValue(memberTypeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(purchaseDateFilter))
                    csv.AppendLine($"\"  - Purchase Date Filter\",\"{EscapeCsvValue(purchaseDateFilter)}\",");
                if (!string.IsNullOrWhiteSpace(invoiceFilter))
                    csv.AppendLine($"\"  - Invoice Filter\",\"{EscapeCsvValue(invoiceFilter)}\",");
                if (!string.IsNullOrWhiteSpace(customerFilter))
                    csv.AppendLine($"\"  - Customer Filter\",\"{EscapeCsvValue(customerFilter)}\",");
                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                    csv.AppendLine($"\"  - Item Name Filter\",\"{EscapeCsvValue(itemNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(expiryDateFilter))
                    csv.AppendLine($"\"  - Expiry Date Filter\",\"{EscapeCsvValue(expiryDateFilter)}\",");
                if (!string.IsNullOrWhiteSpace(pvFilter))
                    csv.AppendLine($"\"  - PV Balance Filter\",\"{EscapeCsvValue(pvFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Mem Credit A/C\",\"Member Type\",\"Purchase Date\",\"Invoice No\",\"Customer Name\",\"Item Name\",\"Expiry Date\",\"PV Balance\"");

            foreach (var credit in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(credit.ARAPOutstandingID)}\",\"{EscapeCsvValue(credit.MemberTypeID)}\",\"{credit.PurchaseDate:dd/MM/yyyy}\",\"{EscapeCsvValue(credit.PurchaseInvoiceNo)}\",\"{EscapeCsvValue(credit.CustomerName)}\",\"{EscapeCsvValue(credit.Description)}\",\"{credit.ExpiryDate:dd/MM/yyyy}\",{credit.CreditPurchased}");
            }

            var fileName = $"MemberCreditBalance_{GetStatusFilterText().Replace(" ", "_")}_{selectedDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredCreditData.Any() ? filteredCreditData : creditData;

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
                var fileName = $"MemberCreditBalance_{GetStatusFilterText().Replace(" ", "_")}_{selectedDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<MemberCreditBalance> dataToExport)
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
                        .Text("Member Credit Balance Report")
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
                                text.Span($"Status: {GetStatusFilterText()} | ");
                                text.Span($"Date: {selectedDate:dd/MM/yyyy}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(creditAcFilter))
                                    column.Item().Text($"  - Credit A/C: {creditAcFilter}");
                                if (!string.IsNullOrWhiteSpace(memberTypeFilter))
                                    column.Item().Text($"  - Member Type: {memberTypeFilter}");
                                if (!string.IsNullOrWhiteSpace(purchaseDateFilter))
                                    column.Item().Text($"  - Purchase Date: {purchaseDateFilter}");
                                if (!string.IsNullOrWhiteSpace(invoiceFilter))
                                    column.Item().Text($"  - Invoice: {invoiceFilter}");
                                if (!string.IsNullOrWhiteSpace(customerFilter))
                                    column.Item().Text($"  - Customer: {customerFilter}");
                                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                                    column.Item().Text($"  - Item Name: {itemNameFilter}");
                                if (!string.IsNullOrWhiteSpace(expiryDateFilter))
                                    column.Item().Text($"  - Expiry Date: {expiryDateFilter}");
                                if (!string.IsNullOrWhiteSpace(pvFilter))
                                    column.Item().Text($"  - PV Balance: {pvFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Credits: {totalCredits:N0} | ");
                                text.Span($"{GetCurrentSummaryLabel()}: {currentSummaryValue} | ");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header - 8 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).Text("Credit A/C").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Member Type").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Purchase Date").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Invoice No").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Customer").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.5f).Padding(4f).Background(Colors.Brown.Medium).Text("Item Name").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4f).Background(Colors.Brown.Medium).Text("Expiry Date").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.6f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("PV Balance").FontColor(Colors.White).Bold().FontSize(7);
                            });

                            // Table Rows
                            foreach (var credit in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.ARAPOutstandingID).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.MemberTypeID).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.PurchaseDate.ToString("dd/MM/yyyy")).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.PurchaseInvoiceNo).FontSize(7);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.CustomerName).FontSize(7);
                                    row.RelativeItem(1.5f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.Description).FontSize(7);
                                    row.RelativeItem(0.8f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(credit.ExpiryDate.ToString("dd/MM/yyyy")).FontSize(7);
                                    row.RelativeItem(0.6f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{credit.CreditPurchased:N0}").FontSize(7).Bold();
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

        public class MemberCreditBalance
        {
            public string ARAPOutstandingID { get; set; } = string.Empty;
            public string PurchaseInvoiceNo { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string MemberTypeID { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime PurchaseDate { get; set; }
            public DateTime ExpiryDate { get; set; }
            public decimal CreditPurchased { get; set; }
            public decimal CreditBalance { get; set; }
        }
    }
}