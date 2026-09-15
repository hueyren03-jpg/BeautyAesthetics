using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services.MembershipTypes;

/// <summary>
/// Orchestrates membership-type CRUD via <see cref="MembershipTypeAC"/>.
/// </summary>
public sealed class MembershipTypeService : IMembershipTypeService
{
    private readonly MembershipTypeAC membershipTypeAC;

    public MembershipTypeService(MembershipTypeAC membershipTypeAC)
    {
        this.membershipTypeAC = membershipTypeAC;
    }

    // ── Queries ────────────────────────────────────────────

    public async Task<ApiCallResult<IReadOnlyList<MembershipTypeDM>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await membershipTypeAC.GetAllMembershipTypesAsync(cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<MembershipTypeDM>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load membership types.");
        }

        IReadOnlyList<MembershipTypeDM> list = result.Value
            .OrderBy(m => m.MemberTypeName ?? string.Empty)
            .ToList();

        return ApiCallResult<IReadOnlyList<MembershipTypeDM>>.Ok(result.StatusCode, list);
    }

    public async Task<ApiCallResult<MembershipTypeDM>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await membershipTypeAC.LoadRecordAsync(id, cancellationToken);
    }

    // ── Mutations ──────────────────────────────────────────

    public async Task<ApiCallResult<bool>> CreateAsync(
        MembershipTypeDM membershipType,
        List<MembershipTypeDiscountDM>? discounts = null,
        CancellationToken cancellationToken = default)
    {
        membershipType.SaveAction = EntityState.Added;
        membershipType.IsDirty = true;
        foreach (var discount in discounts ?? [])
        {
            discount.SaveAction = EntityState.Added;
            discount.IsDirty = true;
        }

        var payload = new MembershipTypeSaveDTO
        {
            ObjMembershipType = membershipType,
            LstMembershipTypeDiscount = discounts ?? new()
        };

        var result = await membershipTypeAC.CreateRecordAsync(payload, cancellationToken);
        return ToSaveResult(result, "Unable to create membership type.");
    }

    public async Task<ApiCallResult<bool>> UpdateAsync(
        MembershipTypeDM membershipType,
        List<MembershipTypeDiscountDM>? discounts = null,
        CancellationToken cancellationToken = default)
    {
        membershipType.SaveAction = EntityState.Changed;
        membershipType.IsDirty = true;
        foreach (var discount in discounts ?? [])
        {
            discount.SaveAction = string.IsNullOrWhiteSpace(discount.AutoID)
                ? EntityState.Added
                : EntityState.Changed;
            discount.IsDirty = true;
        }

        var payload = new MembershipTypeSaveDTO
        {
            ObjMembershipType = membershipType,
            LstMembershipTypeDiscount = discounts ?? new()
        };

        var result = await membershipTypeAC.UpdateRecordAsync(payload, cancellationToken);
        return ToSaveResult(result, "Unable to update membership type.");
    }

    public async Task<ApiCallResult<bool>> DeleteAsync(
        string memberTypeId,
        CancellationToken cancellationToken = default)
    {
        var result = await membershipTypeAC.DeleteRecordAsync(memberTypeId, cancellationToken);
        return ToSaveResult(result, "Unable to delete membership type.");
    }

    // ── Helpers ────────────────────────────────────────────

    private static ApiCallResult<bool> ToSaveResult(
        ApiCallResult<JsonElement> result,
        string fallbackMessage)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallbackMessage);
    }
}
