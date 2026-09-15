using SenangRetails.Shared.Models;

namespace SenangRetails.Shared.Services.EInvoiceService
{
    public interface IEInvoiceService
    {
        Task<ApiResponse<EInvoiceSubmissionSummary>?> GetSubmissionSummaryAsync(DateTime startDate, DateTime endDate, string branchID = "HQ");
        Task<ApiResponse<EInvoiceSubmitPayload>?> SubmitConsolidatedAsync(DateTime startDate, DateTime endDate, string branchID = "HQ");
    }
}