using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.EInvoiceService;

namespace SenangRetails.Shared.Pages
{
    public partial class EInvoices : BasePage
    {
        [Inject] private AppState AppState { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private IEInvoiceService EInvoiceService { get; set; } = default!;
        [Inject] private IReportService ReportService { get; set; } = default!;
        [Inject] private ICashSalesService CashSalesService { get; set; } = default!;

        private DateTime? SummaryDateFrom { get; set; }
        private DateTime? SummaryDateTo { get; set; }

        private DateTime? SubmissionDateFrom { get; set; }
        private DateTime? SubmissionDateTo { get; set; }

        private string MaxDateString => DateTime.Today.ToString("yyyy-MM-dd");

        private bool IsLoading { get; set; }
        private bool IsSubmitting { get; set; }

        // Summary Data
        private EInvoiceSubmissionSummary SummaryData { get; set; } = new();

        public int PendingBill => SummaryData?.PendingBill ?? 0;
        public decimal PendingAmount => SummaryData?.PendingAmount ?? 0;
        public int ValidIndividualBill => SummaryData?.ValidIndividualBill ?? 0;
        public decimal ValidIndividualAmount => SummaryData?.ValidIndividualAmount ?? 0;
        public int ValidConsoBill => SummaryData?.ValidConsoBill ?? 0;
        public decimal ValidConsoAmount => SummaryData?.ValidConsoAmount ?? 0;
        public int InvalidBill => SummaryData?.InvalidBill ?? 0;
        public decimal InvalidAmount => SummaryData?.InvalidAmount ?? 0;
        public int CancelledBill => SummaryData?.CancelledBill ?? 0;
        public decimal CancelledAmount => SummaryData?.CancelledAmount ?? 0;
        private bool CanSubmitConsolidated =>
            SubmissionDateFrom.HasValue &&
            SubmissionDateTo.HasValue &&
            SubmissionDateFrom.Value.Date <= SubmissionDateTo.Value.Date &&
            !IsSubmitting;

        private bool ShowModal { get; set; }
        private bool ShowConfirmModal { get; set; }
        private string ModalType { get; set; } = "";
        private string ModalTitle { get; set; } = "";
        private string ModalMessage { get; set; } = "";
        private EInvoiceSubmitPayload? SubmissionResult { get; set; }

        private string ConfirmFromDate { get; set; } = "";
        private string ConfirmToDate { get; set; } = "";
        private bool SubmissionSuccess { get; set; }
        private bool ShowLoadingModal { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var today = DateTime.Today;

            SummaryDateFrom = today;
            SummaryDateTo = today;
            SubmissionDateFrom = today;
            SubmissionDateTo = today;

            await LoadSummaryData();
        }

        private DateTime GetStartOfDay(DateTime date)
        {
            return date.Date; // Returns 00:00:00
        }

        private DateTime GetEndOfDay(DateTime date)
        {
            return date.Date.AddDays(1).AddMinutes(-1); // Returns 23:59:00
        }

        private async Task LoadSummaryData()
        {
            if (!SummaryDateFrom.HasValue || !SummaryDateTo.HasValue) return;

            IsLoading = true;
            StateHasChanged();

            var startDate = GetStartOfDay(SummaryDateFrom.Value);
            var endDate = GetEndOfDay(SummaryDateTo.Value);

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID)
                    ? "HQ"
                    : AppState.SelectedBranchID;

                var result = await EInvoiceService.GetSubmissionSummaryAsync(
                    startDate,
                    endDate,
                    branchId);

                SummaryData = result?.Result ?? new EInvoiceSubmissionSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading E-Invoice summary: {ex.Message}");
                SummaryData = new EInvoiceSubmissionSummary();
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        private async Task SubmitConsolidated()
        {
            if (!CanSubmitConsolidated) return;

            ConfirmFromDate = SubmissionDateFrom?.ToString("dd/MM/yyyy") ?? "";
            ConfirmToDate = SubmissionDateTo?.ToString("dd/MM/yyyy") ?? "";

            ShowConfirmModal = true;
            StateHasChanged();
        }

        private async Task ConfirmAndSubmit()
        {
            ShowConfirmModal = false;
            await ExecuteSubmission();
        }

        private async Task ExecuteSubmission()
        {
            ShowConfirmModal = false;

            ShowLoadingModal = true;
            StateHasChanged();

            var startDate = GetStartOfDay(SubmissionDateFrom.Value);
            var endDate = GetEndOfDay(SubmissionDateTo.Value);

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID)
                    ? "HQ"
                    : AppState.SelectedBranchID;

                var response = await EInvoiceService.SubmitConsolidatedAsync(
                    startDate,
                    endDate,
                    branchId);

                ShowLoadingModal = false;
                StateHasChanged();

                if (response == null)
                {
                    ModalType = "error";
                    ModalTitle = "Submission Failed";
                    ModalMessage = "No response from server";
                }
                else if (response.IsError && response.Title?.Contains("No records", StringComparison.OrdinalIgnoreCase) == true)
                {
                    ModalType = "no-records";
                }
                else if (response.IsError)
                {
                    ModalType = "error";
                    ModalTitle = response.Title ?? "Submission Failed";
                    ModalMessage = response.Detail ?? response.Message ?? "Unknown error occurred";
                }
                else if (response.Result != null && !string.IsNullOrEmpty(response.Result.UUID))
                {
                    SubmissionResult = response.Result;
                    ModalType = "success";
                    SubmissionSuccess = true;
                }
                else
                {
                    ModalType = "error";
                    ModalTitle = "Submission Failed";
                    ModalMessage = response.Message ?? "Unknown error occurred";
                }

                ShowModal = true;
                StateHasChanged();

                if (ModalType == "success")
                {
                    await LoadSummaryData();
                }
            }
            catch (Exception ex)
            {
                ShowLoadingModal = false;
                StateHasChanged();

                ModalType = "error";
                ModalTitle = "Exception";
                ModalMessage = ex.Message;
                ShowModal = true;
                Console.WriteLine($"Submission Error: {ex}");
            }
            finally
            {
                IsSubmitting = false;
                StateHasChanged();
            }
        }

