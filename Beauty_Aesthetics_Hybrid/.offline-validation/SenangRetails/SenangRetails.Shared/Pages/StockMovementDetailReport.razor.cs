using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EBI.DM;
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
using SenangRetails.Shared.Services.InventoryService;

namespace SenangRetails.Shared.Components.Reports
{
    public partial class StockMovementDetailReport
    {
       [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private IFileDownloadService FileDownloadService { get; set; } = default!;
        [Inject] private IInventoryService InventoryService { get; set; } = default!;

        // State variables
        private string selectedDateRange = "Today";
        private DateTime startDate = DateTime.Today;
        private DateTime endDate = DateTime.Today;
        private bool isLoading = false;
        private string? exportMessage;
        private string alertType = "info";
        private bool showExportDropdown = false;

        // Item selection state (Single Selection - No All Items)
        private bool showItemModal = false;
        private string selectedItemDisplay = string.Empty;
        private string _selectedItemId = string.Empty; // For single selection staging
        private string _itemSearchTerm = string.Empty;
        private List<InventoryDM> masterItemList = new();
        private List<InventoryDM> filteredItemList = new();
        private string selectedItemId = string.Empty; // Final selection

        // Data
        private List<StockMovementDetail> movementData = new();
        private List<StockMovementDetail> filteredMovementData = new();

        // Filter properties
        private string branchIdFilter = string.Empty;
        private string dateFilter = string.Empty;
        private string documentTypeFilter = string.Empty;
        private string documentNoFilter = string.Empty;
        private string descriptionFilter = string.Empty;
        private string itemNameFilter = string.Empty;
        private string quantityFilter = string.Empty;

        // Summary totals
        private int totalItems = 0;
        private decimal totalQuantity = 0;
        private int totalDocuments = 0;
        private int totalBranches = 0;

        private bool isLoadingItems = false;

        static StockMovementDetailReport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            // Disable the glyph availability check
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        private string GetDocumentTypeClass(string documentType)
        {
            return documentType?.ToLower() switch
            {
                "purchase" => "badge-purchase",
                "sales" => "badge-sales",
                "transfer" => "badge-transfer",
                "adjustment" => "badge-adjustment",
                "return" => "badge-return",
                _ => "badge-default"
            };
        }

        private void ToggleExportDropdown()
        {
            showExportDropdown = !showExportDropdown;
        }

        private void ApplyFilters()
        {
            if (movementData == null || !movementData.Any())
            {
                filteredMovementData = new List<StockMovementDetail>();
                return;
            }

            var query = movementData.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(branchIdFilter))
            {
                query = query.Where(x => x.BranchID != null &&
                    x.BranchID.Contains(branchIdFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                query = query.Where(x => x.FinancialDate.ToString("dd/MM/yyyy HH:mm")
                    .Contains(dateFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(documentTypeFilter))
            {
                query = query.Where(x => x.DocumentType != null &&
                    x.DocumentType.Contains(documentTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(documentNoFilter))
            {
                query = query.Where(x => x.DocumentNo != null &&
                    x.DocumentNo.Contains(documentNoFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(descriptionFilter))
            {
                query = query.Where(x => x.Description != null &&
                    x.Description.Contains(descriptionFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(itemNameFilter))
            {
                query = query.Where(x => x.InventoryName != null &&
                    x.InventoryName.Contains(itemNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(quantityFilter))
            {
                query = ApplyNumericFilter(query, x => x.Quantity, quantityFilter);
            }

            filteredMovementData = query.ToList();
            StateHasChanged();
        }

        private IEnumerable<StockMovementDetail> ApplyNumericFilter(
            IEnumerable<StockMovementDetail> query,
            Func<StockMovementDetail, decimal> selector,
            string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return query;

            var conditions = ParseNumericFilter(filterText);
            if (conditions.HasValue)
            {
                if (conditions.Value.min.HasValue)
                    query = query.Where(x => selector(x) >= conditions.Value.min.Value);
                if (conditions.Value.max.HasValue)
                    query = query.Where(x => selector(x) <= conditions.Value.max.Value);
            }
            return query;
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
            return !string.IsNullOrWhiteSpace(branchIdFilter) ||
                   !string.IsNullOrWhiteSpace(dateFilter) ||
                   !string.IsNullOrWhiteSpace(documentTypeFilter) ||
                   !string.IsNullOrWhiteSpace(documentNoFilter) ||
                   !string.IsNullOrWhiteSpace(descriptionFilter) ||
                   !string.IsNullOrWhiteSpace(itemNameFilter) ||
                   !string.IsNullOrWhiteSpace(quantityFilter);
        }

        private void ClearFilters()
        {
            branchIdFilter = string.Empty;
            dateFilter = string.Empty;
            documentTypeFilter = string.Empty;
            documentNoFilter = string.Empty;
            descriptionFilter = string.Empty;
            itemNameFilter = string.Empty;
            quantityFilter = string.Empty;
            ApplyFilters();
        }

        private DateTime GetCutOffDateTime(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 23, 59, 00);
        }

        // Item Selection Methods (Single Selection - No All Items)
        private string ItemSearchTerm
        {
            get => _itemSearchTerm;
            set
            {
                if (_itemSearchTerm != value)
                {
                    _itemSearchTerm = value;
                    FilterItemModalList();
                }
            }
        }

        private async Task OpenItemSelectionModal()
        {
            _itemSearchTerm = string.Empty;

            // Wait for items to load if they haven't finished yet
            if (_itemsLoadTask != null && !_itemsLoadTask.IsCompleted)
            {
                isLoadingItems = true;
                showItemModal = true;
                StateHasChanged();

                await _itemsLoadTask;

                isLoadingItems = false;
            }
            else if (!masterItemList.Any())
            {
                // Fallback - load if empty
                isLoadingItems = true;
                showItemModal = true;
                StateHasChanged();

                await LoadItemsAsync();

                isLoadingItems = false;
            }

            filteredItemList = new List<InventoryDM>(masterItemList);
            _selectedItemId = selectedItemId;

            if (!showItemModal)
            {
                showItemModal = true;
            }

            StateHasChanged();
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                var items = await InventoryService.LoadItemsAsync(AppState.SelectedBranchID);
                if (items != null)
                {
                    // Only load inventory type ID = 1 (Products)
                    masterItemList = items
                        .Where(x => x.InventoryTypeID == 1)
                        .OrderBy(x => x.AccountName)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading items: {ex.Message}");
            }
        }

        private void CloseItemModal()
        {
            showItemModal = false;
            _selectedItemId = string.Empty;
            _itemSearchTerm = string.Empty;
        }

        private void FilterItemModalList()
        {
            if (string.IsNullOrWhiteSpace(_itemSearchTerm))
            {
                filteredItemList = new List<InventoryDM>(masterItemList);
            }
            else
            {
                filteredItemList = masterItemList
                    .Where(item => item.AccountName != null &&
                        item.AccountName.Contains(_itemSearchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            StateHasChanged();
        }

        // Single selection - select one item
        private void SelectSingleItem(InventoryDM item)
        {
            if (_selectedItemId == item.MasterAccountID)
            {
                // Deselect if already selected
                _selectedItemId = string.Empty;
            }
            else
            {
                _selectedItemId = item.MasterAccountID;
            }
            StateHasChanged();
        }

        private bool IsItemSelected(string itemId)
        {
            return _selectedItemId == itemId;
        }

        private async Task ConfirmItemSelection()
        {
            if (!string.IsNullOrEmpty(_selectedItemId))
            {
                selectedItemId = _selectedItemId;
                var item = masterItemList.FirstOrDefault(x => x.MasterAccountID == _selectedItemId);
                selectedItemDisplay = item?.AccountName ?? _selectedItemId;
            }
            else
            {
                // No selection - clear
                selectedItemId = string.Empty;
                selectedItemDisplay = "Select Item";
            }

            showItemModal = false;
            _selectedItemId = string.Empty;
            _itemSearchTerm = string.Empty;

            await LoadDataAsync();
            StateHasChanged();
        }

        private string GetItemIDsForAPI()
        {
            if (string.IsNullOrEmpty(selectedItemId))
                return "ALL";
            return selectedItemId;
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            exportMessage = null;
            StateHasChanged();

            try
            {
                var branchID = AppState.SelectedBranchID ?? "HQ";
                var start = GetCutOffDateTime(startDate);
                var end = GetCutOffDateTime(endDate);
                var inventoryID = GetItemIDsForAPI();

                var apiResult = await ReportService.GetStockMovementDetailAsync(
                    branchID, inventoryID, start, end);

                if (apiResult != null && apiResult.StatusCode == 200 && apiResult.Result != null)
                {
                    movementData = apiResult.Result;

                    totalItems = movementData.Count;
                    totalQuantity = movementData.Sum(x => x.Quantity);
                    totalDocuments = movementData.Select(x => x.DocumentNo).Distinct().Count();
                    totalBranches = movementData.Select(x => x.BranchID).Distinct().Count();

                    ApplyFilters();
                }
                else
                {
                    exportMessage = apiResult?.Message ?? "No data available";
                    alertType = "warning";
                    movementData = new List<StockMovementDetail>();
                    filteredMovementData = new List<StockMovementDetail>();
                    ResetTotals();
                }
            }
            catch (Exception ex)
            {
                exportMessage = $"Error loading data: {ex.Message}";
                alertType = "error";
                movementData = new List<StockMovementDetail>();
                filteredMovementData = new List<StockMovementDetail>();
                ResetTotals();
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void ResetTotals()
        {
            totalItems = 0;
            totalQuantity = 0;
            totalDocuments = 0;
            totalBranches = 0;
        }

        private async Task RefreshDataAsync()
        {
            // Validate date range
            if (startDate > endDate)
            {
                ShowNotification("Start Date cannot fall after the specified End Date.");
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
                    startDate = today;
                    endDate = today;
                    break;
                case "Week":
                    int daysUntilEndOfWeek = (7 - (int)today.DayOfWeek + (int)DayOfWeek.Saturday) % 7;
                    startDate = today.AddDays(-(int)today.DayOfWeek + 1);
                    endDate = today.AddDays(daysUntilEndOfWeek);
                    break;
                case "Month":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
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

        private string GetDateText()
        {
            return $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
        }

        private async Task ExportToCSV()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMovementData.Any() ? filteredMovementData : movementData;

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
            csv.AppendLine($"\"Branch ID\",\"{AppState.SelectedBranchID ?? "HQ"}\",");
            csv.AppendLine($"\"Item\",\"{GetItemDisplayText()}\",");
            csv.AppendLine($"\"Date Range\",\"{GetDateText()}\",");
            csv.AppendLine($"\"Total Items\",,{totalItems.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Quantity\",,{totalQuantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Documents\",,{totalDocuments.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Total Branches\",,{totalBranches.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

            if (HasActiveFilters())
            {
                csv.AppendLine($"\"Filters Applied\",,");
                if (!string.IsNullOrWhiteSpace(branchIdFilter))
                    csv.AppendLine($"\"  - Branch ID Filter\",\"{EscapeCsvValue(branchIdFilter)}\",");
                if (!string.IsNullOrWhiteSpace(dateFilter))
                    csv.AppendLine($"\"  - Date Filter\",\"{EscapeCsvValue(dateFilter)}\",");
                if (!string.IsNullOrWhiteSpace(documentTypeFilter))
                    csv.AppendLine($"\"  - Document Type Filter\",\"{EscapeCsvValue(documentTypeFilter)}\",");
                if (!string.IsNullOrWhiteSpace(documentNoFilter))
                    csv.AppendLine($"\"  - Document No Filter\",\"{EscapeCsvValue(documentNoFilter)}\",");
                if (!string.IsNullOrWhiteSpace(descriptionFilter))
                    csv.AppendLine($"\"  - Description Filter\",\"{EscapeCsvValue(descriptionFilter)}\",");
                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                    csv.AppendLine($"\"  - Item Name Filter\",\"{EscapeCsvValue(itemNameFilter)}\",");
                if (!string.IsNullOrWhiteSpace(quantityFilter))
                    csv.AppendLine($"\"  - Quantity Filter\",\"{EscapeCsvValue(quantityFilter)}\",");
            }

            csv.AppendLine();

            csv.AppendLine($"\"Branch ID\",\"Date\",\"Document Type\",\"Document No\",\"Description\",\"Item Name\",\"Quantity\"");

            foreach (var item in dataToExport)
            {
                csv.AppendLine($"\"{EscapeCsvValue(item.BranchID)}\",\"{item.FinancialDate:dd/MM/yyyy HH:mm}\",\"{EscapeCsvValue(item.DocumentType)}\",\"{EscapeCsvValue(item.DocumentNo)}\",\"{EscapeCsvValue(item.Description)}\",\"{EscapeCsvValue(item.InventoryName)}\",{item.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var fileName = $"StockMovementDetail_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.csv";
            var csvContent = csv.ToString();
            var csvBytes = Encoding.UTF8.GetBytes(csvContent);
            var base64Content = Convert.ToBase64String(csvBytes);

            await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, "text/csv");

            exportMessage = LangSvc.GetText("CSV export completed!");
            alertType = "success";
            StateHasChanged();
            await Task.Delay(3000);
            exportMessage = null;
            StateHasChanged();
        }

        private string GetItemDisplayText()
        {
            if (string.IsNullOrEmpty(selectedItemId))
                return "All Items";
            return selectedItemDisplay;
        }

        private string EscapeCsvValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\"", "\"\"");
        }

        private async Task ExportToPDF()
        {
            showExportDropdown = false;
            StateHasChanged();

            var dataToExport = filteredMovementData.Any() ? filteredMovementData : movementData;

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
                var fileName = $"StockMovementDetail_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf";

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

        private byte[] GeneratePdfDocument(List<StockMovementDetail> dataToExport)
        {
            var branchID = AppState.SelectedBranchID ?? "HQ";
            var itemDisplay = GetItemDisplayText();

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
                    page.Size(PageSizes.A3.Landscape());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                       .FontFamily(fontFamily)
                       .FontSize(7));

                    page.Header()
                        .AlignCenter()
                        .Text("Stock Movement Detail Report")
                        .SemiBold().FontSize(14).FontColor(Colors.Brown.Medium);

                    page.Content()
                        .PaddingVertical(5f)
                        .Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Report Summary").Bold().FontSize(9);
                                text.Line("");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Branch ID: {branchID} | ");
                                text.Span($"Item: {itemDisplay} | ");
                                text.Span($"Date Range: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
                            });

                            column.Item().Text(text =>
                            {
                                text.Span($"Total Items: {totalItems:N0} | ");
                                text.Span($"Total Quantity: {totalQuantity:N2} | ");
                                text.Span($"Total Documents: {totalDocuments:N0} | ");
                                text.Span($"Total Branches: {totalBranches:N0}");
                            });

                            if (HasActiveFilters())
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Filters Applied:").Bold();
                                    text.Line("");
                                });
                                if (!string.IsNullOrWhiteSpace(branchIdFilter))
                                    column.Item().Text($"  - Branch ID: {branchIdFilter}");
                                if (!string.IsNullOrWhiteSpace(dateFilter))
                                    column.Item().Text($"  - Date: {dateFilter}");
                                if (!string.IsNullOrWhiteSpace(documentTypeFilter))
                                    column.Item().Text($"  - Document Type: {documentTypeFilter}");
                                if (!string.IsNullOrWhiteSpace(documentNoFilter))
                                    column.Item().Text($"  - Document No: {documentNoFilter}");
                                if (!string.IsNullOrWhiteSpace(descriptionFilter))
                                    column.Item().Text($"  - Description: {descriptionFilter}");
                                if (!string.IsNullOrWhiteSpace(itemNameFilter))
                                    column.Item().Text($"  - Item Name: {itemNameFilter}");
                                if (!string.IsNullOrWhiteSpace(quantityFilter))
                                    column.Item().Text($"  - Quantity: {quantityFilter}");
                                column.Item().PaddingVertical(5);
                            }

                            column.Item().PaddingVertical(5f).LineHorizontal(1f);

                            // Table Header - 7 columns
                            column.Item().Row(row =>
                            {
                                row.RelativeItem(0.8f).Padding(3f).Background(Colors.Brown.Medium).Text("Branch ID").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(1.2f).Padding(3f).Background(Colors.Brown.Medium).Text("Date").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(1.0f).Padding(3f).Background(Colors.Brown.Medium).Text("Document Type").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(1.0f).Padding(3f).Background(Colors.Brown.Medium).Text("Document No").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(1.5f).Padding(3f).Background(Colors.Brown.Medium).Text("Description").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(1.5f).Padding(3f).Background(Colors.Brown.Medium).Text("Item Name").FontColor(Colors.White).Bold().FontSize(6);
                                row.RelativeItem(0.8f).Padding(3f).Background(Colors.Brown.Medium).AlignRight().Text("Quantity").FontColor(Colors.White).Bold().FontSize(6);
                            });

                            // Table Rows
                            foreach (var item in dataToExport)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(0.8f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.BranchID) ? "" : item.BranchID).FontSize(6);
                                    row.RelativeItem(1.2f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(item.FinancialDate.ToString("dd/MM/yyyy HH:mm")).FontSize(6);
                                    row.RelativeItem(1.0f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.DocumentType) ? "" : item.DocumentType).FontSize(6);
                                    row.RelativeItem(1.0f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.DocumentNo) ? "" : item.DocumentNo).FontSize(6);
                                    row.RelativeItem(1.5f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.Description) ? "" : item.Description).FontSize(6);
                                    row.RelativeItem(1.5f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .Text(string.IsNullOrEmpty(item.InventoryName) ? "" : item.InventoryName).FontSize(6);
                                    row.RelativeItem(0.8f).Padding(3f).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{item.Quantity:N2}").FontSize(6);
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

        private Task _itemsLoadTask;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            // Initialize with no item selected
            selectedItemId = string.Empty;
            selectedItemDisplay = "Select Item";

            // Start loading items but don't wait for it
            _itemsLoadTask = LoadItemsAsync();

            // Load data
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

        public class StockMovementDetail
        {
            public string BranchID { get; set; } = string.Empty;
            public DateTime FinancialDate { get; set; }
            public string DocumentType { get; set; } = string.Empty;
            public string DocumentNo { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string InventoryName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
        }
    }
}