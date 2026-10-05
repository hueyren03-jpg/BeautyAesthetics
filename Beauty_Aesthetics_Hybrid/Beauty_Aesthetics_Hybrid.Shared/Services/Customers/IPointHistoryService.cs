using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public interface IPointHistoryService
{
    Task<CustomerOperationResult<PointHistorySnapshotDTO>> LoadHistoryAsync(
        string customerId,
        string branchId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}