        private void CloseModal()
        {
            ShowModal = false;
            ModalType = "";
            ModalTitle = "";
            ModalMessage = "";
            SubmissionResult = null;
        }

        private void CloseConfirmModal()
        {
            ShowConfirmModal = false;
        }

        private bool ShowValidIndModal { get; set; } = false;
        private List<EInvoiceDetail> ValidIndInvoices { get; set; } = new();
        private bool IsLoadingValidInd { get; set; } = false;

        public class EInvoiceDetail
        {
            public string DocumentID { get; set; } = string.Empty;
            public string DisplayCode { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public DateTime FinancialDate { get; set; }
            public decimal TotalAfterTax { get; set; }
            public int eInvoiceTypeID { get; set; }
            public string eInvoiceStatus { get; set; } = string.Empty;
        }

        private string SearchTerm { get; set; } = "";

        private List<EInvoiceDetail> FilteredInvoices =>
            string.IsNullOrWhiteSpace(SearchTerm)
                ? ValidIndInvoices
                : ValidIndInvoices.Where(i =>
                    i.DisplayCode.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    i.AccountName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                  ).ToList();

        private async Task OpenValidIndModal()
        {
            ShowValidIndModal = true;
            StateHasChanged();

            await LoadValidIndInvoices();
        }

        private async Task LoadValidIndInvoices()
        {
            if (IsLoadingValidInd) return;

            IsLoadingValidInd = true;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID)
                    ? "HQ"
                    : AppState.SelectedBranchID;

                var startDate = SummaryDateFrom ?? DateTime.Today;
                var endDate = SummaryDateTo ?? DateTime.Today;

                var startDateTime = GetStartOfDay(startDate);
                var endDateTime = GetEndOfDay(endDate);

                var response = await ReportService.GetAppSalesListAsync(
                    branchId,
                    startDateTime,
                    endDateTime
                );

