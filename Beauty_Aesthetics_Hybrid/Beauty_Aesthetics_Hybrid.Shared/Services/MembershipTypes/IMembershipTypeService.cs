using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.MembershipTypes;

/// <summary>
/// Business-level operations for membership types.
/// </summary>
public interface IMembershipTypeService
{
    /// <summary>Retrieve every membership type.</summary>
    Task<ApiCallResult<IReadOnlyList<MembershipTypeDM>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Load a single membership type by its ID (or a blank template when <paramref name="id"/> is empty).</summary>
    Task<ApiCallResult<MembershipTypeDM>> LoadRecordAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>Create a new membership type.</summary>
    Task<ApiCallResult<bool>> CreateAsync(
        MembershipTypeDM membershipType,
        List<MembershipTypeDiscountDM>? discounts = null,
        CancellationToken cancellationToken = default);

    /// <summary>Update an existing membership type.</summary>
    Task<ApiCallResult<bool>> UpdateAsync(
        MembershipTypeDM membershipType,
        List<MembershipTypeDiscountDM>? discounts = null,
        CancellationToken cancellationToken = default);

    /// <summary>Delete a membership type by ID.</summary>
    Task<ApiCallResult<bool>> DeleteAsync(
        string memberTypeId,
        CancellationToken cancellationToken = default);
}
