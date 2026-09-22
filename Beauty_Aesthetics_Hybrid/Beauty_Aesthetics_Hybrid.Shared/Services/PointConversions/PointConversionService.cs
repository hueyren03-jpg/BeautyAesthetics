using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.PointConversions;

public sealed class PointConversionService : IPointConversionService
{
    private readonly PointConversionAC pointConversionAC;
    private readonly AppFeedbackService feedback;

    public PointConversionService(PointConversionAC pointConversionAC, AppFeedbackService feedback)
    {
        this.pointConversionAC = pointConversionAC;
        this.feedback = feedback;
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

    public async Task<ApiCallResult<bool>> CreateAsync(
        PointConversionDM pointConversion,
        CancellationToken cancellationToken = default)
    {
        PrepareForSave(pointConversion, "Added");
        var result = await pointConversionAC.CreateRecordAsync(pointConversion, cancellationToken);
        if (result.Success)
            feedback.Success("Point conversion rule created successfully.", "Point rule created");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to create point conversion rule.", "Point rule not created");
        return result;
    }

    public async Task<ApiCallResult<bool>> UpdateAsync(
        PointConversionDM pointConversion,
        CancellationToken cancellationToken = default)
    {
        PrepareForSave(pointConversion, "Changed");
        var result = await pointConversionAC.UpdateRecordAsync(pointConversion, cancellationToken);
        if (result.Success)
            feedback.Success("Point conversion rule updated successfully.", "Point rule updated");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to update point conversion rule.", "Point rule not updated");
        return result;
    }

    public async Task<ApiCallResult<bool>> DeleteAsync(
        string pointId,
        CancellationToken cancellationToken = default)
    {
        var result = await pointConversionAC.DeleteAsync(pointId, cancellationToken);
        if (result.Success)
            feedback.Success("Point conversion rule deleted successfully.", "Point rule deleted");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to delete point conversion rule.", "Point rule not deleted");
        return result;
    }

    private static void PrepareForSave(PointConversionDM pointConversion, string saveAction)
    {
        pointConversion.DaysCovered = Math.Max(
            0,
            (int)(pointConversion.ToDate.Date - pointConversion.FromDate.Date).TotalDays);
        pointConversion.SaveAction = saveAction;
        pointConversion.IsDirty = true;
    }
}
