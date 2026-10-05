using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

public interface IFollowUpService
{
    Task<ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>> LoadByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>> LoadByBranchAsync(
        DateTime startDate,
        DateTime endDate,
        string branchId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<FollowUpRecordDTO>> LoadRecordAsync(
        string recordId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<FollowUpRecordDTO>> SaveAsync(
        FollowUpRecordDTO record,
        string branchId,
        string groupId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteAsync(
        string recordId,
        CancellationToken cancellationToken = default);
}
