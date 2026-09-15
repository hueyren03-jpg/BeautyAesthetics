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
    public partial class LastVisitReport
    {
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // Visit range filter
        private string selectedVisitRange = "LessThan1Month";
        private bool showExportDropdown = false;

        // Custom range values
        private int customFromDays = 0;
        private int customToDays = 30;

        // Filter properties
        private string lastVisitFilter = string.Empty;
        private string memberFilter = string.Empty;
        private string amountFilter = string.Empty;

        // Data
        private List<CustomerLastVisit> displayData = new();        // Paginated data for display
        private List<CustomerLastVisit> filteredData = new();       // Filtered data (all matching filters)
        private List<CustomerLastVisit> allData = new();            // All data from API
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";

        // Pagination - Same as MemberListReport
        private int currentPage = 1;
        private int pageSize = 10;
        private int totalPages = 1;
        private int totalFilteredCount = 0;
        private bool hasMoreData = true;

        // Summary statistics - Counts
        private int lessThanOneMonthCount = 0;
        private int oneToTwoMonthsCount = 0;
        private int twoToSixMonthsCount = 0;
        private int sixToTwelveMonthsCount = 0;
        private int moreThanOneYearCount = 0;

        // Summary statistics - Total Sales Amounts
        private decimal lessThanOneMonthTotal = 0;
        private decimal oneToTwoMonthsTotal = 0;
        private decimal twoToSixMonthsTotal = 0;
        private decimal sixToTwelveMonthsTotal = 0;
        private decimal moreThanOneYearTotal = 0;

        // Range mapping for API
        private Dictionary<string, (int from, int to)> rangeMapping = new()
        {
            { "LessThan1Month", (0, 30) },
            { "Between1-2Months", (31, 60) },
            { "Between2-6Months", (61, 180) },
            { "Between6-12Months", (181, 365) },
            { "MoreThan1Year", (366, 9999) }
        };

        // Initialize QuestPDF license
        static LastVisitReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        public class CustomerLastVisit
        {
            public string AccountID { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string DocumentID { get; set; } = string.Empty;
            public int DocumentTypeID { get; set; }
            public DateTime LastVisitDate { get; set; }
            public decimal LastSpend { get; set; }
            public int TotalDaysAgo { get; set; }
            public int MonthsElapsed { get; set; }
            public int DaysElapsed { get; set; }
            public string LastVisitDateString { get; set; } = string.Empty;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await LoadAllDataAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private async Task LoadAllDataAsync()
        {
            isLoading = true;
            exportMessage = null;
            StateHasChanged();

            try
            {
                int fromDays = 0;
                int toDays = 9999;

                if (selectedVisitRange == "Custom")
                {
                    fromDays = customFromDays;
                    toDays = customToDays;
                }
                else if (rangeMapping.ContainsKey(selectedVisitRange))
                {
                    (fromDays, toDays) = rangeMapping[selectedVisitRange];
                }

                // Load all data for filtering and summary
                var allResponse = await ReportService.GetCustomerLastVisitAsync(
                    fromDays,
                    toDays,
                    1,
                    10000);

                if (allResponse != null && allResponse.StatusCode == 200 && allResponse.Result != null)
                {
                    allData = allResponse.Result;
                    currentPage = 1;
                    ApplyFilters();
                    await CalculateSummaryStatistics();
                }
                else
                {
                    allData = new List<CustomerLastVisit>();
                    filteredData = new List<CustomerLastVisit>();
                    displayData = new List<CustomerLastVisit>();
                    totalFilteredCount = 0;
                    totalPages = 1;
                    hasMoreData = false;
                    currentPage = 1;
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                allData = new List<CustomerLastVisit>();
                filteredData = new List<CustomerLastVisit>();
                displayData = new List<CustomerLastVisit>();
                totalFilteredCount = 0;
                totalPages = 1;
                hasMoreData = false;
                currentPage = 1;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ApplyFilters()
        {
            if (allData == null || !allData.Any())
            {
                filteredData = new List<CustomerLastVisit>();
                totalFilteredCount = 0;
                totalPages = 1;
                displayData = new List<CustomerLastVisit>();
                hasMoreData = false;
                return;
            }

            var query = allData.AsEnumerable();

            // Last visit filter
            if (!string.IsNullOrWhiteSpace(lastVisitFilter))
            {
                query = query.Where(x =>
                    GetDaysAgoText(x.TotalDaysAgo).Contains(lastVisitFilter, StringComparison.OrdinalIgnoreCase) ||
                    x.LastVisitDate.ToString("dd/MM/yyyy").Contains(lastVisitFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Member name/ID/phone filter
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                query = query.Where(x =>
                    (x.CustomerName != null && x.CustomerName.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)) ||
                    (x.AccountID != null && x.AccountID.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Phone != null && x.Phone.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)));
            }

            // Amount filter
            if (!string.IsNullOrWhiteSpace(amountFilter))
            {
                if (decimal.TryParse(amountFilter, out decimal amountValue))
                {
                    query = query.Where(x => x.LastSpend >= amountValue);
                }
                else
                {
                    query = query.Where(x => x.LastSpend.ToString("N2").Contains(amountFilter, StringComparison.OrdinalIgnoreCase));
                }
            }

            filteredData = query.ToList();
            totalFilteredCount = filteredData.Count;

            // Update pagination based on filtered results
            totalPages = (int)Math.Ceiling((double)totalFilteredCount / pageSize);
            if (totalPages == 0) totalPages = 1;

            // Ensure current page is valid
            if (currentPage > totalPages)
            {
                currentPage = totalPages;
            }

            // Set hasMoreData based on whether current page is less than total pages
            hasMoreData = currentPage < totalPages;

            // Apply pagination
            displayData = filteredData.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

            StateHasChanged();
        }

        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(lastVisitFilter) ||
                   !string.IsNullOrWhiteSpace(memberFilter) ||
                   !string.IsNullOrWhiteSpace(amountFilter);
        }

        private void ClearFilters()
        {
            lastVisitFilter = string.Empty;
            memberFilter = string.Empty;
            amountFilter = string.Empty;
            currentPage = 1;
            ApplyFilters();
        }

        private async Task CalculateSummaryStatistics()
        {
            // Reset totals
            lessThanOneMonthCount = 0;
            oneToTwoMonthsCount = 0;
            twoToSixMonthsCount = 0;
            sixToTwelveMonthsCount = 0;
            moreThanOneYearCount = 0;

            lessThanOneMonthTotal = 0;
            oneToTwoMonthsTotal = 0;
            twoToSixMonthsTotal = 0;
            sixToTwelveMonthsTotal = 0;
            moreThanOneYearTotal = 0;

            foreach (var customer in allData)
            {
                if (customer.TotalDaysAgo <= 30)
                {
                    lessThanOneMonthCount++;
                    lessThanOneMonthTotal += customer.LastSpend;
                }
                else if (customer.TotalDaysAgo <= 60)
                {
                    oneToTwoMonthsCount++;
                    oneToTwoMonthsTotal += customer.LastSpend;
                }
                else if (customer.TotalDaysAgo <= 180)
                {
                    twoToSixMonthsCount++;
                    twoToSixMonthsTotal += customer.LastSpend;
                }
                else if (customer.TotalDaysAgo <= 365)
                {
                    sixToTwelveMonthsCount++;
                    sixToTwelveMonthsTotal += customer.LastSpend;
                }
                else
                {
                    moreThanOneYearCount++;
                    moreThanOneYearTotal += customer.LastSpend;
                }
            }
        }

        private string GetDaysAgoText(int days)
        {
            if (days == 0) return "Today";
            if (days == 1) return "1 day ago";
            if (days < 30) return $"{days} days ago";
            if (days < 60) return "1 month ago";
            if (days < 365) return $"{days / 30} months ago";
            return $"{days / 365} year{(days / 365 > 1 ? "s" : "")} ago";
        }

        private async Task SelectVisitRange(string range)
        {
            selectedVisitRange = range;
            currentPage = 1;
            ClearFilters();

            // If Custom is selected, validate the custom range
            if (range == "Custom")
            {
                if (customFromDays < 0)
                {
                    ShowNotification("From Days cannot be negative.");
                    return;
                }

                if (customToDays < 0)
                {
                    ShowNotification("To Days cannot be negative.");
                    return;
                }

                if (customFromDays > customToDays)
                {
                    ShowNotification("From Days cannot be greater than To Days.");
                    return;
                }
            }

            await LoadAllDataAsync();
        }

        private async Task GoToFirstPage()
        {
            if (currentPage != 1)
            {
                currentPage = 1;
                ApplyFilters();
            }
        }

        private async Task GoToPreviousPage()
        {
            if (currentPage > 1)
            {
                currentPage--;
                ApplyFilters();
            }
        }

        private async Task GoToNextPage()
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                ApplyFilters();
            }
        }

        private async Task GoToLastPage()
        {
            if (currentPage != totalPages)
            {
                currentPage = totalPages;
                ApplyFilters();
            }
        }

        private async Task OnPageSizeChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int newSize))
            {
                pageSize = newSize;
                currentPage = 1;
                ApplyFilters();
            }
        }

        private async Task RefreshData()
        {
            // Validate custom range if in Custom mode
            if (selectedVisitRange == "Custom")
            {
                if (customFromDays < 0)
                {
                    ShowNotification("From Days cannot be negative.");
                    return;
                }

                if (customToDays < 0)
                {
                    ShowNotification("To Days cannot be negative.");
                    return;
                }

                if (customFromDays > customToDays)
                {
                    ShowNotification("From Days cannot be greater than To Days.");
                    return;
                }
            }

            currentPage = 1;
            ClearFilters(); // Clear filters when refreshing
            await LoadAllDataAsync();
        }

        private string GetVisitRangeDisplayText()
        {
            if (selectedVisitRange == "Custom")
            {
                return $"{customFromDays} - {customToDays} days";
            }

            return selectedVisitRange switch
            {
                "LessThan1Month" => "< 1 Month",
                "Between1-2Months" => "1 - 2 Months",
                "Between2-6Months" => "2 - 6 Months",
                "Between6-12Months" => "6 - 12 Months",
                "MoreThan1Year" => "> 1 Year",
                _ => "All"
            };
        }

        private async Task ExportToCSV()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredData.Any() ? filteredData : allData;

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

            csv.AppendLine($"\"Last Visit Report\",,");
            csv.AppendLine($"\"Visit Range\",\"{GetVisitRangeDisplayText()}\",");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            csv.AppendLine();

            csv.AppendLine($"\"Summary Statistics\",\"Count\",\"Total Sales (RM)\"");
            csv.AppendLine($"\"< 1 Month\",{lessThanOneMonthCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{lessThanOneMonthTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"1 - 2 Months\",{oneToTwoMonthsCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{oneToTwoMonthsTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"2 - 6 Months\",{twoToSixMonthsCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{twoToSixMonthsTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"6 - 12 Months\",{sixToTwelveMonthsCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{sixToTwelveMonthsTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"> 1 Year\",{moreThanOneYearCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{moreThanOneYearTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");

            csv.AppendLine();

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(lastVisitFilter))
                    csv.AppendLine($"\"  - Last Visit Filter\",\"{EscapeCsvValue(lastVisitFilter)}\",");
                if (!string.IsNullOrWhiteSpace(memberFilter))
                    csv.AppendLine($"\"  - Member Filter\",\"{EscapeCsvValue(memberFilter)}\",");
                if (!string.IsNullOrWhiteSpace(amountFilter))
                    csv.AppendLine($"\"  - Amount Filter\",\"{EscapeCsvValue(amountFilter)}\",");
                csv.AppendLine();
            }

            csv.AppendLine($"\"Last Visit\",\"Member\",\"Last Purchase (RM)\"");

            foreach (var customer in dataToExport)
            {
                var lastVisitDisplay = $"{GetDaysAgoText(customer.TotalDaysAgo)} ({customer.LastVisitDate:dd/MM/yyyy})";
                var phoneDisplay = string.IsNullOrEmpty(customer.Phone) ? "-" : customer.Phone;

                csv.AppendLine($"\"{EscapeCsvValue(lastVisitDisplay)}\"," +
                    $"\"{EscapeCsvValue(customer.CustomerName)} ({customer.AccountID} / {phoneDisplay})\"," +
                    $"{customer.LastSpend:N2}");
            }

            var fileName = $"LastVisit_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var csvContent = csv.ToString();

            var csvBytes = Encoding.UTF8.GetBytes(csvContent);
            var base64Content = Convert.ToBase64String(csvBytes);

            await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, "text/csv");

            exportMessage = "CSV export completed!";
            alertType = "success";
            StateHasChanged();
            await Task.Delay(3000);
            exportMessage = null;
            StateHasChanged();
        }

        private async Task ExportToPDF()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredData.Any() ? filteredData : allData;

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
                var fileName = $"LastVisit_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

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

        private byte[] GeneratePdfDocument(List<CustomerLastVisit> data)
        {
            var summaryStats = new
            {
                LessThanOneMonthCount = data.Count(c => c.TotalDaysAgo <= 30),
                LessThanOneMonthTotal = data.Where(c => c.TotalDaysAgo <= 30).Sum(c => c.LastSpend),
                OneToTwoMonthsCount = data.Count(c => c.TotalDaysAgo > 30 && c.TotalDaysAgo <= 60),
                OneToTwoMonthsTotal = data.Where(c => c.TotalDaysAgo > 30 && c.TotalDaysAgo <= 60).Sum(c => c.LastSpend),
                TwoToSixMonthsCount = data.Count(c => c.TotalDaysAgo > 60 && c.TotalDaysAgo <= 180),
                TwoToSixMonthsTotal = data.Where(c => c.TotalDaysAgo > 60 && c.TotalDaysAgo <= 180).Sum(c => c.LastSpend),
                SixToTwelveMonthsCount = data.Count(c => c.TotalDaysAgo > 180 && c.TotalDaysAgo <= 365),
                SixToTwelveMonthsTotal = data.Where(c => c.TotalDaysAgo > 180 && c.TotalDaysAgo <= 365).Sum(c => c.LastSpend),
                MoreThanOneYearCount = data.Count(c => c.TotalDaysAgo > 365),
                MoreThanOneYearTotal = data.Where(c => c.TotalDaysAgo > 365).Sum(c => c.LastSpend)
            };

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
                        .Text("Last Visit Report")
                        .SemiBold().FontSize(16).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(5f)
                        .Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span($"Visit Range: {GetVisitRangeDisplayText()}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(lastVisitFilter))
                                    column.Item().Text($"  - Last Visit: {lastVisitFilter}");
                                if (!string.IsNullOrWhiteSpace(memberFilter))
                                    column.Item().Text($"  - Member: {memberFilter}");
                                if (!string.IsNullOrWhiteSpace(amountFilter))
                                    column.Item().Text($"  - Amount: {amountFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            // Summary Statistics row
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("< 1 Month").FontSize(7);
                                    col.Item().Text(summaryStats.LessThanOneMonthCount.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                    col.Item().Text($"RM {summaryStats.LessThanOneMonthTotal:N2}").FontSize(8);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("1 - 2 Months").FontSize(7);
                                    col.Item().Text(summaryStats.OneToTwoMonthsCount.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                    col.Item().Text($"RM {summaryStats.OneToTwoMonthsTotal:N2}").FontSize(8);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("2 - 6 Months").FontSize(7);
                                    col.Item().Text(summaryStats.TwoToSixMonthsCount.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                    col.Item().Text($"RM {summaryStats.TwoToSixMonthsTotal:N2}").FontSize(8);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("6 - 12 Months").FontSize(7);
                                    col.Item().Text(summaryStats.SixToTwelveMonthsCount.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                    col.Item().Text($"RM {summaryStats.SixToTwelveMonthsTotal:N2}").FontSize(8);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("> 1 Year").FontSize(7);
                                    col.Item().Text(summaryStats.MoreThanOneYearCount.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                    col.Item().Text($"RM {summaryStats.MoreThanOneYearTotal:N2}").FontSize(8);
                                });
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1f).Padding(4).Background(Colors.Brown.Medium).Text("Last Visit").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.5f).Padding(4).Background(Colors.Brown.Medium).Text("Member").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4).Background(Colors.Brown.Medium).AlignRight().Text("Last Purchase").FontColor(Colors.White).Bold().FontSize(7);
                            });

                            // Table Rows
                            foreach (var customer in data)
                            {
                                var lastVisitDisplay = GetDaysAgoText(customer.TotalDaysAgo);
                                var lastVisitDate = customer.LastVisitDate.ToString("dd/MM/yyyy");
                                var phoneDisplay = string.IsNullOrEmpty(customer.Phone) ? "-" : customer.Phone;

                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                                    {
                                        col.Item().Text(lastVisitDisplay).FontSize(8);
                                        col.Item().Text(lastVisitDate).FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(1.5f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                                    {
                                        col.Item().Text(customer.CustomerName ?? "-").FontSize(8);
                                        col.Item().Text($"{customer.AccountID} / {phoneDisplay}").FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(0.8f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"RM {customer.LastSpend:N2}").FontSize(8);
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

        private string EscapeCsvValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\"", "\"\"");
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
    }
}