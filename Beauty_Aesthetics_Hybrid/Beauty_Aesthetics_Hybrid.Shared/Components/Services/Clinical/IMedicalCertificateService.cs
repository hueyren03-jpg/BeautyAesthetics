using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

public interface IMedicalCertificateService
{
    Task<ApiCallResult<IReadOnlyList<MedicalCertificateRecordDTO>>> LoadByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<MedicalCertificateRecordDTO>> SaveAsync(
        MedicalCertificateRecordDTO record,
        string branchId,
        string groupId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteAsync(
        string recordId,
        CancellationToken cancellationToken = default);
}
