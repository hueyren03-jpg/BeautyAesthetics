using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public interface ICashSalesService
{
    Task<ApiCallResult<IReadOnlyList<Transaction>>> LoadTransactionsAsync(DateTime startDate, DateTime endDate, string branchId = "", bool resolvePaymentMethods = true, CancellationToken cancellationToken = default);
    Task<ApiCallResult<SalesByTypeDTO>> LoadSalesByTypeAsync(DateTime startDate, DateTime endDate, string branchId, CancellationToken cancellationToken = default);
    Task<ApiCallResult<IReadOnlyList<SalesByCollectionDTO>>> LoadSalesByCollectionAsync(DateTime startDate, DateTime endDate, string branchId, CancellationToken cancellationToken = default);
    Task<ApiCallResult<Transaction>> LoadTransactionAsync(string documentId, CancellationToken cancellationToken = default);
    Task<ApiCallResult<IReadOnlyList<CashSalesPaymentTypeDTO>>> LoadPaymentTypesAsync(CancellationToken cancellationToken = default);
    Task<ApiCallResult<Transaction>> CreateTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<ApiCallResult<Transaction>> UpdateTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<ApiCallResult<string>> DeleteTransactionAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default);
}