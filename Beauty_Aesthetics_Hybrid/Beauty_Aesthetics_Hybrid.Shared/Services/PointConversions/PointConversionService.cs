using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.PointConversions;

public sealed class PointConversionService : IPointConversionService
{
    private readonly PointConversionAC pointConversionAC;

    public PointConversionService(PointConversionAC pointConversionAC)
    {
        this.pointConversionAC = pointConversionAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<PointConversionDM>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await pointConversionAC.LoadProxyAsync(cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<PointConversionDM>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load member point rules.");
        }

        return ApiCallResult<IReadOnlyList<PointConversionDM>>.Ok(
            result.StatusCode,
            result.Value.OrderBy(item => item.MemberTypeID).ToList());
    }

    public Task<ApiCallResult<PointConversionDM>> LoadRecordAsync(
        string pointId,
        CancellationToken cancellationToken = default) =>
        pointConversionAC.LoadRecordAsync(pointId, cancellationToken);

    public Task<ApiCallResult<bool>> CreateAsync(
        PointConversionDM pointConversion,
        CancellationToken cancellationToken = default)
    {
        PrepareForSave(pointConversion, "Added");
        return pointConversionAC.CreateRecordAsync(pointConversion, cancellationToken);
    }

    public Task<ApiCallResult<bool>> UpdateAsync(
        PointConversionDM pointConversion,
        CancellationToken cancellationToken = default)
    {
        PrepareForSave(pointConversion, "Changed");
        return pointConversionAC.UpdateRecordAsync(pointConversion, cancellationToken);
    }

    public Task<ApiCallResult<bool>> DeleteAsync(
        string pointId,
        CancellationToken cancellationToken = default) =>
        pointConversionAC.DeleteAsync(pointId, cancellationToken);

    private static void PrepareForSave(PointConversionDM pointConversion, string saveAction)
    {
        pointConversion.DaysCovered = Math.Max(
            0,
            (int)(pointConversion.ToDate.Date - pointConversion.FromDate.Date).TotalDays);
        pointConversion.SaveAction = saveAction;
        pointConversion.IsDirty = true;
    }
}