                if (response != null && !response.IsError && response.Result != null)
                {
                    // Filter only Valid Individual invoices
                    // Assuming Valid (Ind) means eInvoiceTypeID = 1 and eInvoiceStatus = "Valid" or similar
                    ValidIndInvoices = response.Result
                        .Where(inv => inv.eInvoiceTypeID == 1 &&
                                     inv.eInvoiceStatus?.Equals("Valid", StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();
                }
                else
                {
                    ValidIndInvoices = new List<EInvoiceDetail>();

                    if (response?.IsError == true)
                    {
                        Console.WriteLine($"Error loading invoices: {response.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Valid Individual invoices: {ex.Message}");
                ValidIndInvoices = new List<EInvoiceDetail>();
            }
            finally
            {
                IsLoadingValidInd = false;
                StateHasChanged();
            }
        }

        private void CloseValidIndModal()
        {
            ShowValidIndModal = false;
            SearchTerm = "";
        }

        private bool ShowValidConsoModal { get; set; } = false;
        private List<EInvoiceDetail> ValidConsoInvoices { get; set; } = new();
        private bool IsLoadingValidConso { get; set; } = false;
        private string ConsoSearchTerm { get; set; } = "";

        private List<EInvoiceDetail> FilteredConsoInvoices =>
            string.IsNullOrWhiteSpace(ConsoSearchTerm)
                ? ValidConsoInvoices
                : ValidConsoInvoices.Where(i =>
                    i.DisplayCode.Contains(ConsoSearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    i.AccountName.Contains(ConsoSearchTerm, StringComparison.OrdinalIgnoreCase)
                  ).ToList();

        private async Task OpenValidConsoModal()
        {
            ShowValidConsoModal = true;
            StateHasChanged();
            await LoadValidConsoInvoices();
        }

        private async Task LoadValidConsoInvoices()
        {
            if (IsLoadingValidConso) return;

            IsLoadingValidConso = true;
            StateHasChanged();

            try
            {
                var branchId = string.IsNullOrEmpty(AppState.SelectedBranchID)
                    ? "HQ"
                    : AppState.SelectedBranchID;

                var startDate = SummaryDateFrom ?? DateTime.Today;
                var endDate = SummaryDateTo ?? DateTime.Today;

                var startDateTime = GetStartOfDay(startDate);
                var endDateTime = GetEndOfDay(endDate);

                var response = await ReportService.GetAppSalesListAsync(
                    branchId,
                    startDateTime,
                    endDateTime
                );

                if (response != null && !response.IsError && response.Result != null)
                {
                    // Filter for Valid Consolidated invoices
                    // eInvoiceTypeID == 2 (Consolidated) and eInvoiceStatus == "Valid"
                    ValidConsoInvoices = response.Result
                        .Where(inv => inv.eInvoiceTypeID == 2 &&
                                     inv.eInvoiceStatus?.Equals("Valid", StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();
                }
                else
                {
                    ValidConsoInvoices = new List<EInvoiceDetail>();

                    if (response?.IsError == true)
                    {
                        Console.WriteLine($"Error loading consolidated invoices: {response.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Valid Consolidated invoices: {ex.Message}");
                ValidConsoInvoices = new List<EInvoiceDetail>();
            }
            finally
            {
                IsLoadingValidConso = false;
                StateHasChanged();
            }
        }

        private void CloseValidConsoModal()
        {
            ShowValidConsoModal = false;
            ConsoSearchTerm = "";
        }

        private bool IsDownloading { get; set; } = false;
        private string DownloadingId { get; set; } = string.Empty;

        private async Task DownloadInvoice(EInvoiceDetail invoice)
        {
            if (IsDownloading) return;

            try
            {
                IsDownloading = true;
                DownloadingId = invoice.DocumentID;
                StateHasChanged();

                var success = await CashSalesService.DownloadReceiptPdfAsync(invoice.DocumentID);

                if (!success)
                {
                    ModalType = "error";
                    ModalTitle = "Download Failed";
                    ModalMessage = "Unable to download the invoice. Please try again later.";
                    ShowModal = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading invoice: {ex.Message}");
                ModalType = "error";
                ModalTitle = "Download Error";
                ModalMessage = ex.Message;
                ShowModal = true;
            }
            finally
            {
                IsDownloading = false;
                DownloadingId = string.Empty;
                StateHasChanged();
            }
        }

        private void GoBack()
        {
            var returnUrl = Navigation.Uri.Contains("returnTo=admin") ? "/admin" : "/home";
            Navigation.NavigateTo(returnUrl);
        }
    }
}