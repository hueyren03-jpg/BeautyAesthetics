using EBI.DM;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.CustomerService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using SenangRetails.Shared.Services.WhatsappService;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace SenangRetails.Shared.Pages
{
    public partial class SalesHistory
    {
        [Inject] private ICashSalesService SalesService { get; set; } = default!;
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IReceiptPrinterService ReceiptPrinterSvc { get; set; } = default!;
        [Inject] private IBluetoothPrinterService PrinterSvc { get; set; } = default!;
        [Inject] private CashSalesAC _cashSalesAC { get; set; } = default!;
        [Inject] private ICustomerService _customerService { get; set; } = default!;

        private DateTime _selectedDate = DateTime.Today;
        private DateTime selectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    _ = LoadSales();
                }
            }
        }

        private string _searchAmount = "";
        private string searchAmount
        {
            get => _searchAmount;
            set
            {
                if (_searchAmount != value)
                {
                    _searchAmount = value;
                    showAmountError = !string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value, out _);
                }
            }
        }
        private bool isLoading = false;
        private List<Doc_CashSalesDM> sales = new();

        protected override async Task OnInitializedAsync()
        {
            await LoadSales();
        }

        private async Task LoadSales()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID) ? "HQ" : AppState.SelectedBranchID;
                sales = await SalesService.GetSalesHistoryAsync(branchId, selectedDate);
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private List<Doc_CashSalesDM> FilteredSales()
        {
            if (string.IsNullOrWhiteSpace(searchAmount))
            {
                return sales;
            }

            if (decimal.TryParse(searchAmount, out var targetAmt))
            {
                return sales.Where(s => s.TotalAfterTax == targetAmt).ToList();
            }

            return new List<Doc_CashSalesDM>();
        }

        private void OnSearchInput(ChangeEventArgs e)
        {
            searchAmount = e.Value?.ToString() ?? "";
        }

        private bool showDeleteConfirm = false;
        private Doc_CashSalesDM? saleToDelete = null;
        private bool showAmountError = false;

        private void ConfirmDelete(Doc_CashSalesDM sale)
        {
            if (sale.eInvoiceStatus == "Valid")
            {
                ShowNotification("This record is linked to a Valid e-Invoice and cannot be deleted.");
                return;
            }
            saleToDelete = sale;
            showDeleteConfirm = true;
        }

        private void CancelDelete()
        {
            saleToDelete = null;
            showDeleteConfirm = false;
        }

        private async Task ExecuteDelete()
        {
            if (saleToDelete != null)
            {
                if (saleToDelete.eInvoiceStatus == "Valid")
                {
                    ShowNotification("This record is linked to a Valid e-Invoice and cannot be deleted.");
                    CancelDelete();
                    return;
                }
                isLoading = true;
                StateHasChanged();

                var result = await SalesService.DeleteSaleAsync(saleToDelete.DocumentID);

                if (result.Success)
                {
                    sales.Remove(saleToDelete);
                    ShowNotification("Record Deleted Successfully");
                }
                else
                {
                    ShowNotification("Error: " + result.Message);
                }

                isLoading = false;
            }
            CancelDelete();
        }

        private void GoBack() => NavigationManager.NavigateTo("/home");

        private async void ShowNotification(string message)
        {
            try
            {
                await JS.InvokeVoidAsync("alert", message);
            }
            catch
            {
                Console.WriteLine($"NOTIF: {message}");
            }
        }

        private bool IsValidPhoneNumber
        {
            get
            {
                if (string.IsNullOrWhiteSpace(targetPhone)) return false;

                bool isNumeric = targetPhone.All(char.IsDigit);

                bool isMinLength = targetPhone.Length >= 10;

                return isNumeric && isMinLength;
            }
        }

        [Inject] private WhatsAppService WhatsApp { get; set; } = default!;

        private bool showWhatsAppModal = false;
        private bool isProcessing = false;
        private string targetPhone = "";
        private Doc_CashSalesDM? selectedSale;

        private void OpenWhatsAppPrompt(Doc_CashSalesDM sale)
        {
            selectedSale = sale;
            showWhatsAppModal = true;
        }

        private async Task ProcessWhatsApp()
        {
            if (selectedSale == null || string.IsNullOrWhiteSpace(targetPhone)) return;

            isProcessing = true;
            try
            {
                var (einvoice, bill) = await SalesService.GenerateInvoiceLinksAsync(selectedSale.DocumentID);

                if (string.IsNullOrEmpty(einvoice) && string.IsNullOrEmpty(bill))
                {
                    ShowNotification("Could not generate links");
                    return;
                }

                string companyName = AppState.CurrentBranch?.CompanyName ?? string.Empty;
                string message = WhatsApp.BuildMessage(companyName, einvoice, bill);

                await WhatsApp.OpenAsync(targetPhone, message);
                showWhatsAppModal = false;
            }
            catch (Exception ex)
            {
                ShowNotification("WhatsApp Error: " + ex.Message);
            }
            finally
            {
                isProcessing = false;
            }
        }

        private bool isDownloading = false;
        private string selectedDocId = "";

        private async Task DownloadPdf(Doc_CashSalesDM sale)
        {
            if (isDownloading) return;

            isDownloading = true;
            selectedDocId = sale.DocumentID;
            StateHasChanged();

            try
            {
                var success = await SalesService.DownloadReceiptPdfAsync(sale.DocumentID);

                if (success)
                {
                    //ShowNotification("Receipt downloaded successfully"
                    //);
                }
                else
                {
                    ShowNotification("Failed to generate PDF. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Error: " + ex.Message);
            }
            finally
            {
                isDownloading = false;
                selectedDocId = "";
                StateHasChanged();
            }
        }

        private bool isPrinting = false;

        private async Task PrintSale(Doc_CashSalesDM sale)
        {
            var printer = PrinterSvc.GetSelectedPrinter();
            if (printer == null)
            {
                ShowNotification("No printer selected. Please select a printer from the operation menu.");
                return;
            }

            try
            {
                isPrinting = true;
                selectedDocId = sale.DocumentID;
                StateHasChanged();

                var response = await _cashSalesAC.LoadRecordAsync(sale.DocumentID);
                if (response == null || response.StatusCode != 200 || response.Result == null)
                {
                    ShowNotification("Failed to load sales details from server.");
                    return;
                }

                var record = response.Result;
                var doc = record.objDoc_CashSales;

                CustomerDM? fullCustomer = null;
                if (!string.IsNullOrEmpty(doc?.AccountID))
                {
                    fullCustomer = await _customerService.GetSingleCustomer(doc.AccountID);
                }

                var branch = AppState.CurrentBranch;
                var receiptData = new ReceiptData
                {
                    CompanyName = branch?.CompanyName ?? "",
                    BranchName = branch?.Branch ?? "",
                    Address1 = branch?.Address1 ?? "",
                    Address2 = branch?.Address2 ?? "",
                    Address3 = branch?.Address3 ?? "",
                    Phone = branch?.Phone ?? "",
                    Email = branch?.Email ?? "",
                    CurrencyName = doc?.LocalCurrencyName ?? "",
                    CoRegistrationNo = branch?.CoRegistrationNo,
                    TIN = branch?.TIN,
                    CashierName = doc?.CashierName ?? "",
                    ReceiptNo = doc?.DisplayCode ?? "",
                    DateTimeOfSale = doc?.FinancialDate,
                    ReferenceNumber = doc?.ReferenceNumber ?? "",
                    CustomerName = doc?.AccountName ?? "",
                    CustomerID = doc?.AccountID ?? "",
                    CustomerAddress1 = fullCustomer?.Address1 ?? "",
                    CustomerAddress2 = fullCustomer?.Address2 ?? "",
                    CustomerCity = fullCustomer?.City ?? "",
                    CustomerPostcode = fullCustomer?.ZipCode ?? "",
                    CustomerState = fullCustomer?.CountryState ?? "",
                    CustomerCountry = fullCustomer?.Country ?? "",
                    CustomerPhone = fullCustomer?.Phone ?? "",

                    Items = record.lstDocumentLine.Select(i => new ReceiptLineItem
                    {
                        Name = i.Description,
                        Quantity = (int)i.Quantity,
                        Discount = i.Discount,
                        LineTotal = (i.UnitPrice * i.Quantity) - i.Discount,
                        Remarks = i.RefCompanyName
                    }).ToList(),

                    Payments = record.lstReceiptLines.Select(p => new ReceiptPaymentLine
                    {
                        Method = p.Description ?? "",
                        Amount = p.POSReceiptLineAmount
                    }).ToList(),

                    ChangeAmount = Math.Abs(record.lstReceiptLines
                        .FirstOrDefault(x => x.POSReceiptChangeAmount < 0)?.POSReceiptChangeAmount ?? 0m),

                    TaxSummary = record.lstDocumentLine
                        .Where(l => !string.IsNullOrEmpty(l.TaxCodeID))
                        .GroupBy(l => new { l.TaxCodeID, l.TaxPercentage })
                        .Select(g => new TaxSummaryLine
                        {
                            TaxCode = $"{g.Key.TaxCodeID} {g.Key.TaxPercentage * 100:0.#}%",
                            Amount = g.Sum(x => (x.UnitPrice * x.Quantity) - x.Discount),
                            Tax = g.Sum(x => x.TaxAmount)
                        }).ToList(),

                    Subtotal = doc?.TotalBeforeTax ?? 0,
                    RoundingAmount = doc?.RoundingAmount ?? 0,
                    GrandTotal = doc?.TotalAfterTax ?? 0,
                    PaidAmount = record.lstReceiptLines.Sum(p => p.POSReceiptLineAmount)
                };

                var (success, error) = await ReceiptPrinterSvc.PrintAsync(printer, receiptData);

                if (!success)
                {
                    ShowNotification($"Print failed: {error}");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"An error occurred during printing: {ex.Message}");
            }
            finally
            {
                isPrinting = false;
                selectedDocId = "";
                StateHasChanged();
            }
        }
    }
}
