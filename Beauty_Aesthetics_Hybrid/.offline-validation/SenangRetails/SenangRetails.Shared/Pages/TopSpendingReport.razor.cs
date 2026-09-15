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
    public partial class TopSpendingReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private string selectedTypeTab = "All";
        private string selectedRankBy = "Amount";
        private int recordCount = 10;
        private List<TopSalesCustomer> items = new();
        private List<TopSalesCustomer> filteredItems = new();
        private decimal totalAmount = 0;
        private decimal totalQuantity = 0;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Filter properties
        private string memberFilter = string.Empty;
        private string salesFilter = string.Empty;
        private string redeemedFilter = string.Empty;
        private string totalFilter = string.Empty;

        // Initialize QuestPDF license
        static TopSpendingReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        public class TopSalesCustomer
        {
            public string CustomerID { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public decimal SalesQuantity { get; set; }
            public decimal RedeemQuantity { get; set; }
            public decimal TotalQuantity { get; set; }
            public decimal SalesAmount { get; set; }
            public decimal RedeemAmount { get; set; }
            public decimal TotalAmount { get; set; }
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private void ApplyFilters()
        {
            if (items == null || !items.Any())
            {
                filteredItems = new List<TopSalesCustomer>();
                return;
            }

            var query = items.AsEnumerable();

            // Member filter
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                query = query.Where(x => (x.CustomerName != null && x.CustomerName.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)) ||
                                         (x.CustomerID != null && x.CustomerID.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)));
            }

            // Sales filter
            if (!string.IsNullOrWhiteSpace(salesFilter))
            {
                var salesConditions = ParseNumericFilter(salesFilter);
                if (salesConditions.HasValue)
                {
                    if (salesConditions.Value.min.HasValue)
                        query = query.Where(x => x.SalesAmount >= salesConditions.Value.min.Value);
                    if (salesConditions.Value.max.HasValue)
                        query = query.Where(x => x.SalesAmount <= salesConditions.Value.max.Value);
                }
            }

            // Redeemed filter
            if (!string.IsNullOrWhiteSpace(redeemedFilter))
            {
                var redeemedConditions = ParseNumericFilter(redeemedFilter);
                if (redeemedConditions.HasValue)
                {
                    if (redeemedConditions.Value.min.HasValue)
                        query = query.Where(x => x.RedeemAmount >= redeemedConditions.Value.min.Value);
                    if (redeemedConditions.Value.max.HasValue)
                        query = query.Where(x => x.RedeemAmount <= redeemedConditions.Value.max.Value);
                }
            }

            // Total filter
            if (!string.IsNullOrWhiteSpace(totalFilter))
            {
                var totalConditions = ParseNumericFilter(totalFilter);
                if (totalConditions.HasValue)
                {
                    if (totalConditions.Value.min.HasValue)
                        query = query.Where(x => x.TotalAmount >= totalConditions.Value.min.Value);
                    if (totalConditions.Value.max.HasValue)
                        query = query.Where(x => x.TotalAmount <= totalConditions.Value.max.Value);
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
            return !string.IsNullOrWhiteSpace(memberFilter) ||
                   !string.IsNullOrWhiteSpace(salesFilter) ||
                   !string.IsNullOrWhiteSpace(redeemedFilter) ||
                   !string.IsNullOrWhiteSpace(totalFilter);
        }

        private void ClearFilters()
        {
            memberFilter = string.Empty;
            salesFilter = string.Empty;
            redeemedFilter = string.Empty;
            totalFilter = string.Empty;
            ApplyFilters();
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private int GetInventoryTypeId(string type)
        {
            return type switch
            {
                "All" => -1,
                "Service" => 3,
                "Product" => 1,
                "Package" => 5,
                "TopUp" => 7,
                _ => -1
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
                var branches = GetBranchDisplay();
                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);
                var inventoryTypeId = GetInventoryTypeId(selectedTypeTab);
                string rankByValue = selectedRankBy == "Amount" ? "Amount" : "Quantity";

                var apiResponse = await ReportService.GetTopSalesCustomerAsync(
                    startDateTime,
                    endDateTime,
                    inventoryTypeId,
                    recordCount,
                    rankByValue,
                    branches);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null && apiResponse.Result.Any())
                {
                    items = apiResponse.Result;
                    totalAmount = items.Sum(x => x.SalesAmount);
                    totalQuantity = items.Sum(x => x.SalesQuantity);
                    ApplyFilters();
                    exportMessage = null;
                }
                else
                {
                    items = new List<TopSalesCustomer>();
                    filteredItems = new List<TopSalesCustomer>();
                    totalAmount = 0;
                    totalQuantity = 0;
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                items = new List<TopSalesCustomer>();
                filteredItems = new List<TopSalesCustomer>();
                totalAmount = 0;
                totalQuantity = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
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
            ClearFilters();
            await LoadDataAsync();
        }

        private async Task SelectRankBy(string rankBy)
        {
            selectedRankBy = rankBy;
            await LoadDataAsync();
        }

        private async Task ApplyRecordCount()
        {
            if (recordCount < 1)
                recordCount = 1;
            if (recordCount > 500)
                recordCount = 500;
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

        private string GetRankByText()
        {
            return selectedRankBy == "Amount" ? "By Amount (Highest to Lowest)" : "By Quantity (Highest to Lowest)";
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

            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Report Type\",\"{selectedTypeTab}\",");
            csv.AppendLine($"\"Rank By\",\"{GetRankByText()}\",");
            csv.AppendLine($"\"Record Count\",{recordCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Sales\",{totalAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},");
            csv.AppendLine($"\"Total Quantity\",{totalQuantity.ToString("0", System.Globalization.CultureInfo.InvariantCulture)},");
            csv.AppendLine($"\"Showing Records\",{dataToExport.Count.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(memberFilter))
                    csv.AppendLine($"\"  - Member Filter\",\"{EscapeCsvValue(memberFilter)}\",");
                if (!string.IsNullOrWhiteSpace(salesFilter))
                    csv.AppendLine($"\"  - Sales Filter\",\"{EscapeCsvValue(salesFilter)}\",");
                if (!string.IsNullOrWhiteSpace(redeemedFilter))
                    csv.AppendLine($"\"  - Redeemed Filter\",\"{EscapeCsvValue(redeemedFilter)}\",");
                if (!string.IsNullOrWhiteSpace(totalFilter))
                    csv.AppendLine($"\"  - Total Filter\",\"{EscapeCsvValue(totalFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"{LangSvc.GetText("Member")}\",\"Member ID\",\"{LangSvc.GetText("ReportSales")} (Amount)\",\"{LangSvc.GetText("ReportSales")} (Qty)\",\"{LangSvc.GetText("ReportRedeemed")} (Amount)\",\"{LangSvc.GetText("ReportRedeemed")} (Qty)\",\"{LangSvc.GetText("ReportTotal")} (Amount)\",\"{LangSvc.GetText("ReportTotal")} (Qty)\"");

            foreach (var item in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(item.CustomerName)}\"," +
                    $"\"{item.CustomerID}\"," +
                    $"{item.SalesAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{item.SalesQuantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{item.RedeemAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{item.RedeemQuantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{item.TotalAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{item.TotalQuantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"TopSpending_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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
                var fileName = $"TopSpending_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<TopSalesCustomer> dataToExport)
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
                        .Text("Top Spending Report")
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
                                text.Span($"Report Type: {selectedTypeTab}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Rank By: {GetRankByText()} | ");
                                text.Span($"Record Count: {recordCount}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(memberFilter))
                                    column.Item().Text($"  - Member: {memberFilter}");
                                if (!string.IsNullOrWhiteSpace(salesFilter))
                                    column.Item().Text($"  - Sales: {salesFilter}");
                                if (!string.IsNullOrWhiteSpace(redeemedFilter))
                                    column.Item().Text($"  - Redeemed: {redeemedFilter}");
                                if (!string.IsNullOrWhiteSpace(totalFilter))
                                    column.Item().Text($"  - Total: {totalFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Amount: RM {totalAmount:N2} | ");
                                text.Span($"Total Quantity: {totalQuantity:N2} | ");
                                text.Span($"Showing Records: {dataToExport.Count}");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            column.Item().Row(row =>
                            {
                                row.RelativeItem(2).Padding(4).Background(Colors.Brown.Medium).Text("Member / Member ID").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1).Padding(4).Background(Colors.Brown.Medium).AlignRight().Text("Sales").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1).Padding(4).Background(Colors.Brown.Medium).AlignRight().Text("Redeemed").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1).Padding(4).Background(Colors.Brown.Medium).AlignRight().Text("Total").FontColor(Colors.White).Bold().FontSize(8);
                            });

                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(2).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                                    {
                                        col.Item().Text(item.CustomerName ?? "").FontSize(8).Bold();
                                        col.Item().Text(item.CustomerID ?? "").FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(1).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Column(col =>
                                    {
                                        col.Item().Text($"RM {item.SalesAmount:N2}").FontSize(8);
                                        col.Item().Text($"Qty: {item.SalesQuantity:N2}").FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(1).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Column(col =>
                                    {
                                        col.Item().Text($"RM {item.RedeemAmount:N2}").FontSize(8);
                                        col.Item().Text($"Qty: {item.RedeemQuantity:N2}").FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(1).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Column(col =>
                                    {
                                        col.Item().Text($"RM {item.TotalAmount:N2}").FontSize(8).Bold().FontColor(Colors.Brown.Medium);
                                        col.Item().Text($"Qty: {item.TotalQuantity:N2}").FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
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
    }
}