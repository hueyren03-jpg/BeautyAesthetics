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
    public partial class BalanceTransactionReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime selectedDate = DateTime.Today;
        private string selectedBalanceType = "Outstanding";
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Filter properties
        private string memberFilter = string.Empty;
        private string phoneFilter = string.Empty;
        private string invoiceFilter = string.Empty;
        private string dateFilter = string.Empty;
        private string amountFilter = string.Empty;
        private string balanceFilter = string.Empty;

        // Data collections
        private List<BalanceTransactionDisplay> transactions = new();
        private List<BalanceTransactionDisplay> filteredTransactions = new();

        // Initialize QuestPDF license
        static BalanceTransactionReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private void ApplyFilters()
        {
            if (transactions == null || !transactions.Any())
            {
                filteredTransactions = new List<BalanceTransactionDisplay>();
                return;
            }

            var query = transactions.AsEnumerable();

            // Member name filter
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                query = query.Where(x => x.CustomerName != null &&
                    x.CustomerName.Contains(memberFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Phone filter
            if (!string.IsNullOrWhiteSpace(phoneFilter))
            {
                query = query.Where(x => x.Phone != null &&
                    x.Phone.Contains(phoneFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Invoice number filter
            if (!string.IsNullOrWhiteSpace(invoiceFilter))
            {
                query = query.Where(x => x.BillNo != null &&
                    x.BillNo.Contains(invoiceFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Date filter
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                query = query.Where(x => x.FinancialDate.ToString("dd/MM/yyyy").Contains(dateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Amount filter
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

            // Balance filter
            if (!string.IsNullOrWhiteSpace(balanceFilter))
            {
                var balanceConditions = ParseNumericFilter(balanceFilter);
                if (balanceConditions.HasValue)
                {
                    if (balanceConditions.Value.min.HasValue)
                        query = query.Where(x => x.Balance >= balanceConditions.Value.min.Value);
                    if (balanceConditions.Value.max.HasValue)
                        query = query.Where(x => x.Balance <= balanceConditions.Value.max.Value);
                }
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
            return !string.IsNullOrWhiteSpace(memberFilter) ||
                   !string.IsNullOrWhiteSpace(phoneFilter) ||
                   !string.IsNullOrWhiteSpace(invoiceFilter) ||
                   !string.IsNullOrWhiteSpace(dateFilter) ||
                   !string.IsNullOrWhiteSpace(amountFilter) ||
                   !string.IsNullOrWhiteSpace(balanceFilter);
        }

        private void ClearFilters()
        {
            memberFilter = string.Empty;
            phoneFilter = string.Empty;
            invoiceFilter = string.Empty;
            dateFilter = string.Empty;
            amountFilter = string.Empty;
            balanceFilter = string.Empty;
            ApplyFilters();
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            exportMessage = null;
            transactions.Clear();
            StateHasChanged();

            try
            {
                var customerID = "All";
                var startDateTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, 0, 0, 0);

                var apiResponse = await ReportService.GetMemberOtherBalanceDetailAsync(
                    customerID,
                    startDateTime,
                    selectedBalanceType);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    var flattenedTransactions = new List<BalanceTransactionDisplay>();

                    foreach (var kvp in apiResponse.Result)
                    {
                        var customerData = kvp.Value;

                        if (customerData.lstTransaction != null && customerData.lstTransaction.Any())
                        {
                            foreach (var transaction in customerData.lstTransaction)
                            {
                                flattenedTransactions.Add(new BalanceTransactionDisplay
                                {
                                    CustomerName = customerData.CustomerName,
                                    Phone = customerData.Phone,
                                    BillNo = transaction.BillNo,
                                    FinancialDate = transaction.FinancialDate,
                                    Amount = transaction.Amount,
                                    Balance = transaction.Balance
                                });
                            }
                        }
                    }

                    transactions = flattenedTransactions
                        .OrderByDescending(t => t.FinancialDate)
                        .ToList();

                    // Apply filters after loading
                    ApplyFilters();

                    if (transactions.Count == 0)
                    {
                        exportMessage = "No transactions found for the selected criteria";
                        alertType = "info";
                    }
                }
                else
                {
                    if (apiResponse == null)
                    {
                        exportMessage = LangSvc.GetText("Failed to load data from API");
                        alertType = "error";
                    }
                    else
                    {
                        exportMessage = apiResponse.Message ?? "No data available";
                        alertType = "warning";
                    }
                    transactions = new List<BalanceTransactionDisplay>();
                    filteredTransactions = new List<BalanceTransactionDisplay>();
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                transactions = new List<BalanceTransactionDisplay>();
                filteredTransactions = new List<BalanceTransactionDisplay>();
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task SelectBalanceType(string type)
        {
            selectedBalanceType = type;
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
                    selectedDate = today;
                    break;
                case "Week":
                    selectedDate = today;
                    break;
                case "Month":
                    selectedDate = today;
                    break;
                case "Year":
                    selectedDate = today;
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

        private async Task RefreshDataAsync()
        {
            // Validate that the selected date is not in the future
            if (selectedDate > DateTime.Today)
            {
                ShowNotification("Selected Date cannot be in the future.");
                return;
            }

            ClearFilters();
            await LoadDataAsync();
        }

        private string GetDateText()
        {
            return selectedDate.ToString("dd/MM/yyyy");
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

            // Report Summary Section
            csv.AppendLine($"\"{LangSvc.GetText("Report Summary")}\"");
            csv.AppendLine($"\"{LangSvc.GetText("Branch")}\",\"{GetBranchDisplay()}\"");
            csv.AppendLine($"\"{LangSvc.GetText("Exported On")}\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\"");
            csv.AppendLine($"\"{LangSvc.GetText("Date")}\",\"{GetDateText()}\"");
            csv.AppendLine($"\"{LangSvc.GetText("Balance Type")}\",\"{selectedBalanceType}\"");
            csv.AppendLine($"\"{LangSvc.GetText("Total Transactions")}\",{dataToExport.Count.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");

            // Add filter information if filters are active
            if (HasActiveFilters())
            {
                csv.AppendLine($"\"{LangSvc.GetText("Filters Applied")}\",");
                if (!string.IsNullOrWhiteSpace(memberFilter))
                    csv.AppendLine($"\"  - Member Filter\",\"{EscapeCsvValue(memberFilter)}\"");
                if (!string.IsNullOrWhiteSpace(phoneFilter))
                    csv.AppendLine($"\"  - Phone Filter\",\"{EscapeCsvValue(phoneFilter)}\"");
                if (!string.IsNullOrWhiteSpace(invoiceFilter))
                    csv.AppendLine($"\"  - Invoice Filter\",\"{EscapeCsvValue(invoiceFilter)}\"");
                if (!string.IsNullOrWhiteSpace(dateFilter))
                    csv.AppendLine($"\"  - Date Filter\",\"{EscapeCsvValue(dateFilter)}\"");
                if (!string.IsNullOrWhiteSpace(amountFilter))
                    csv.AppendLine($"\"  - Amount Filter\",\"{EscapeCsvValue(amountFilter)}\"");
                if (!string.IsNullOrWhiteSpace(balanceFilter))
                    csv.AppendLine($"\"  - Balance Filter\",\"{EscapeCsvValue(balanceFilter)}\"");
            }

            csv.AppendLine();

            // Data Header
            csv.AppendLine($"\"{LangSvc.GetText("Member")}\",\"{LangSvc.GetText("Phone")}\",\"{LangSvc.GetText("Invoice No")}\",\"{LangSvc.GetText("Date")}\",\"{LangSvc.GetText("Amount")}\",\"{LangSvc.GetText("Balance")}\"");

            // Data rows
            foreach (var transaction in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(transaction.CustomerName)}\",\"{EscapeCsvValue(transaction.Phone)}\",\"{EscapeCsvValue(transaction.BillNo)}\",\"{transaction.FinancialDate:dd/MM/yyyy}\",{transaction.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{transaction.Balance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"BalanceTransaction_{selectedBalanceType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
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
                var fileName = $"BalanceTransaction_{selectedBalanceType}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

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

        private byte[] GeneratePdfDocument(List<BalanceTransactionDisplay> dataToExport)
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
                        .Text($"Balance Transaction Report - {selectedBalanceType}")
                        .SemiBold().FontSize(16).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(5f)
                        .Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span($"Branch: {branchName} | ");
                                text.Span($"Date: {GetDateText()} | ");
                                text.Span($"Balance Type: {selectedBalanceType} | ");
                                text.Span($"Total Transactions: {dataToExport.Count}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Exported On: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                            });

                            // Add filter information if active
                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(memberFilter))
                                    column.Item().Text($"  - Member: {memberFilter}");
                                if (!string.IsNullOrWhiteSpace(phoneFilter))
                                    column.Item().Text($"  - Phone: {phoneFilter}");
                                if (!string.IsNullOrWhiteSpace(invoiceFilter))
                                    column.Item().Text($"  - Invoice: {invoiceFilter}");
                                if (!string.IsNullOrWhiteSpace(dateFilter))
                                    column.Item().Text($"  - Date: {dateFilter}");
                                if (!string.IsNullOrWhiteSpace(amountFilter))
                                    column.Item().Text($"  - Amount: {amountFilter}");
                                if (!string.IsNullOrWhiteSpace(balanceFilter))
                                    column.Item().Text($"  - Balance: {balanceFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table header - 6 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1.5f).Padding(4f).Background(Colors.Brown.Medium).Text("Member").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Phone").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.2f).Padding(4f).Background(Colors.Brown.Medium).Text("Invoice No").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.9f).Padding(4f).Background(Colors.Brown.Medium).Text("Date").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.9f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Amount").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.9f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Balance").FontColor(Colors.White).Bold().FontSize(7);
                            });

                            foreach (var transaction in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1.5f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(transaction.CustomerName).FontSize(7);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(transaction.Phone).FontSize(7);
                                    row.RelativeItem(1.2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(transaction.BillNo).FontSize(7);
                                    row.RelativeItem(0.9f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(transaction.FinancialDate.ToString("dd/MM/yyyy")).FontSize(7);
                                    row.RelativeItem(0.9f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{transaction.Amount:N2}").FontSize(7);
                                    row.RelativeItem(0.9f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{transaction.Balance:N2}").FontSize(7);
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

        public class BalanceTransactionDisplay
        {
            public string CustomerName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string BillNo { get; set; } = string.Empty;
            public DateTime FinancialDate { get; set; }
            public decimal Amount { get; set; }
            public decimal Balance { get; set; }
        }

        public class MemberOtherBalanceDetail
        {
            public Dictionary<string, MemberBalanceDetail> Result { get; set; } = new();
        }

        public class MemberBalanceDetail
        {
            public string CustomerID { get; set; } = string.Empty;
            public string CustomerCode { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public List<TransactionDetail> lstTransaction { get; set; } = new();
        }

        public class TransactionDetail
        {
            public string DocumentID { get; set; } = string.Empty;
            public DateTime FinancialDate { get; set; }
            public string BillNo { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public decimal Balance { get; set; }
        }
    }
}