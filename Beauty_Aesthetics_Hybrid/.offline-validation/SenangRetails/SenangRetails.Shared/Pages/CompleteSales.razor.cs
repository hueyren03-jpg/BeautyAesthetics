using Microsoft.JSInterop;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;
using SenangRetails.Shared.Services.CustomerService;
using SenangRetails.Shared.Entities;
using EBI.UC;
using EBI.DM;

namespace SenangRetails.Shared.Pages
{
    public partial class CompleteSales
    {
        [Inject] CashSalesAC _cashSalesAC { get; set; } = default!;
        [Inject] ICustomerService _customerService { get; set; } = default!;
        [Inject] SenangRetails.Shared.Services.ThemeService.IThemeService ThemeService { get; set; } = default!;
        private bool isPrinting = false;
        private bool isProcessingWA = false;
        private string whatsappNumber = "";
        private Doc_CashSales? salesRecord;
        private bool isLoadingRecord = true;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try { await ThemeService.InitializeThemeAsync(); } catch { }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            if (string.IsNullOrEmpty(AppState.LastSaleDocumentId))
            {
                Nav.NavigateTo("/orders", replace: true);
                return;
            }
            await LoadSalesRecordDetails();
        }



        private async Task LoadSalesRecordDetails()
        {
            if (AppState.IsOutstandingPaymentMode)
            {
                isLoadingRecord = false;
                StateHasChanged();
                return;
            }

            try
            {
                isLoadingRecord = true;
                var response = await _cashSalesAC.LoadRecordAsync(AppState.LastSaleDocumentId);

                if (response != null && response.StatusCode == 200 && response.Result != null)
                {
                    salesRecord = response.Result;
                }
                else if (AppState.LastCompletedOrder != null)
                {
                    salesRecord = AppState.LastCompletedOrder;
                }
                else
                {
                    ShowNotification("Failed to load sales details from server.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading record: {ex.Message}");
                if (AppState.LastCompletedOrder != null)
                {
                    salesRecord = AppState.LastCompletedOrder;
                }
            }
            finally
            {
                isLoadingRecord = false;
                StateHasChanged();
            }
        }

        private bool IsValidPhoneNumber => !string.IsNullOrWhiteSpace(whatsappNumber) && whatsappNumber.Length >= 10;

        private async Task PrintReceipt()
        {
            var printer = PrinterSvc.GetSelectedPrinter();
            if (printer == null) { await JS.InvokeVoidAsync("alert", "No printer selected."); return; }
            if (salesRecord == null) { ShowNotification("Sales data not loaded."); return; }
            try
            {
                isPrinting = true;
                StateHasChanged();

                var branch = AppState.CurrentBranch;
                var doc = salesRecord.objDoc_CashSales;
                CustomerDM? fullCustomer = null;
                if (!string.IsNullOrEmpty(doc?.AccountID))
                {
                    fullCustomer = await _customerService.GetSingleCustomer(doc.AccountID);
                }
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
                    Items = salesRecord.lstDocumentLine.Select(i => new ReceiptLineItem
                    {
                        Name = i.Description,
                        Quantity = (int)i.Quantity,
                        Discount = i.Discount,
                        LineTotal = (i.UnitPrice * i.Quantity) - i.Discount,
                        Remarks = i.RefCompanyName,
                        PaymentMethod = i.Description

                    }).ToList(),
                    Payments = salesRecord.lstReceiptLines.Select(p => new ReceiptPaymentLine
                    {
                        Method = p.Description ?? "",
                        Amount = p.POSReceiptLineAmount
                    }).ToList(),
                    ChangeAmount = salesRecord.lstReceiptLines.Any(x => x.POSReceiptChangeAmount < 0)
                        ? Math.Abs(salesRecord.lstReceiptLines.FirstOrDefault(x => x.POSReceiptChangeAmount < 0)?.POSReceiptChangeAmount ?? 0m)
                        : Math.Max(0, AppState.LastPaidAmount - (doc?.TotalAfterTax ?? 0m)),
                    TaxSummary = salesRecord.lstDocumentLine
                        .Where(l => !string.IsNullOrEmpty(l.TaxCodeID))
                        .GroupBy(l => new { l.TaxCodeID, l.TaxPercentage })
                        .Select(g => new TaxSummaryLine
                        {
                            TaxCode = $"{g.Key.TaxCodeID} {g.Key.TaxPercentage * 100:0.#}%",
                            Amount = g.Sum(x => (x.UnitPrice * x.Quantity) - x.Discount),
                            Tax = g.Sum(x => x.TaxAmount)
                        }).ToList(),
                    Subtotal = doc?.TotalBeforeTax ?? 0,
                    TaxAmount = doc?.TaxAmount ?? 0,
                    TotalBeforeTax_ServiceCharge = doc?.TotalBeforeTax_ServiceCharge ?? 0,
                    RoundingAmount = doc?.RoundingAmount ?? 0,
                    GrandTotal = doc?.TotalAfterTax ?? 0,
                    PaymentMethod = AppState.LastPaymentMethod,
                    PaidAmount = AppState.LastPaidAmount
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
                StateHasChanged();
            }
        }
        private async void ShowNotification(string message)
        {
            try { await JS.InvokeVoidAsync("alert", message); }
            catch { Console.WriteLine($"NOTIF: {message}"); }
        }

        private bool isProcessing = false;

        private async Task ProcessWhatsApp()
        {
            if (string.IsNullOrWhiteSpace(AppState.LastSaleDocumentId) || !IsValidPhoneNumber) return;

            isProcessing = true;
            StateHasChanged();

            try
            {
                var (einvoice, bill) = await SalesService.GenerateInvoiceLinksAsync(AppState.LastSaleDocumentId);

                if (string.IsNullOrEmpty(einvoice) && string.IsNullOrEmpty(bill))
                {
                    ShowNotification("Could not generate receipt links. Please try again.");
                    return;
                }

                string companyName = AppState.CurrentBranch?.CompanyName ?? "";
                string message = WhatsApp.BuildMessage(companyName, einvoice, bill);

                await WhatsApp.OpenAsync(whatsappNumber, message);
            }
            catch (Exception ex)
            {
                ShowNotification("WhatsApp Error: " + ex.Message);
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }
        private bool isDownloading = false;

        private async Task DownloadPDFReceipt()
        {
            if (isDownloading) return;

            try
            {
                isDownloading = true;
                StateHasChanged();

                await SalesService.DownloadReceiptPdfAsync(AppState.LastSaleDocumentId);
            }
            catch (Exception ex)
            {
                ShowNotification($"Download error: {ex.Message}");
            }
            finally
            {
                isDownloading = false;
                StateHasChanged();
            }
        }
        private bool isPrintingEInvoice = false;
        private async Task RequestEInvoicePrint()
        {
            if (isPrintingEInvoice) return;
            var printer = PrinterSvc.GetSelectedPrinter();
            if (printer == null) { ShowNotification("No printer selected."); return; }

            try
            {
                isPrintingEInvoice = true;
                StateHasChanged();

                var (einvoice, bill) = await SalesService.GenerateInvoiceLinksAsync(AppState.LastSaleDocumentId);

                var receiptData = await BuildBaseReceiptData();
                receiptData.EInvoiceQrUrl = einvoice;

                // 3. Print
                var (success, error) = await ReceiptPrinterSvc.PrintAsync(printer, receiptData);

                if (!success) ShowNotification($"Print failed: {error}");
                else ShowNotification("e-Invoice Receipt Printed.");
            }
            catch (Exception ex)
            {
                ShowNotification($"Print Error: {ex.Message}");
            }
            finally
            {
                isPrintingEInvoice = false;
                StateHasChanged();
            }
        }

        private async Task<ReceiptData> BuildBaseReceiptData()
        {
            if (salesRecord == null || salesRecord.objDoc_CashSales == null)
            {
                Console.WriteLine("[WARN] BuildBaseReceiptData called but salesRecord is null.");
                return new ReceiptData();
            }
            var doc = salesRecord.objDoc_CashSales;
            CustomerDM? fullCustomer = null;
            if (!string.IsNullOrEmpty(doc?.AccountID))
                fullCustomer = await _customerService.GetSingleCustomer(doc.AccountID);

            var branch = AppState.CurrentBranch;
            return new ReceiptData
            {
                CompanyName = branch?.CompanyName ?? "",
                BranchName = branch?.Branch ?? "",
                Address1 = branch?.Address1 ?? "",
                Address2 = branch?.Address2 ?? "",
                Address3 = branch?.Address3 ?? "",
                Phone = branch?.Phone ?? "",
                Email = branch?.Email ?? "",
                CurrencyName = doc?.LocalCurrencyName ?? "",
                ReceiptNo = doc?.DisplayCode ?? "",
                DateTimeOfSale = doc?.FinancialDate,
                CustomerName = doc?.AccountName ?? "",
                CustomerID = doc?.AccountID ?? "",
                CustomerAddress1 = fullCustomer?.Address1 ?? "",
                CustomerAddress2 = fullCustomer?.Address2 ?? "",
                CustomerPhone = fullCustomer?.Phone ?? "",
                Items = salesRecord.lstDocumentLine.Select(i => new ReceiptLineItem
                {
                    Name = i.Description,
                    Quantity = (int)i.Quantity,
                    Discount = i.Discount,
                    LineTotal = (i.UnitPrice * i.Quantity) - i.Discount,
                    Remarks = i.RefCompanyName
                }).ToList(),
                Payments = salesRecord.lstReceiptLines.Select(p => new ReceiptPaymentLine
                {
                    Method = p.Description ?? "",
                    Amount = p.POSReceiptLineAmount
                }).ToList(),
                ChangeAmount = salesRecord.lstReceiptLines.Any(x => x.POSReceiptChangeAmount < 0)
                    ? Math.Abs(salesRecord.lstReceiptLines.FirstOrDefault(x => x.POSReceiptChangeAmount < 0)?.POSReceiptChangeAmount ?? 0m)
                    : Math.Max(0, AppState.LastPaidAmount - (doc?.TotalAfterTax ?? 0m)),
                TaxSummary = salesRecord.lstDocumentLine.Where(l => !string.IsNullOrEmpty(l.TaxCodeID)).GroupBy(l => new { l.TaxCodeID, l.TaxPercentage }).Select(g => new TaxSummaryLine
                {
                    TaxCode = $"{g.Key.TaxCodeID} {g.Key.TaxPercentage * 100:0.#}%",
                    Amount = g.Sum(x => (x.UnitPrice * x.Quantity) - x.Discount),
                    Tax = g.Sum(x => x.TaxAmount)
                }).ToList(),
                Subtotal = doc?.TotalBeforeTax ?? 0,
                RoundingAmount = doc?.RoundingAmount ?? 0,
                GrandTotal = doc?.TotalAfterTax ?? 0,
                CashierName = doc?.CashierName ?? "",
                PaidAmount = AppState.LastPaidAmount
            };
        }

        private void Finish()
        {
            AppState.IsOutstandingPaymentMode = false;
            AppState.OutstandingPaymentAmount = 0m;
            AppState.ResetPaymentState();
            AppState.CurrentOrder = null;
            AppState.SelectedCustomer = null;
            Nav.NavigateTo("/orders", replace: true);
        }
    }
}
