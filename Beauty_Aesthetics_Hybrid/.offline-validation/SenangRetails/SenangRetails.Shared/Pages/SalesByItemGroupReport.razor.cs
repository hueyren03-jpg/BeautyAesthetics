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
using static SenangRetails.Shared.Pages.SalesByItemReport;


namespace SenangRetails.Shared.Pages
{
    public partial class SalesByItemGroupReport
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;

        private List<ItemGroupData> itemGroupData = new();
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private string selectedTypeTab = "Service";
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Summary totals
        private decimal totalSales = 0;
        private int totalItemGroups = 0;
        private int totalItems = 0;

        // Initialize QuestPDF license
        static SalesByItemGroupReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetBranchDisplay()
        {
            return string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
        }

        private int GetInventoryTypeId(string type)
        {
            return type switch
            {
                "Service" => 3,
                "Product" => 1,
                "Package" => 5,
                "TopUp" => 7,
                _ => 0
            };
        }

        private string GetItemIcon(string type)
        {
            return type switch
            {
                "Service" => "bi-star",
                "Product" => "bi-box",
                "Package" => "bi-gift",
                "TopUp" => "bi-plus-circle",
                _ => "bi-tag"
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
                var branchId = GetBranchDisplay();
                var startDateTime = GetStartDateTime(startDate);
                var endDateTime = GetEndDateTime(endDate);
                var inventoryTypeId = GetInventoryTypeId(selectedTypeTab);

                var apiResponse = await ReportService.GetSalesByItemAsync(
                    startDateTime,
                    endDateTime,
                    branchId,
                    inventoryTypeId);

                List<SalesBySKUItem> allItems = new();


                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Result != null)
                {
                    allItems = apiResponse.Result.ToList();
                }

                // Hardcode the Type based on selected tab
                var hardcodedType = selectedTypeTab;

                // Group by Item Group Name
                itemGroupData = allItems
                    .GroupBy(x => x.ItemGroupName ?? "Uncategorized")
                    .Select((group, index) => new ItemGroupData
                    {
                        ItemGroupId = index + 1,
                        ItemGroupName = string.IsNullOrEmpty(group.Key) ? "Uncategorized" : group.Key,
                        TotalQuantity = (int)group.Sum(x => x.Quantity),
                        TotalAmount = group.Sum(x => x.SubTotal),
                        IsExpanded = false,
                        Items = group.Select(item => new ItemGroupItem
                        {
                            ItemName = item.Description,
                            ItemCode = item.ProductCode ?? string.Empty,
                            Type = hardcodedType,
                            Quantity = item.Quantity,
                            Amount = item.SubTotal,
                            UOM = item.UOM,
                            UnitCost = item.UnitCost,
                            UnitPrice = item.UnitSalesPrice,
                            Profit = item.Profit
                        }).ToList()
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                // Calculate totals
                totalItemGroups = itemGroupData.Count;
                totalSales = itemGroupData.Sum(x => x.TotalAmount);
                totalItems = itemGroupData.Sum(x => x.Items.Sum(i => (int)i.Quantity));
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                itemGroupData = new List<ItemGroupData>();
                totalSales = 0;
                totalItemGroups = 0;
                totalItems = 0;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task SelectTypeTab(string tab)
        {
            selectedTypeTab = tab;
            await LoadDataAsync();
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

        private void ToggleItemGroup(int itemGroupId)
        {
            var group = itemGroupData.FirstOrDefault(g => g.ItemGroupId == itemGroupId);
            if (group != null)
            {
                group.IsExpanded = !group.IsExpanded;
                StateHasChanged();
            }
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
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

            if (!itemGroupData.Any())
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

            // Report Summary Section
            csv.AppendLine($"\"Report Summary\",,");
            csv.AppendLine($"\"Branch\",\"{GetBranchDisplay()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateRangeText()}\",");
            csv.AppendLine($"\"Report Type\",\"{selectedTypeTab}\",");
            csv.AppendLine($"\"Total Sales\",,{totalSales.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Item Groups\",,{totalItemGroups.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Items\",,{totalItems.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");
            csv.AppendLine();

            // Data Header - 5 columns
            csv.AppendLine($"\"{LangSvc.GetText("Item Group")}\",\"{LangSvc.GetText("Item Name")}\",\"{LangSvc.GetText("Item Type")}\",\"{LangSvc.GetText("Quantity")}\",\"{LangSvc.GetText("Amount")}\"");

            // Data rows
            foreach (var group in itemGroupData)
            {
                // Group row
                csv.AppendLine($"\"{EscapeCsvValue(group.ItemGroupName)}\",\"\",\"\",{group.TotalQuantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{group.TotalAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");

                // Item rows
                foreach (var item in group.Items)
                {
                    csv.AppendLine($"\"\",\"{EscapeCsvValue(item.ItemName)}\",\"{EscapeCsvValue(item.Type)}\",{item.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
                }
            }

            var fileName = $"SalesByItemGroup_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
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

            if (!itemGroupData.Any())
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
                var pdfBytes = GeneratePdfDocument();
                var fileName = $"SalesByItemGroup_{selectedTypeTab}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument()
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
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(10));

                    page.Header()
                        .AlignCenter()
                        .Text("Sales By Item Group Report")
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
                                text.Span("Report Type: ").Bold();
                                text.Span(selectedTypeTab);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Date Range: {GetDateRangeText()}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Sales: RM ").Bold();
                                text.Span($"{totalSales:N2}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Item Groups: ").Bold();
                                text.Span($"{totalItemGroups:N0}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Items: ").Bold();
                                text.Span($"{totalItems:N0}");
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Generated On: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                                text.Line("");
                            });

                            column.Item().PaddingVertical(10).LineHorizontal(1);

                            // Table Header - 5 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(1.5f).Padding(8).Background(Colors.Brown.Medium).Text("Item Group").FontColor(Colors.White).Bold();
                                row.RelativeItem(2f).Padding(8).Background(Colors.Brown.Medium).Text("Item Name").FontColor(Colors.White).Bold();
                                row.RelativeItem(0.8f).Padding(8).Background(Colors.Brown.Medium).Text("Type").FontColor(Colors.White).Bold();
                                row.RelativeItem(0.7f).Padding(8).Background(Colors.Brown.Medium).AlignRight().Text("Qty").FontColor(Colors.White).Bold();
                                row.RelativeItem(1f).Padding(8).Background(Colors.Brown.Medium).AlignRight().Text("Amount (RM)").FontColor(Colors.White).Bold();
                            });

                            // Table Rows
                            foreach (var group in itemGroupData)
                            {
                                // Group row (bold background)
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1.5f).Padding(8).Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text(group.ItemGroupName).Bold();
                                    row.RelativeItem(2f).Padding(8).Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text("");
                                    row.RelativeItem(0.8f).Padding(8).Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text("");
                                    row.RelativeItem(0.7f).Padding(8).Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{group.TotalQuantity:N0}").Bold();
                                    row.RelativeItem(1f).Padding(8).Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{group.TotalAmount:N2}").Bold();
                                });

                                // Item rows
                                foreach (var item in group.Items)
                                {
                                    column.Item().Row(row =>
                                    {
                                        row.RelativeItem(1.5f).PaddingLeft(20).PaddingTop(8).PaddingRight(8).PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                            .Text("");
                                        row.RelativeItem(2f).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                            .Text(item.ItemName);
                                        row.RelativeItem(0.8f).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                            .Text(item.Type);
                                        row.RelativeItem(0.7f).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                            .AlignRight().Text($"{item.Quantity:N2}");
                                        row.RelativeItem(1f).Padding(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                            .AlignRight().Text($"{item.Amount:N2}");
                                    });
                                }
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

        public class ItemGroupData
        {
            public int ItemGroupId { get; set; }
            public string ItemGroupName { get; set; } = string.Empty;
            public int TotalQuantity { get; set; }
            public decimal TotalAmount { get; set; }
            public bool IsExpanded { get; set; }
            public List<ItemGroupItem> Items { get; set; } = new();
        }

        public class ItemGroupItem
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string ItemCode { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string UOM { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal UnitCost { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Amount { get; set; }
            public decimal Profit { get; set; }
        }
    }
}