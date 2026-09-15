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
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.FileDownloadService;

namespace SenangRetails.Shared.Components.Reports
{
    public partial class BalanceSummaryReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime selectedDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Data
        private List<MemberOtherBalanceSummary> balanceData = new();
        private List<MemberOtherBalanceSummary> filteredBalanceData = new();

        // Filter properties
        private string memberFilter = string.Empty;
        private string phoneFilter = string.Empty;
        private string outstandingFilter = string.Empty;
        private string pointsFilter = string.Empty;

        // Summary totals
        private int totalMembers = 0;
        private decimal totalOutstanding = 0;
        private decimal totalPoints = 0;

        static BalanceSummaryReport()
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

        private string GetOutstandingClass(decimal outstanding)
        {
            if (outstanding > 0) return "has-outstanding";
            if (outstanding < 0) return "credit-balance";
            return "zero-balance";
        }

        private void ApplyFilters()
        {
            if (balanceData == null || !balanceData.Any())
            {
                filteredBalanceData = new List<MemberOtherBalanceSummary>();
                return;
            }

            var query = balanceData.AsEnumerable();

            // Member name filter
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                query = query.Where(x => x.CustomerName != null && x.CustomerName.Contains(memberFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Phone filter
            if (!string.IsNullOrWhiteSpace(phoneFilter))
            {
                query = query.Where(x => x.Phone != null && x.Phone.Contains(phoneFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Outstanding filter
            if (!string.IsNullOrWhiteSpace(outstandingFilter))
            {
                var outstandingConditions = ParseNumericFilter(outstandingFilter);
                if (outstandingConditions.HasValue)
                {
                    if (outstandingConditions.Value.min.HasValue)
                        query = query.Where(x => x.Outstanding >= outstandingConditions.Value.min.Value);
                    if (outstandingConditions.Value.max.HasValue)
                        query = query.Where(x => x.Outstanding <= outstandingConditions.Value.max.Value);
                }
            }

            // Points filter
            if (!string.IsNullOrWhiteSpace(pointsFilter))
            {
                var pointsConditions = ParseNumericFilter(pointsFilter);
                if (pointsConditions.HasValue)
                {
                    if (pointsConditions.Value.min.HasValue)
                        query = query.Where(x => x.Point >= pointsConditions.Value.min.Value);
                    if (pointsConditions.Value.max.HasValue)
                        query = query.Where(x => x.Point <= pointsConditions.Value.max.Value);
                }
            }

            filteredBalanceData = query.ToList();
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
                   !string.IsNullOrWhiteSpace(outstandingFilter) ||
                   !string.IsNullOrWhiteSpace(pointsFilter);
        }

        private void ClearFilters()
        {
            memberFilter = string.Empty;
            phoneFilter = string.Empty;
            outstandingFilter = string.Empty;
            pointsFilter = string.Empty;
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

                var apiResponse = await ReportService.GetMemberOtherBalanceSummaryAsync(
                   customerID,
                   cutOffDateTime);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    balanceData = apiResponse.Result.ToList();

                    // Calculate totals
                    totalMembers = balanceData.Count;
                    totalOutstanding = balanceData.Sum(m => m.Outstanding);
                    totalPoints = balanceData.Sum(m => m.Point);

                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResponse?.Message ?? "No data available";
                    alertType = "warning";
                    balanceData = new List<MemberOtherBalanceSummary>();
                    filteredBalanceData = new List<MemberOtherBalanceSummary>();
                    totalMembers = 0;
                    totalOutstanding = 0;
                    totalPoints = 0;
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                balanceData = new List<MemberOtherBalanceSummary>();
                filteredBalanceData = new List<MemberOtherBalanceSummary>();
                totalMembers = 0;
                totalOutstanding = 0;
                totalPoints = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task RefreshDataAsync()
        {
            // Validate date (e.g., cannot be in the future)
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

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredBalanceData.Any() ? filteredBalanceData : balanceData;

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
            csv.AppendLine($"\"Date\",\"{GetDateText()}\",");
            csv.AppendLine($"\"Total Members\",,{totalMembers.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Outstandings\",,{totalOutstanding.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Points\",,{totalPoints.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(memberFilter))
                    csv.AppendLine($"\"  - Member Filter\",\"{EscapeCsvValue(memberFilter)}\",");
                if (!string.IsNullOrWhiteSpace(phoneFilter))
                    csv.AppendLine($"\"  - Phone Filter\",\"{EscapeCsvValue(phoneFilter)}\",");
                if (!string.IsNullOrWhiteSpace(outstandingFilter))
                    csv.AppendLine($"\"  - Outstanding Filter\",\"{EscapeCsvValue(outstandingFilter)}\",");
                if (!string.IsNullOrWhiteSpace(pointsFilter))
                    csv.AppendLine($"\"  - Points Filter\",\"{EscapeCsvValue(pointsFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Member\",\"Phone\",\"Outstandings\",\"Points\"");

            foreach (var member in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(member.CustomerName)}\",\"{EscapeCsvValue(member.Phone)}\",{member.Outstanding.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{member.Point.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"BalanceSummary_{selectedDate:yyyyMMdd}.csv";
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

            var dataToExport = filteredBalanceData.Any() ? filteredBalanceData : balanceData;

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
                var fileName = $"BalanceSummary_{selectedDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<MemberOtherBalanceSummary> dataToExport)
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
                        .Text("Balance Summary Report")
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
                                text.Span($"Date: {selectedDate:dd/MM/yyyy}");
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
                                if (!string.IsNullOrWhiteSpace(phoneFilter))
                                    column.Item().Text($"  - Phone: {phoneFilter}");
                                if (!string.IsNullOrWhiteSpace(outstandingFilter))
                                    column.Item().Text($"  - Outstanding: {outstandingFilter}");
                                if (!string.IsNullOrWhiteSpace(pointsFilter))
                                    column.Item().Text($"  - Points: {pointsFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Members: {totalMembers:N0} | ");
                                text.Span($"Total Outstandings: RM {totalOutstanding:N2} | ");
                                text.Span($"Total Points: {totalPoints:N0} | ");
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header - 4 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(2f).Padding(4f).Background(Colors.Brown.Medium).Text("Member").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1.5f).Padding(4f).Background(Colors.Brown.Medium).Text("Phone").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Outstandings").FontColor(Colors.White).Bold().FontSize(8);
                                row.RelativeItem(1f).Padding(4f).Background(Colors.Brown.Medium).AlignRight().Text("Points").FontColor(Colors.White).Bold().FontSize(8);
                            });

                            // Table Rows
                            foreach (var member in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(2f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(member.CustomerName).FontSize(8);
                                    row.RelativeItem(1.5f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(member.Phone).FontSize(8);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{member.Outstanding:N2}").FontSize(8);
                                    row.RelativeItem(1f).Padding(4f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{member.Point:N0}").FontSize(8).Bold();
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

        public class MemberOtherBalanceSummary
        {
            public string CustomerName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public decimal Outstanding { get; set; }
            public decimal Point { get; set; }
        }
    }
}