using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IMemberCreditService
{
    Task<ApiCallResult<IReadOnlyList<MembershipViewModel.MemberCredit>>> LoadMemberCreditsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateMemberCreditAsync(
        MembershipViewModel.MemberCredit memberCredit,
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdateMemberCreditAsync(
        MembershipViewModel.MemberCredit memberCredit,
        string branchId = "hq",
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeactivateMemberCreditAsync(
        string masterAccountId,
        string branchId = "hq",
        CancellationToken cancellationToken = default);
}
