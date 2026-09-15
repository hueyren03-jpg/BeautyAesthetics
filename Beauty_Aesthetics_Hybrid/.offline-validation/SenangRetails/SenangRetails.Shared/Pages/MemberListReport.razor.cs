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
    public partial class MemberListReport
    {
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private string selectedGender = "All";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Filter properties
        private string memberFilter = string.Empty;
        private string contactFilter = string.Empty;
        private string genderFilter = string.Empty;
        private string birthdayFilter = string.Empty;
        private string createdDateFilter = string.Empty;

        // Summary Statistics
        private int totalMembers = 0;
        private int todayMembers = 0;
        private int thisMonthMembers = 0;
        private int lastMonthMembers = 0;
        private decimal monthlyGrowthRate = 0;

        // Detail Data
        private List<MemberStatisticDetail> members = new();
        private List<MemberStatisticDetail> filteredMembers = new();
        private List<MemberStatisticDetail> allMembers = new();

        // Pagination
        private int currentPage = 1;
        private int pageSize = 10;
        private int totalPages = 1;
        private int totalFilteredCount = 0;
        private bool hasMoreData = true;

        // Helper method to determine if a gender value is Male
        private bool IsMale(string gender)
        {
            if (string.IsNullOrEmpty(gender)) return false;
            var normalizedGender = gender.Trim().ToLowerInvariant();
            return normalizedGender == "male" || normalizedGender == "m" || normalizedGender == "男";
        }

        // Helper method to determine if a gender value is Female
        private bool IsFemale(string gender)
        {
            if (string.IsNullOrEmpty(gender)) return false;
            var normalizedGender = gender.Trim().ToLowerInvariant();
            return normalizedGender == "female" || normalizedGender == "f" || normalizedGender == "女";
        }

        // Helper method to get display text for gender
        private string GetGenderDisplay(string gender)
        {
            if (IsMale(gender)) return "Male";
            if (IsFemale(gender)) return "Female";
            return "";
        }

        // Initialize QuestPDF license
        static MemberListReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private void ApplyFilters()
        {
            if (allMembers == null || !allMembers.Any())
            {
                filteredMembers = new List<MemberStatisticDetail>();
                totalFilteredCount = 0;
                totalPages = 1;
                members = new List<MemberStatisticDetail>();
                hasMoreData = false;
                UpdateKPIsFromFilteredData(); // Add this line
                return;
            }

            var query = allMembers.AsEnumerable();

            // Member name/ID filter
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                query = query.Where(x => (x.AccountName != null && x.AccountName.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)) ||
                                         (x.MasterAccountID != null && x.MasterAccountID.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)));
            }

            // Contact/IC filter
            if (!string.IsNullOrWhiteSpace(contactFilter))
            {
                query = query.Where(x => (x.Phone != null && x.Phone.Contains(contactFilter, StringComparison.OrdinalIgnoreCase)) ||
                                         (x.NRIC != null && x.NRIC.Contains(contactFilter, StringComparison.OrdinalIgnoreCase)));
            }

            // Gender text filter (applied alongside gender tabs)
            if (!string.IsNullOrWhiteSpace(genderFilter))
            {
                query = query.Where(x => GetGenderDisplay(x.Gender).Contains(genderFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Birthday filter
            if (!string.IsNullOrWhiteSpace(birthdayFilter))
            {
                query = query.Where(x => GetBirthday(x).Contains(birthdayFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Created date filter
            if (!string.IsNullOrWhiteSpace(createdDateFilter))
            {
                query = query.Where(x => x.AccountSince.ToString("dd/MM/yyyy").Contains(createdDateFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply gender filter from tabs
            if (selectedGender == "Male")
            {
                query = query.Where(x => IsMale(x.Gender));
            }
            else if (selectedGender == "Female")
            {
                query = query.Where(x => IsFemale(x.Gender));
            }

            filteredMembers = query.ToList();
            totalFilteredCount = filteredMembers.Count;

            // Update KPIs based on filtered data
            UpdateKPIsFromFilteredData();

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
            members = filteredMembers.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

            StateHasChanged();
        }

        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(memberFilter) ||
                   !string.IsNullOrWhiteSpace(contactFilter) ||
                   !string.IsNullOrWhiteSpace(genderFilter) ||
                   !string.IsNullOrWhiteSpace(birthdayFilter) ||
                   !string.IsNullOrWhiteSpace(createdDateFilter);
        }

        private void ClearFilters()
        {
            memberFilter = string.Empty;
            contactFilter = string.Empty;
            genderFilter = string.Empty;
            birthdayFilter = string.Empty;
            createdDateFilter = string.Empty;
            currentPage = 1;
            UpdateKPIsFromFilteredData(); // Add this line
            ApplyFilters();
        }

        public class MemberStatisticDetail
        {
            public string MasterAccountID { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string NRIC { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public int BirthYear { get; set; }
            public int BirthMonth { get; set; }
            public int BirthDay { get; set; }
            public DateTime AccountSince { get; set; }
        }

        public class MemberStatisticSummary
        {
            public int TotalCustomers { get; set; }
            public int Today { get; set; }
            public int ThisMonth { get; set; }
            public int LastMonth { get; set; }
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await LoadSummaryDataAsync();
            await LoadAllDetailDataAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task SetDateRange(string range)
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

            currentPage = 1;
            await LoadAllDetailDataAsync();
            StateHasChanged();
        }

        private async Task SelectGender(string gender)
        {
            selectedGender = gender;
            currentPage = 1;

            // Recalculate KPIs based on filtered data
            UpdateKPIsFromFilteredData();

            ApplyFilters();
            StateHasChanged();
        }

        private void UpdateKPIsFromFilteredData()
        {
            if (allMembers == null || !allMembers.Any())
            {
                totalMembers = 0;
                todayMembers = 0;
                thisMonthMembers = 0;
                lastMonthMembers = 0;
                monthlyGrowthRate = 0;
                return;
            }

            // Apply gender filter to all data
            var filteredData = allMembers.AsEnumerable();

            if (selectedGender == "Male")
            {
                filteredData = filteredData.Where(x => IsMale(x.Gender));
            }
            else if (selectedGender == "Female")
            {
                filteredData = filteredData.Where(x => IsFemale(x.Gender));
            }
            // "All" - no filter

            var filteredList = filteredData.ToList();
            var today = DateTime.Today;
            var todayStart = today.Date;
            var todayEnd = today.Date.AddDays(1).AddTicks(-1);

            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);
            var lastDayOfLastMonth = firstDayOfMonth.AddDays(-1);

            // Calculate statistics from filtered data
            totalMembers = filteredList.Count;
            todayMembers = filteredList.Count(m => m.AccountSince.Date == today.Date);
            thisMonthMembers = filteredList.Count(m => m.AccountSince >= firstDayOfMonth && m.AccountSince <= lastDayOfMonth);
            lastMonthMembers = filteredList.Count(m => m.AccountSince >= firstDayOfLastMonth && m.AccountSince <= lastDayOfLastMonth);

            // Calculate growth rate
            if (lastMonthMembers > 0)
            {
                monthlyGrowthRate = ((decimal)(thisMonthMembers - lastMonthMembers) / lastMonthMembers) * 100;
            }
            else
            {
                monthlyGrowthRate = thisMonthMembers > 0 ? 100 : 0;
            }
        }

        private async Task RefreshData()
        {
            // Validate date range
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
                return;
            }

            currentPage = 1;
            await LoadSummaryDataAsync(); // Keep this for initial data
            await LoadAllDetailDataAsync();
            UpdateKPIsFromFilteredData(); // Add this line
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private async Task LoadSummaryDataAsync()
        {
            var branchId = GetBranchDisplay();

            try
            {
                var result = await ReportService.GetMemberStatisticSummaryAsync(branchId);

                if (result != null && result.StatusCode == 200 && result.Result != null)
                {
                    totalMembers = result.Result.TotalCustomers;
                    todayMembers = result.Result.Today;
                    thisMonthMembers = result.Result.ThisMonth;
                    lastMonthMembers = result.Result.LastMonth;

                    if (lastMonthMembers > 0)
                    {
                        monthlyGrowthRate = ((decimal)(thisMonthMembers - lastMonthMembers) / lastMonthMembers) * 100;
                    }
                    else
                    {
                        monthlyGrowthRate = thisMonthMembers > 0 ? 100 : 0;
                    }
                }
                else
                {
                    totalMembers = 0;
                    todayMembers = 0;
                    thisMonthMembers = 0;
                    lastMonthMembers = 0;
                    monthlyGrowthRate = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading summary data: {ex.Message}");
                totalMembers = 0;
                todayMembers = 0;
                thisMonthMembers = 0;
                lastMonthMembers = 0;
                monthlyGrowthRate = 0;
            }
        }

        private async Task LoadAllDetailDataAsync()
        {
            isLoading = true;
            exportMessage = null;
            StateHasChanged();

            var branchId = GetBranchDisplay();
            var startDateTime = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);
            var endDateTime = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 00);

            try
            {
                var allResponse = await ReportService.GetMemberStatisticDetailAsync(
                    branchId,
                    startDateTime,
                    endDateTime,
                    1,
                    10000);

                if (allResponse != null && allResponse.StatusCode == 200 && allResponse.Result != null)
                {
                    allMembers = allResponse.Result;
                    currentPage = 1;
                    UpdateKPIsFromFilteredData(); // Add this line
                    ApplyFilters();
                    exportMessage = null;
                }
                else
                {
                    allMembers = new List<MemberStatisticDetail>();
                    filteredMembers = new List<MemberStatisticDetail>();
                    members = new List<MemberStatisticDetail>();
                    totalFilteredCount = 0;
                    totalPages = 1;
                    hasMoreData = false;
                    currentPage = 1;
                    totalMembers = 0; // Reset KPIs
                    todayMembers = 0;
                    thisMonthMembers = 0;
                    lastMonthMembers = 0;
                    monthlyGrowthRate = 0;

                    if (allResponse == null)
                    {
                        exportMessage = "API returned null response";
                        alertType = "warning";
                    }
                    else if (allResponse.StatusCode != 200)
                    {
                        exportMessage = $"API Error (Status: {allResponse.StatusCode}): {allResponse.Message ?? "Unknown error"}";
                        alertType = "warning";
                    }
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                allMembers = new List<MemberStatisticDetail>();
                filteredMembers = new List<MemberStatisticDetail>();
                members = new List<MemberStatisticDetail>();
                totalFilteredCount = 0;
                totalPages = 1;
                hasMoreData = false;
                currentPage = 1;
                totalMembers = 0;
                todayMembers = 0;
                thisMonthMembers = 0;
                lastMonthMembers = 0;
                monthlyGrowthRate = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private string GetBirthday(MemberStatisticDetail member)
        {
            if (member.BirthYear > 0 && member.BirthMonth > 0 && member.BirthDay > 0)
            {
                try
                {
                    var birthday = new DateTime(member.BirthYear, member.BirthMonth, member.BirthDay);
                    return birthday.ToString("dd/MM/yyyy");
                }
                catch
                {
                    return "";
                }
            }
            return "";
        }

        // Pagination methods
        private async Task GoToFirstPage()
        {
            if (currentPage != 1)
            {
                currentPage = 1;
                members = filteredMembers.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
                hasMoreData = currentPage < totalPages;
                StateHasChanged();
            }
        }

        private async Task GoToPreviousPage()
        {
            if (currentPage > 1)
            {
                currentPage--;
                members = filteredMembers.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
                hasMoreData = currentPage < totalPages;
                StateHasChanged();
            }
        }

        private async Task GoToNextPage()
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                members = filteredMembers.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
                hasMoreData = currentPage < totalPages;
                StateHasChanged();
            }
        }

        private async Task GoToLastPage()
        {
            if (currentPage != totalPages)
            {
                currentPage = totalPages;
                members = filteredMembers.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
                hasMoreData = false; // At last page, so no more data
                StateHasChanged();
            }
        }

        private async Task OnPageSizeChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int newSize))
            {
                pageSize = newSize;
                currentPage = 1;
                totalPages = (int)Math.Ceiling((double)totalFilteredCount / pageSize);
                if (totalPages == 0) totalPages = 1;
                hasMoreData = currentPage < totalPages;
                members = filteredMembers.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
                StateHasChanged();
            }
        }

        private string GetDateRangeText()
        {
            var startDateFormatted = startDate.ToString("dd/MM/yyyy");
            var endDateFormatted = endDate.ToString("dd/MM/yyyy");

            if (startDateFormatted == endDateFormatted)
                return startDateFormatted;

            return $"{startDateFormatted} - {endDateFormatted}";
        }

        private async Task ExportToCSV()
        {
            // Close dropdown first
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMembers.Any() ? filteredMembers : allMembers;

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
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Gender Filter Tab\",\"{selectedGender}\",");
            csv.AppendLine($"\"Total Members\",,{totalMembers.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Today\",,{todayMembers.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"This Month\",,{thisMonthMembers.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Last Month\",,{lastMonthMembers.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Monthly Growth Rate\",,{monthlyGrowthRate:N2}%");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(memberFilter))
                    csv.AppendLine($"\"  - Member Filter\",\"{EscapeCsvValue(memberFilter)}\",");
                if (!string.IsNullOrWhiteSpace(contactFilter))
                    csv.AppendLine($"\"  - Contact Filter\",\"{EscapeCsvValue(contactFilter)}\",");
                if (!string.IsNullOrWhiteSpace(genderFilter))
                    csv.AppendLine($"\"  - Gender Filter\",\"{EscapeCsvValue(genderFilter)}\",");
                if (!string.IsNullOrWhiteSpace(birthdayFilter))
                    csv.AppendLine($"\"  - Birthday Filter\",\"{EscapeCsvValue(birthdayFilter)}\",");
                if (!string.IsNullOrWhiteSpace(createdDateFilter))
                    csv.AppendLine($"\"  - Created Date Filter\",\"{EscapeCsvValue(createdDateFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Member / Member ID\",\"Phone / IC\",\"Gender\",\"Birthday\",\"Created Date\"");

            foreach (var member in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(member.AccountName)} ({member.MasterAccountID})\",\"{EscapeCsvValue(member.Phone)} / {member.NRIC}\",\"{GetGenderDisplay(member.Gender)}\",\"{GetBirthday(member)}\",\"{member.AccountSince:dd/MM/yyyy}\"");
            }

            var fileName = $"MemberList_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
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

            var dataToExport = filteredMembers.Any() ? filteredMembers : allMembers;

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
                var fileName = $"MemberList_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

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

        private byte[] GeneratePdfDocument(List<MemberStatisticDetail> dataToExport)
        {
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
                        .Text("Member List Report")
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
                                text.Span($"Date Range: {GetDateRangeText()} | ");
                                text.Span($"Gender Filter Tab: {selectedGender}");
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
                                if (!string.IsNullOrWhiteSpace(contactFilter))
                                    column.Item().Text($"  - Contact: {contactFilter}");
                                if (!string.IsNullOrWhiteSpace(genderFilter))
                                    column.Item().Text($"  - Gender: {genderFilter}");
                                if (!string.IsNullOrWhiteSpace(birthdayFilter))
                                    column.Item().Text($"  - Birthday: {birthdayFilter}");
                                if (!string.IsNullOrWhiteSpace(createdDateFilter))
                                    column.Item().Text($"  - Created Date: {createdDateFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            // Summary Statistics row
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("Total Members").FontSize(7);
                                    col.Item().Text(totalMembers.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("Today").FontSize(7);
                                    col.Item().Text(todayMembers.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("This Month").FontSize(7);
                                    col.Item().Text(thisMonthMembers.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("Last Month").FontSize(7);
                                    col.Item().Text(lastMonthMembers.ToString("N0")).FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                });
                                row.RelativeItem().Padding(4).Background(Colors.Grey.Lighten3).Column(col =>
                                {
                                    col.Item().Text("Growth Rate").FontSize(7);
                                    col.Item().Text($"{monthlyGrowthRate:N2}%").FontSize(11).Bold().FontColor(Colors.Brown.Medium);
                                });
                            });

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(2f).Padding(4).Background(Colors.Brown.Medium).Text("Member / Member ID").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1.8f).Padding(4).Background(Colors.Brown.Medium).Text("Phone / IC").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(0.8f).Padding(4).Background(Colors.Brown.Medium).Text("Gender").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1f).Padding(4).Background(Colors.Brown.Medium).Text("Birthday").FontColor(Colors.White).Bold().FontSize(7);
                                row.RelativeItem(1f).Padding(4).Background(Colors.Brown.Medium).Text("Created Date").FontColor(Colors.White).Bold().FontSize(7);
                            });

                            // Table Rows
                            foreach (var member in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(2f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                                    {
                                        col.Item().Text(member.AccountName ?? "").FontSize(8);
                                        col.Item().Text(member.MasterAccountID).FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(1.8f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                                    {
                                        col.Item().Text(member.Phone ?? "").FontSize(8);
                                        col.Item().Text(member.NRIC).FontSize(7).FontColor(Colors.Grey.Darken1);
                                    });
                                    row.RelativeItem(0.8f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text(GetGenderDisplay(member.Gender)).FontSize(8);
                                    row.RelativeItem(1f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text(GetBirthday(member)).FontSize(8);
                                    row.RelativeItem(1f).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text(member.AccountSince.ToString("dd/MM/yyyy")).FontSize(8);
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
    }
}