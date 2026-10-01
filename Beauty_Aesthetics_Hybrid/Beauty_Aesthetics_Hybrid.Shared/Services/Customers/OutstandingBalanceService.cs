using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public interface IOutstandingBalanceService
{
    Task<CustomerOperationResult<MemberOtherBalanceSummaryDTO>> LoadSummaryAsync(
        string customerId,
        DateTime cutOffDate,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>> LoadDocumentsAsync(
        string customerId,
        string branchGroupId,
        CancellationToken cancellationToken = default);
}

public sealed class OutstandingBalanceService : IOutstandingBalanceService
{
    private readonly ICustomerService customerService;
    private readonly ARReceiptAC arReceiptAC;

    public OutstandingBalanceService(
        ICustomerService customerService,
        ARReceiptAC arReceiptAC)
    {
        this.customerService = customerService;
        this.arReceiptAC = arReceiptAC;
    }

    public async Task<CustomerOperationResult<MemberOtherBalanceSummaryDTO>> LoadSummaryAsync(
        string customerId,
        DateTime cutOffDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerId = customerId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            return CustomerOperationResult<MemberOtherBalanceSummaryDTO>.Fail(
                "Customer ID is required.");
        }

        // Outstanding Step 1 is summary/display only.
        // Do not treat this as invoice settlement or a POS payment.
        var result = await customerService.GetOtherBalanceSummaryAsync(
            normalizedCustomerId,
            cutOffDate,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<MemberOtherBalanceSummaryDTO>.Fail(
                result.ErrorMessage ?? "Unable to load outstanding balance.");
        }

        Console.WriteLine(
            $"[Outstanding Step 1] Customer={normalizedCustomerId} | " +
            $"CutOffDate={cutOffDate:yyyy-MM-dd} | Outstanding={result.Value.Outstanding:N2}");

        return CustomerOperationResult<MemberOtherBalanceSummaryDTO>.Ok(result.Value);
    }

    public async Task<CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>> LoadDocumentsAsync(
        string customerId,
        string branchGroupId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerId = customerId?.Trim() ?? string.Empty;
        var normalizedGroupId = branchGroupId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Fail(
                "Customer ID is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedGroupId))
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Fail(
                "Branch group ID is required to load outstanding documents.");
        }

        var result = await arReceiptAC.RetrieveSettlementLinesAsync(
            normalizedCustomerId,
            normalizedGroupId,
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Fail(
                result.ErrorMessage ?? "Unable to load outstanding documents.");
        }

        // Senang settles only rows where Outstanding > 0 and allocates oldest
        // FinancialDate first. Step 2 keeps that same ordering but remains read-only.
        var documents = result.Value
            .Where(document => document.RemainingAmount > 0m)
            .OrderBy(document =>
                document.FinancialDate.Year <= 1900
                    ? DateTime.MaxValue
                    : document.FinancialDate)
            .ThenBy(document => document.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine(
            $"[Outstanding Step 2] Customer={normalizedCustomerId} | " +
            $"Group={normalizedGroupId} | Documents={documents.Count} | " +
            $"TotalOutstanding={documents.Sum(document => document.RemainingAmount):N2}");

        foreach (var document in documents)
        {
            Console.WriteLine(
                $"[Outstanding Step 2] DOCUMENT | Source={document.SourceId} | " +
                $"No={document.DocumentNumber} | Date={document.FinancialDate:yyyy-MM-dd} | " +
                $"Original={document.EffectiveOriginalAmount:N2} | " +
                $"Paid={document.EffectivePaidAmount:N2} | " +
                $"Remaining={document.RemainingAmount:N2} | " +
                $"Due={(document.DueDate.Year <= 1900 ? "—" : document.DueDate.ToString("yyyy-MM-dd"))} | " +
                $"Branch={document.BranchID}");
        }

        return CustomerOperationResult<IReadOnlyList<OutstandingDocumentDTO>>.Ok(documents);
    }
}
