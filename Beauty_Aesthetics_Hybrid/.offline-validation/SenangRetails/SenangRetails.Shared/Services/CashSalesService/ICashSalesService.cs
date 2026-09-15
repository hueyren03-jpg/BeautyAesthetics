using EBI.UC;
using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Text;
using static SenangRetails.Shared.Pages.Orders;

namespace SenangRetails.Shared.Services.CashSalesService
{
    public interface ICashSalesService
    {
        Task<(bool Success, string Message, string? DisplayCode, string? DocumentId)> CompletePaymentAsync(Doc_CashSales order, List<PaymentLine> payments);
        Task<List<Doc_CashSalesDM>> GetSalesHistoryAsync(string branchId, DateTime date);
        Task<(bool Success, string Message)> DeleteSaleAsync(string documentId);
        Task<(string EInvoiceUrl, string BillUrl)> GenerateInvoiceLinksAsync(string documentId);
        Task<bool> DownloadReceiptPdfAsync(string documentId);
        Task<(bool Success, string Message, string? DisplayCode, string? DocumentId)> RedeemPackageAsync(Order order);
    }
}
