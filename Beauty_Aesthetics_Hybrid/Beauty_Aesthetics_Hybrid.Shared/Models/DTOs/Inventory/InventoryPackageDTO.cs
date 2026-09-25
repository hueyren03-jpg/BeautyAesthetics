using System.Text.Json;
using System.Text.Json.Serialization;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class InventoryPackageRequestDTO
{
    [JsonPropertyName("objInventory")]
    public InventoryDM ObjInventory { get; set; } = new();

    [JsonIgnore]
    public List<InventoryMembershipCreditDTO> MembershipCredits { get; set; } = new();

    [JsonPropertyName("lstMasterAccount_Branch")]
    public List<InventoryBranchDTO> Branches { get; set; } = new();

    [JsonPropertyName("lstVendor_InventorySupplies")]
    public List<object> VendorSupplies { get; set; } = new();

    [JsonPropertyName("lstMasterAccount_Location")]
    public List<object> Locations { get; set; } = new();

    [JsonPropertyName("lstInventory_CommissionByGroup")]
    public List<object> CommissionGroups { get; set; } = new();
}

public sealed class InventoryPackageLoadDTO
{
    [JsonPropertyName("objInventory")]
    public InventoryPackageLoadRecordDTO? ObjInventory { get; set; }

    [JsonPropertyName("lstPackage")]
    public List<InventoryPackageLineDTO>? PackageLines { get; set; }

    [JsonPropertyName("lstMasterAccount_Branch")]
    public List<InventoryPackageBranchLoadDTO>? Branches { get; set; }

    [JsonPropertyName("lstMembershipCredit")]
    public List<InventoryMembershipCreditDTO>? MembershipCredits { get; set; }
}

public sealed class InventoryPackageLoadRecordDTO
{
    [JsonPropertyName("masterAccountID")]
    public string? MasterAccountId { get; set; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    [JsonPropertyName("salesDescription")]
    public string? SalesDescription { get; set; }

    [JsonPropertyName("displayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("vendorItemCode")]
    public string? VendorItemCode { get; set; }

    [JsonPropertyName("salesPrice")]
    public decimal SalesPrice { get; set; }

    [JsonPropertyName("purchasePrice")]
    public decimal PurchasePrice { get; set; }

    [JsonPropertyName("taxCodeID")]
    public string? TaxCodeId { get; set; }

    [JsonPropertyName("isTaxInclusive")]
    public bool IsTaxInclusive { get; set; }

    [JsonPropertyName("accountStatus")]
    public string? AccountStatus { get; set; }

    [JsonPropertyName("branchID")]
    public string? BranchId { get; set; }

    [JsonPropertyName("itemGroupName")]
    public string? ItemGroupName { get; set; }

    [JsonPropertyName("unitOfMeasureID")]
    public string? UnitOfMeasureId { get; set; }

    [JsonPropertyName("validityDays")]
    public int ValidityDays { get; set; }

    [JsonPropertyName("memberExpiryDays")]
    public int MemberExpiryDays { get; set; }

    [JsonPropertyName("triggeredMemberTypeID")]
    public string? TriggeredMemberTypeId { get; set; }

    [JsonPropertyName("memberMainAccountCredit")]
    public decimal MemberMainAccountCredit { get; set; }

    [JsonPropertyName("lstMembershipCredit")]
    public List<InventoryMembershipCreditDTO>? MembershipCredits { get; set; }

    [JsonPropertyName("lstPackage")]
    public List<InventoryPackageLineDTO>? PackageLines { get; set; }
}

public sealed class InventoryMembershipCreditDTO
{
    [JsonPropertyName("memberTypeID")]
    public string MemberTypeId { get; set; } = string.Empty;

    [JsonPropertyName("memberCredit")]
    public decimal MemberCredit { get; set; }

    [JsonPropertyName("saveAction")]
    public string SaveAction { get; set; } = "Added";

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; } = true;
}

public sealed class InventoryPackageLineDTO
{
    [JsonPropertyName("autoID")]
    public JsonElement AutoId { get; set; }

    [JsonPropertyName("packageID")]
    public string? PackageId { get; set; }

    [JsonPropertyName("inventoryID")]
    public string? InventoryId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; set; }

    [JsonPropertyName("unitActualValue")]
    public decimal UnitActualValue { get; set; }

    [JsonPropertyName("totalActualValue")]
    public decimal TotalActualValue { get; set; }

    [JsonPropertyName("inventoryTypeID")]
    public int InventoryTypeId { get; set; }

    [JsonPropertyName("isDeferred")]
    public bool IsDeferred { get; set; }

    [JsonPropertyName("isVoided")]
    public bool IsVoided { get; set; }

    [JsonPropertyName("isConfirmed")]
    public bool IsConfirmed { get; set; }

    [JsonPropertyName("packageQuantityTypeID")]
    public int PackageQuantityTypeId { get; set; }
}

public sealed class InventoryPackageBranchLoadDTO
{
    [JsonPropertyName("MasterAccountID")]
    public string? MasterAccountId { get; set; }

    [JsonPropertyName("BranchID")]
    public string? BranchId { get; set; }

    [JsonPropertyName("BranchPrice")]
    public decimal BranchPrice { get; set; }

    [JsonPropertyName("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("GroupID")]
    public string? GroupId { get; set; }
}

public sealed class InventoryBranchDTO
{
    [JsonPropertyName("MasterAccountID")]
    public string? MasterAccountId { get; set; }

    [JsonPropertyName("BranchID")]
    public string BranchId { get; set; } = string.Empty;

    [JsonPropertyName("BranchPrice")]
    public decimal BranchPrice { get; set; }

    [JsonPropertyName("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("GroupID")]
    public string? GroupId { get; set; }

    [JsonPropertyName("SaveAction")]
    public string SaveAction { get; set; } = "Added";

    [JsonPropertyName("IsDirty")]
    public bool IsDirty { get; set; } = true;
}

public sealed class InventorySaveResultDTO
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("DisplayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("SuccessMessage")]
    public string? SuccessMessage { get; set; }
}
