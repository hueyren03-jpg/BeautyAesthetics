using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenangRetails.Shared.Models.DTOs
{
    /// <summary>
    /// Accepts both JSON string ("Added") and JSON integer (-1) for saveAction.
    /// The API sends saveAction as an int enum on LoadRecord responses but expects
    /// a string on Create/Update requests.
    /// </summary>
    /// 
    /// Accepts both JSON string ("50") and JSON number (50) for decimal? fields.
    /// The API returns PointToRedeem as a quoted string even though it is a numeric value.
    /// </summary>
    public class StringOrNumberDecimalConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.Number) return reader.GetDecimal();
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
            }
            return null;
        }
        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteNumberValue(value.Value);
        }
    }

    /// <summary>
    public class SaveActionConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Number ? reader.GetInt32().ToString() : reader.GetString();

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    // ── Inventory ──────────────────────────────────────────────────────────────

    /// <summary>Response item returned by LoadProxy / LoadProxyByItemGroup.</summary>
    public class InventoryItem
    {
        [JsonPropertyName("MasterAccountID")] public string? MasterAccountID { get; set; }
        [JsonPropertyName("AlphaCode")] public string? AlphaCode { get; set; }
        [JsonPropertyName("NumericCode")] public int NumericCode { get; set; }
        [JsonPropertyName("DisplayCode")] public string? DisplayCode { get; set; }
        [JsonPropertyName("AccountName")] public string? AccountName { get; set; }
        [JsonPropertyName("AccountTypeID")] public int AccountTypeID { get; set; }
        [JsonPropertyName("CreatedDateTime")] public DateTime? CreatedDateTime { get; set; }
        [JsonPropertyName("ModifiedDateTime")] public DateTime? ModifiedDateTime { get; set; }
        [JsonPropertyName("InventoryTypeID")] public int InventoryTypeID { get; set; }
        [JsonPropertyName("ItemGroupID")] public string? ItemGroupID { get; set; }
        [JsonPropertyName("ItemGroupName")] public string? ItemGroupName { get; set; }
        [JsonPropertyName("SalesDescription")] public string? SalesDescription { get; set; }
        [JsonPropertyName("ProductCode")] public string? ProductCode { get; set; }
        [JsonPropertyName("ImagePath")] public string? ImagePath { get; set; }
        [JsonPropertyName("ImageFileName")] public string? ImageFileName { get; set; }
        [JsonPropertyName("SalesPrice")] public decimal SalesPrice { get; set; }
        [JsonPropertyName("SalesStandardCost")] public decimal SalesStandardCost { get; set; }
        [JsonPropertyName("IsSold")] public bool IsSold { get; set; }
        [JsonPropertyName("PurchasePrice")] public decimal PurchasePrice { get; set; }
        [JsonPropertyName("PurchaseDescription")] public string? PurchaseDescription { get; set; }
        [JsonPropertyName("VendorItemCode")] public string? VendorItemCode { get; set; }
        [JsonPropertyName("Remarks")] public string? Remarks { get; set; }
        [JsonPropertyName("AccountStatus")] public string? AccountStatus { get; set; }
        [JsonPropertyName("BranchID")] public string? BranchID { get; set; }
        [JsonPropertyName("StockReorderLevel")] public decimal StockReorderLevel { get; set; }
        [JsonPropertyName("StockMaxLevel")] public decimal StockMaxLevel { get; set; }
        [JsonPropertyName("StockPackLevel")] public decimal StockPackLevel { get; set; }
        [JsonPropertyName("UnitOfMeasureID")] public string? UnitOfMeasureID { get; set; }
        [JsonPropertyName("UnitOfMeasureName")] public string? UnitOfMeasureName { get; set; }
        [JsonPropertyName("ItemTaxGroupID")] public string? ItemTaxGroupID { get; set; }
        [JsonPropertyName("ItemTaxGroupName")] public string? ItemTaxGroupName { get; set; }
        [JsonPropertyName("IsTaxInclusive")] public bool IsTaxInclusive { get; set; }
        [JsonPropertyName("TaxCodeID")] public string? TaxCodeID { get; set; }
    }

    /// <summary>Detailed record returned by LoadRecord.</summary>
    public class InventoryItemDetail : InventoryItem
    {
        [JsonPropertyName("StockCurrentLevel")] public decimal StockCurrentLevel { get; set; }
        [JsonPropertyName("BillOfMaterial")] public string? BillOfMaterial { get; set; }
    }

    /// <summary>Request body for CreateSimple and Update (EBI.DM.InventoryDM).</summary>
    public class InventoryCreateModel
    {
        /// <summary>
        /// Tells the server what action to perform.
        /// "Added" = create new record (used by CreateSimple).
        /// "Changed" = update existing record (used by Update).
        /// Must be set — if omitted the server treats it as "NotChanged" and persists nothing.
        /// </summary>
        [JsonPropertyName("saveAction")] public string saveAction { get; set; } = "Added";
        [JsonPropertyName("masterAccountID")] public string? masterAccountID { get; set; } = null;
        [JsonPropertyName("inventoryTypeID")] public int inventoryTypeID { get; set; } = 1;
        [JsonPropertyName("isDirty")] public bool isDirty { get; set; } = true;
        [JsonPropertyName("accountName")] public string accountName { get; set; } = string.Empty;
        [JsonPropertyName("itemGroupID")] public string? itemGroupID { get; set; } = null;
        [JsonPropertyName("itemGroupName")] public string? itemGroupName { get; set; } = null;
        [JsonPropertyName("salesDescription")] public string? salesDescription { get; set; } = null;
        [JsonPropertyName("displayCode")] public string? displayCode { get; set; } = null;
        [JsonPropertyName("productCode")] public string? productCode { get; set; } = null;
        [JsonPropertyName("salesPrice")] public decimal salesPrice { get; set; }
        [JsonPropertyName("salesStandardCost")] public decimal salesStandardCost { get; set; }
        [JsonPropertyName("purchasePrice")] public decimal purchasePrice { get; set; }
        [JsonPropertyName("purchaseDescription")] public string? purchaseDescription { get; set; } = null;
        [JsonPropertyName("taxCodeID")] public string? taxCodeID { get; set; } = null;
        [JsonPropertyName("isTaxInclusive")] public bool isTaxInclusive { get; set; }
        [JsonPropertyName("isSold")] public bool isSold { get; set; } = true;
        [JsonPropertyName("accountStatus")] public string accountStatus { get; set; } = "Active";
        [JsonPropertyName("unitOfMeasureID")] public string? unitOfMeasureID { get; set; } = null;
        [JsonPropertyName("unitOfMeasureName")] public string? unitOfMeasureName { get; set; } = null;
        [JsonPropertyName("stockReorderLevel")] public decimal stockReorderLevel { get; set; }
        [JsonPropertyName("branchID")] public string? branchID { get; set; } = null;
        [JsonPropertyName("vendorItemCode")] public string? vendorItemCode { get; set; } = null;
        [JsonPropertyName("remarks")] public string? remarks { get; set; } = null;
        // hasPackage=true tells the server to process lstPackage items.
        [JsonPropertyName("hasPackage")] public bool hasPackage { get; set; } = false;
        [JsonPropertyName("validityDays")] public int validityDays { get; set; }
        [JsonPropertyName("lstPackage")] public List<PackageItemEntry> lstPackage { get; set; } = new();
        [JsonPropertyName("membershipCredit")] public string? membershipCredit { get; set; }
        [JsonPropertyName("IsOpenTopUp")] public bool IsOpenTopUp { get; set; }
        [JsonPropertyName("lstMembershipCredit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MembershipCreditEntry>? lstMembershipCredit { get; set; } = null;
        [JsonPropertyName("triggeredMemberTypeID")] public string? triggeredMemberTypeID { get; set; }
        [JsonPropertyName("triggeredMemberTypeName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? triggeredMemberTypeName { get; set; } = null;
        [JsonPropertyName("memberMainAccountCredit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double memberMainAccountCredit { get; set; } = 0;
        [JsonPropertyName("memberServiceCredit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double memberServiceCredit { get; set; } = 0;
        [JsonPropertyName("memberProductCredit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double memberProductCredit { get; set; } = 0;
        [JsonPropertyName("memberPoint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double memberPoint { get; set; } = 0;
        [JsonPropertyName("memberExpiryDays")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int memberExpiryDays { get; set; } = 0;
        [JsonPropertyName("staffCommissionA")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? staffCommissionA { get; set; } = null;
        [JsonPropertyName("staffCommissionB")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? staffCommissionB { get; set; } = null;
        [JsonPropertyName("staffCommissionC")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? staffCommissionC { get; set; } = null;
        [JsonPropertyName("staffCommissionD")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? staffCommissionD { get; set; } = null;
        [JsonPropertyName("pointToRedeem")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? PointToRedeem { get; set; } = null;
        [JsonPropertyName("allowPointRedemption")]
        public bool AllowPointRedemption { get; set; } = false;
    }

    /// <summary>One service/product line inside a package.</summary>
    public class PackageItemEntry
    {
        [JsonPropertyName("isLoading")] public bool isLoading { get; set; } = false;
        [JsonPropertyName("autoID")] public string? autoID { get; set; }
        [JsonPropertyName("packageID")] public string? packageID { get; set; }
        [JsonPropertyName("inventoryID")] public string inventoryID { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? description { get; set; }
        [JsonPropertyName("quantity")] public decimal quantity { get; set; } = 1;
        [JsonPropertyName("unitPrice")] public decimal unitPrice { get; set; }
        [JsonPropertyName("totalPrice")] public decimal totalPrice { get; set; }
        [JsonPropertyName("unitActualValue")] public decimal unitActualValue { get; set; }
        [JsonPropertyName("totalActualValue")] public decimal totalActualValue { get; set; }
        [JsonPropertyName("inventoryTypeID")] public int inventoryTypeID { get; set; }
        [JsonPropertyName("isDeferred")] public bool isDeferred { get; set; } = false;
        [JsonPropertyName("isVoided")] public bool isVoided { get; set; } = false;
        [JsonPropertyName("isConfirmed")] public bool isConfirmed { get; set; } = false;
        [JsonPropertyName("promotionMethod")] public int promotionMethod { get; set; } = 0;
        [JsonPropertyName("promoCondition")] public int promoCondition { get; set; } = 0;
        [JsonPropertyName("promoConditionAmt")] public double promoConditionAmt { get; set; } = 0;
        [JsonPropertyName("maxQuantity")] public double maxQuantity { get; set; } = 0;
        [JsonPropertyName("roundingOption")] public int roundingOption { get; set; } = 0;
        [JsonPropertyName("packageQuantityTypeID")] public int packageQuantityTypeID { get; set; } = 1;
        [JsonPropertyName("optionItems")] public string? optionItems { get; set; }
        [JsonPropertyName("optionGroups")] public string? optionGroups { get; set; }
        [JsonPropertyName("optionBrands")] public string? optionBrands { get; set; }
        [JsonPropertyName("headerCaption")] public string? headerCaption { get; set; }
        [JsonPropertyName("customRules")] public string? customRules { get; set; }
        [JsonPropertyName("saveAction")][JsonConverter(typeof(SaveActionConverter))] public string saveAction { get; set; } = "Added";
        [JsonPropertyName("isDirty")] public bool isDirty { get; set; } = true;
        [JsonPropertyName("lstAppliedDocumentLines")] public List<object> lstAppliedDocumentLines { get; set; } = new();
        [JsonPropertyName("lstCustomRules")] public List<object> lstCustomRules { get; set; } = new();
    }

    /// <summary>One membership credit entry. Sent inside objInventory.lstMembershipCredit of InventoryFullCreateRequest.</summary>
    public class MembershipCreditEntry
    {
        [JsonPropertyName("memberTypeID")] public string memberTypeID { get; set; } = string.Empty;
        [JsonPropertyName("memberCredit")] public decimal memberCredit { get; set; }
        [JsonPropertyName("saveAction")][JsonConverter(typeof(SaveActionConverter))] public string saveAction { get; set; } = "Added";
        [JsonPropertyName("isDirty")] public bool isDirty { get; set; } = true;
    }

    /// <summary>Branch availability record. Required for item to appear in LoadProxyByItemGroup.
    /// API returns and expects PascalCase keys.</summary>
    public class MasterAccountBranchEntry
    {
        [JsonPropertyName("MasterAccountID")] public string? masterAccountID { get; set; }
        [JsonPropertyName("BranchID")] public string branchID { get; set; } = string.Empty;
        [JsonPropertyName("BranchPrice")] public decimal branchPrice { get; set; }
        [JsonPropertyName("IsEnabled")] public bool isEnabled { get; set; } = true;
        [JsonPropertyName("GroupID")] public string? groupID { get; set; }
        [JsonPropertyName("SaveAction")][JsonConverter(typeof(SaveActionConverter))] public string saveAction { get; set; } = "Added";
        [JsonPropertyName("IsDirty")] public bool isDirty { get; set; } = true;
    }

    /// <summary>Result returned by api/InventoryFull/Create — contains the server-assigned package ID.</summary>
    public class PackageSaveResult
    {
        [JsonPropertyName("Id")] public string? Id { get; set; }
        [JsonPropertyName("SuccessMessage")] public string? SuccessMessage { get; set; }
    }

    /// <summary>Request body for api/InventoryFull/Create and Update. Credits go inside objInventory.lstMembershipCredit.</summary>
    public class InventoryFullCreateRequest
    {
        [JsonPropertyName("objInventory")] public InventoryCreateModel objInventory { get; set; } = new();
        [JsonPropertyName("lstMasterAccount_Branch")] public List<MasterAccountBranchEntry> lstMasterAccount_Branch { get; set; } = new();
        [JsonPropertyName("lstVendor_InventorySupplies")] public List<object> lstVendor_InventorySupplies { get; set; } = new();
        [JsonPropertyName("lstMasterAccount_Location")] public List<object> lstMasterAccount_Location { get; set; } = new();
        [JsonPropertyName("lstInventory_CommissionByGroup")] public List<object> lstInventory_CommissionByGroup { get; set; } = new();
        // api/InventoryFull/LoadRecord returns PointToRedeem at root level; send it at root level on writes too.
        [JsonPropertyName("PointToRedeem")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? PointToRedeem { get; set; } = null;
        [JsonPropertyName("AllowPointRedemption")]
        public bool AllowPointRedemption { get; set; } = false;
    }

    /// <summary>Maps api/InventoryFull/LoadRecord result.</summary>
    public class InventoryFullLoadDetail
    {
        [JsonPropertyName("validityDays")] public int validityDays { get; set; }
        [JsonPropertyName("remarks")] public string? remarks { get; set; }
        [JsonPropertyName("lstPackage")] public List<PackageItemEntry>? lstPackage { get; set; }
        [JsonPropertyName("lstMembershipCredit")] public List<MembershipCreditEntry>? lstMembershipCredit { get; set; }
        [JsonPropertyName("objInventory")] public InventoryFullLoadDetail? objInventory { get; set; }
        [JsonPropertyName("membershipCredit")] public string? membershipCredit { get; set; }
        [JsonPropertyName("IsOpenTopUp")] public bool IsOpenTopUp { get; set; }
        [JsonPropertyName("triggeredMemberTypeID")] public string? triggeredMemberTypeID { get; set; }
        [JsonPropertyName("triggeredMemberTypeName")] public string? triggeredMemberTypeName { get; set; }
        [JsonPropertyName("memberMainAccountCredit")] public double memberMainAccountCredit { get; set; }
        [JsonPropertyName("memberServiceCredit")] public double memberServiceCredit { get; set; }
        [JsonPropertyName("memberProductCredit")] public double memberProductCredit { get; set; }
        [JsonPropertyName("memberPoint")] public double memberPoint { get; set; }
        [JsonPropertyName("memberExpiryDays")] public int memberExpiryDays { get; set; }
        [JsonPropertyName("lstMasterAccount_Branch")] public List<MasterAccountBranchEntry>? lstMasterAccount_Branch { get; set; }
        [JsonPropertyName("PointToRedeem")]
        [JsonConverter(typeof(StringOrNumberDecimalConverter))]
        public decimal? PointToRedeem { get; set; }
        [JsonPropertyName("AllowPointRedemption")] public bool AllowPointRedemption { get; set; }
    }

    // ── SupportingTable ────────────────────────────────────────────────────────

    /// <summary>Response item returned by LoadListByType.</summary>
    public class SupportingTableItem
    {
        [JsonPropertyName("SupportingTableID")] public string? SupportingTableID { get; set; }
        [JsonPropertyName("SupportingTableName")] public string? SupportingTableName { get; set; }
        [JsonPropertyName("SupportingTableTypeID")] public int SupportingTableTypeID { get; set; }
        [JsonPropertyName("Active")] public bool Active { get; set; }
        [JsonPropertyName("Sequence")] public int Sequence { get; set; }
        [JsonPropertyName("Description")] public string? Description { get; set; }
        [JsonPropertyName("CreatedDateTime")] public string? CreatedDateTime { get; set; }
    }

    /// <summary>Request body for SupportingTable Create and Update.</summary>
    public class SupportingTableModel
    {
        [JsonPropertyName("supportingTableID")] public string? supportingTableID { get; set; } = null;
        [JsonPropertyName("supportingTableName")] public string supportingTableName { get; set; } = string.Empty;
        [JsonPropertyName("supportingTableTypeID")] public int supportingTableTypeID { get; set; }
        [JsonPropertyName("active")] public bool active { get; set; } = true;
        [JsonPropertyName("sequence")] public int sequence { get; set; }
        [JsonPropertyName("description")] public string? description { get; set; } = null;
        /// <summary>"Added" for create, "Changed" for update. Required — server ignores the record without it.</summary>
        [JsonPropertyName("saveAction")] public string saveAction { get; set; } = "Added";
        [JsonPropertyName("isDirty")] public bool isDirty { get; set; } = true;
    }

    public class LowStockItem
    {
        [JsonPropertyName("InventoryItemAccountID")] public string InventoryItemAccountID { get; set; } = string.Empty;
        [JsonPropertyName("DisplayCode")] public string DisplayCode { get; set; } = string.Empty;
        [JsonPropertyName("ProductCode")] public string ProductCode { get; set; } = string.Empty;
        [JsonPropertyName("AccountName")] public string AccountName { get; set; } = string.Empty;
        [JsonPropertyName("ItemGroupName")] public string ItemGroupName { get; set; } = string.Empty;
        [JsonPropertyName("BrandName")] public string BrandName { get; set; } = string.Empty;
        [JsonPropertyName("ExistingQuantity")] public decimal ExistingQuantity { get; set; }
        [JsonPropertyName("StockReorderLevel")] public decimal StockReorderLevel { get; set; }
        [JsonPropertyName("StockMaxLevel")] public decimal StockMaxLevel { get; set; }
        [JsonPropertyName("StockPackLevel")] public decimal StockPackLevel { get; set; }
        [JsonPropertyName("SalesOrderQuantity")] public decimal SalesOrderQuantity { get; set; }
        [JsonPropertyName("VendorOrderedQuantity")] public decimal VendorOrderedQuantity { get; set; }
        [JsonPropertyName("ShortFall")] public decimal ShortFall { get; set; }
        [JsonPropertyName("RecommendedOrderQuantiity")] public decimal RecommendedOrderQuantiity { get; set; }
        [JsonPropertyName("NeedToOrder")] public decimal NeedToOrder { get; set; }
        [JsonPropertyName("IsPurchased")] public bool IsPurchased { get; set; }
        [JsonPropertyName("MasterAccountBranchID")] public string MasterAccountBranchID { get; set; } = string.Empty;
        [JsonPropertyName("DefaultSupplierID")] public string? DefaultSupplierID { get; set; }
    }

    public class StockBalanceRequest
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("branchID")] public string? BranchID { get; set; }
        [JsonPropertyName("groupID")] public string? GroupID { get; set; }
        [JsonPropertyName("inventoryIDs")] public string? InventoryIDs { get; set; }
        [JsonPropertyName("filePath")] public string? FilePath { get; set; }
        [JsonPropertyName("fileName")] public string? FileName { get; set; }
        [JsonPropertyName("defaultVendorID")] public string? DefaultVendorID { get; set; }
        [JsonPropertyName("documentID")] public string? DocumentID { get; set; }
        [JsonPropertyName("financialDate")] public DateTime? FinancialDate { get; set; }
        [JsonPropertyName("endDate")] public DateTime? EndDate { get; set; }
        [JsonPropertyName("redemptionEndDate")] public DateTime? RedemptionEndDate { get; set; }
        [JsonPropertyName("minQuantityBalanceToShow")] public decimal MinQuantityBalanceToShow { get; set; }
        [JsonPropertyName("branchIDs")] public string? BranchIDs { get; set; }
        [JsonPropertyName("groupIDs")] public string? GroupIDs { get; set; }
        [JsonPropertyName("newExpiryDate")] public DateTime? NewExpiryDate { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }

    public class StockBalanceItem
    {
        [JsonPropertyName("InventoryID")] public string InventoryID { get; set; } = string.Empty;
        [JsonPropertyName("BatchNo")] public string? BatchNo { get; set; }
        [JsonPropertyName("Matrix")] public string? Matrix { get; set; }
        [JsonPropertyName("ExistingQuantity")] public decimal ExistingQuantity { get; set; }
        [JsonPropertyName("OrderPendingDelivery")] public decimal OrderPendingDelivery { get; set; }
        [JsonPropertyName("StockBalanceAfterSalesOrder")] public decimal StockBalanceAfterSalesOrder { get; set; }
        [JsonPropertyName("UOMConvertedExistingQuantity")] public decimal UOMConvertedExistingQuantity { get; set; }
        [JsonPropertyName("UOMConvertedOrderPendingDelivery")] public decimal UOMConvertedOrderPendingDelivery { get; set; }
        [JsonPropertyName("UOMConvertedStockBalanceAfterSalesOrder")] public decimal UOMConvertedStockBalanceAfterSalesOrder { get; set; }
    }
}
