using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.PointConversions;

public interface IPointConversionService
{
    Task<ApiCallResult<IReadOnlyList<PointConversionDM>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<PointConversionDM>> LoadRecordAsync(
        string pointId,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> CreateAsync(
        PointConversionDM pointConversion,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> UpdateAsync(
        PointConversionDM pointConversion,
        CancellationToken cancellationToken = default);

    Task<ApiCallResult<bool>> DeleteAsync(
        string pointId,
        CancellationToken cancellationToken = default);
}
