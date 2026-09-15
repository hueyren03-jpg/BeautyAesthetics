using System.Text.Json.Serialization;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

/// <summary>
/// Mirrors the MembershipType record returned by the API.
/// Property names use PascalCase to match the server JSON shape
/// (deserialized with <c>PropertyNameCaseInsensitive = true</c>).
/// </summary>
public sealed class MembershipTypeDM
{
    public bool IsLoading { get; set; }
    public string? MemberTypeID { get; set; }
    public string? MemberTypeName { get; set; }
    public int MembershipValidDays { get; set; }
    public decimal MemberDiscountOnInventory { get; set; }
    public decimal MemberDiscountOnServices { get; set; }
    public decimal MemberDiscountOnPackage { get; set; }
    public decimal MemberDiscountOnBundle { get; set; }
    public string? ApplicableToItems { get; set; }
    public string? ApplicableToItemGroup { get; set; }
    public string? ApplicableToItemBrand { get; set; }
    public bool Active { get; set; } = true;
    public bool IsDiscountLimitToCreditRedemption { get; set; }
    public string DiscountTimeFrom { get; set; } = "00:00:00";
    public string DiscountTimeTo { get; set; } = "23:59:59";
    public EntityState SaveAction { get; set; } = (EntityState)(-1);
    public bool IsDirty { get; set; }
    public List<string> lstBrand { get; set; } = new();
    public List<string> lstGroup { get; set; } = new();
    public List<string> lstItem { get; set; } = new();
}

/// <summary>
/// Child discount row used in Create/Update payloads.
/// </summary>
public sealed class MembershipTypeDiscountDM
{
    public bool IsLoading { get; set; }
    public string? AutoID { get; set; }
    public string? MemberTypeID { get; set; }
    public decimal DiscountPercentage { get; set; }
    public string? DiscountItems { get; set; }
    public string? DiscountGroups { get; set; }
    public string? DiscountBrands { get; set; }
    public EntityState SaveAction { get; set; } = EntityState.Changed;
    public bool IsDirty { get; set; }
}

/// <summary>
/// Wraps the membership type and its discount list for Create/Update API payloads.
/// </summary>
public sealed class MembershipTypeSaveDTO
{
    [JsonPropertyName("objMembershipType")]
    public MembershipTypeDM ObjMembershipType { get; set; } = new();

    [JsonPropertyName("lstMembershipTypeDiscount")]
    public List<MembershipTypeDiscountDM> LstMembershipTypeDiscount { get; set; } = new();
}
