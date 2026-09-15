using System;
using System.Collections.Generic;

namespace SenangRetails.Shared.Model
{
    // Base class for Service items
    public class ServiceItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>API identifier (MasterAccountID). Empty for locally-created items not yet saved.</summary>
        public string MasterAccountID { get; set; } = string.Empty;
        /// <summary>API identifier for the item category (ItemGroupID).</summary>
        public string ItemGroupID { get; set; } = string.Empty;
        /// <summary>Branch/outlet ID from the API. Required by the server when creating or updating.</summary>
        public string BranchID { get; set; } = string.Empty;
        public string ItemType { get; set; } = "Service";
        /// <summary>InventoryTypeID from the API. 3 = Service (no stock tracking). Required by the Update endpoint.</summary>
        public int InventoryTypeID { get; set; } = 3;

        public string ServiceSection { get; set; } = string.Empty;
        public string ServiceFolder { get; set; } = string.Empty;
        public string DisplayImageUrl { get; set; } = string.Empty;

        public bool IsAvailable { get; set; } = true;
        public string Station { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        /// <summary>Display Code (shown in UI as "Display Code").</summary>
        public string VendorItemCode { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public string TaxCode { get; set; } = string.Empty;
        public bool PriceInclusiveTax { get; set; } = false;
        public int? Duration { get; set; }

        public string Barcode { get; set; } = string.Empty;
        public string Unit { get; set; } = "unit";
        /// <summary>Original UnitOfMeasureID from API. Preserved so updates send the correct ID back.</summary>
        public string UnitID { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public bool OutletAvailability { get; set; } = true;
        public decimal FreePoint { get; set; }
        public decimal RedeemPoint { get; set; }
        public string BillOfMaterial { get; set; } = string.Empty;

        public string Policy { get; set; } = string.Empty;
        public string TermCondition1 { get; set; } = string.Empty;
        public string TermCondition2 { get; set; } = string.Empty;
        public string TermCondition3 { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string ImageName { get; set; } = string.Empty;

        public bool Commission1IsPercent { get; set; } = true;
        public bool Commission2IsPercent { get; set; } = true;
        public bool Commission3IsPercent { get; set; } = true;

        public decimal Commission1 { get; set; }
        public decimal Commission2 { get; set; }
        public decimal Commission3 { get; set; }
        public string Remark { get; set; } = string.Empty;
    }

    public class ProductItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>API identifier (MasterAccountID). Empty for locally-created items not yet saved.</summary>
        public string MasterAccountID { get; set; } = string.Empty;
        /// <summary>API identifier for the item category (ItemGroupID).</summary>
        public string ItemGroupID { get; set; } = string.Empty;
        /// <summary>Branch/outlet ID from the API. Required by the server when creating or updating.</summary>
        public string BranchID { get; set; } = string.Empty;
        public string ItemType { get; set; } = "Product";
        /// <summary>InventoryTypeID from the API. 1 = Product (standard inventory). Required by the Update endpoint.</summary>
        public int InventoryTypeID { get; set; } = 1;

        public string ProductSection { get; set; } = string.Empty;
        public string ProductFolder { get; set; } = string.Empty;
        public string DisplayImageUrl { get; set; } = string.Empty;

        public bool IsAvailable { get; set; } = true;
        public string Station { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        /// <summary>Item Code (VendorItemCode from API).</summary>
        public string VendorItemCode { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public string TaxCode { get; set; } = string.Empty;
        public bool PriceInclusiveTax { get; set; } = false;
        public int? Duration { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public string Unit { get; set; } = "unit"; // Display name shown in UI
        /// <summary>Original UnitOfMeasureID from API. Preserved so updates send the correct ID back.</summary>
        public string UnitID { get; set; } = string.Empty;
        public int? LowStockAlert { get; set; }

        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public bool OutletAvailability { get; set; } = true;
        public decimal FreePoint { get; set; }
        public decimal RedeemPoint { get; set; }
        public string BillOfMaterial { get; set; } = string.Empty;

        public string Policy { get; set; } = string.Empty;
        public string TermCondition1 { get; set; } = string.Empty;
        public string TermCondition2 { get; set; } = string.Empty;
        public string TermCondition3 { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string ImageName { get; set; } = string.Empty;

        public bool Commission1IsPercent { get; set; } = true;
        public bool Commission2IsPercent { get; set; } = true;
        public bool Commission3IsPercent { get; set; } = true;

        public decimal Commission1 { get; set; }
        public decimal Commission2 { get; set; }
        public decimal Commission3 { get; set; }
        public string Remark { get; set; } = string.Empty;
    }

    public class PackageItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string MasterAccountID { get; set; } = string.Empty;
        public string ItemType { get; set; } = "Package";
        public string PackageSection { get; set; } = string.Empty;
        public string PackageFolder { get; set; } = string.Empty;
        public string DisplayImageUrl { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string Station { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int? Duration { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal Credits { get; set; }
        public int Points { get; set; }
        public decimal PackageValue { get; set; }
        public List<string> VoucherList { get; set; } = new List<string>();
        public string Unit { get; set; } = "Unit";
        public int? LowStockAlert { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public bool OutletAvailability { get; set; } = true;
        public decimal FreePoint { get; set; }
        public decimal RedeemPoint { get; set; }
        public string BillOfMaterial { get; set; } = string.Empty;
        public string Policy { get; set; } = string.Empty;
        public string TermCondition1 { get; set; } = string.Empty;
        public string TermCondition2 { get; set; } = string.Empty;
        public string TermCondition3 { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageName { get; set; } = string.Empty;
    }

    public class DiscountItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemType { get; set; } = "Discount";
        public string DiscountSection { get; set; } = string.Empty;
        public string DiscountFolder { get; set; } = string.Empty;
        public string DisplayImageUrl { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string Station { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int? Duration { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal Credits { get; set; }
        public int Points { get; set; }
        public decimal DiscountValue { get; set; }
        public List<string> VoucherList { get; set; } = new List<string>();
        public string Unit { get; set; } = "Unit";
        public int? LowStockAlert { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public bool OutletAvailability { get; set; } = true;
        public decimal FreePoint { get; set; }
        public decimal RedeemPoint { get; set; }
        public string BillOfMaterial { get; set; } = string.Empty;
        public string Policy { get; set; } = string.Empty;
        public string TermCondition1 { get; set; } = string.Empty;
        public string TermCondition2 { get; set; } = string.Empty;
        public string TermCondition3 { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageName { get; set; } = string.Empty;
    }

    // --- NEW MODELS ---

    public class CustomMenuItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        // Use unicode escape sequence for "Page Facing Up" emoji to avoid encoding issues
        public string Icon { get; set; } = "\U0001F4C4";
        public string Color { get; set; } = "#FF9800"; // Default color
    }

    public class OnlineMenuItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemName { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string DisplayImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Duration { get; set; } = string.Empty; // "1 Hour", etc.
    }

    public class CatalogItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string DisplayImageUrl { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public decimal RedeemableCredits { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class StationItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class MenuSubCategoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}