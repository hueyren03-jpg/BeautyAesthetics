using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using SenangRetails.Shared.Models.DTOs.MembersCredit;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CashSalesRequest
    {
        [JsonPropertyName("strID")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }
    }

    public class LoadWithIdReq
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class ThermalReceiptPOSRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("intDocumentTypeID")]
        public int IntDocumentTypeID { get; set; }
    }

    public class CashSalesResultWrapper
    {
        [JsonPropertyName("objDoc_CashSales")]
        public CashSalesDTO? ObjDoc_CashSales { get; set; }

        [JsonPropertyName("lstDocumentLine")]
        public List<DocumentLine> FirstDocumentLines { get; set; } = new();

        [JsonPropertyName("lstReceiptLines")]
        public List<ReceiptLine> FirstReceiptLines { get; set; } = new();
        public List<LstCashSalesSeriesUnconsumedItem> lstCashSales_Series_UnconsumedItem { get; set; }


    }

    //  DTO
    public class CashSalesDTO
    {
        [JsonPropertyName("DocumentID")]
        public string DocumentID { get; set; } = string.Empty;

        [JsonPropertyName("DocumentTypeID")]
        public int DocumentTypeID { get; set; }

        [JsonPropertyName("DisplayCode")]
        public string DisplayCode { get; set; } = string.Empty;

        [JsonPropertyName("ReferenceNumber")]
        public string ReferenceNumber { get; set; } = string.Empty;

        [JsonPropertyName("FriendlyDocumentName")]
        public string FriendlyDocumentName { get; set; } = string.Empty;

        [JsonPropertyName("BranchID")]
        public string BranchID { get; set; } = string.Empty;

        [JsonPropertyName("FinancialDate")]
        public DateTime FinancialDate { get; set; }

        [JsonPropertyName("AccountID")]
        public string AccountID { get; set; } = string.Empty;

        [JsonPropertyName("AccountName")]
        public string AccountName { get; set; } = string.Empty;

        [JsonPropertyName("TotalBeforeTax")]
        public decimal TotalBeforeTax { get; set; }

        [JsonPropertyName("TaxAmount")]
        public decimal TaxAmount { get; set; }

        [JsonPropertyName("TotalAfterTax")]
        public decimal TotalAfterTax { get; set; }
        [JsonPropertyName("RoundingAmount")]
        public decimal RoundingAmount { get; set; }

        [JsonPropertyName("LocalTotalBeforeTax")]
        public decimal LocalTotalBeforeTax { get; set; }

        [JsonPropertyName("LocalTotalAfterTax")]
        public decimal LocalTotalAfterTax { get; set; }

        [JsonPropertyName("eInvoiceTypeID")]
        public int eInvoiceTypeID { get; set; }

        [JsonPropertyName("eInvoiceStatus")]
        public string eInvoiceStatus { get; set; } = string.Empty;
        [JsonPropertyName("eInvoiceDocumentUid")]
        public string eInvoiceDocumentUid { get; set; } = string.Empty;
        [JsonPropertyName("eInvoiceLongId")]
        public string eInvoiceLongId { get; set; } = string.Empty;

        [JsonPropertyName("CreatedBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("CreatedDateTime")]
        public DateTime CreatedDateTime { get; set; }

        [JsonPropertyName("ModifiedBy")]
        public string? ModifiedBy { get; set; }

        [JsonPropertyName("ModifiedDateTime")]
        public DateTime ModifiedDateTime { get; set; }

        [JsonPropertyName("CounterID")]
        public string CounterID { get; set; } = string.Empty;

        [JsonPropertyName("TransactionCurrencyID")]
        public string TransactionCurrencyID { get; set; } = string.Empty;

        [JsonPropertyName("LocalCurrencyID")]
        public string LocalCurrencyID { get; set; } = string.Empty;

        [JsonPropertyName("TransactionCurrencyName")]
        public string TransactionCurrencyName { get; set; } = string.Empty;

        [JsonPropertyName("LocalCurrencyName")]
        public string LocalCurrencyName { get; set; } = string.Empty;

        [JsonPropertyName("IsLocked")]
        public bool IsLocked { get; set; }

        [JsonPropertyName("IsVoid")]
        public bool IsVoid { get; set; }

        [JsonPropertyName("TablePaidTime")]
        public DateTime TablePaidTime { get; set; }

        [JsonPropertyName("TotalBeforeTax_NonServiceCharge")]
        public decimal TotalBeforeTax_NonServiceCharge { get; set; }

        [JsonPropertyName("TotalBeforeTax_ServiceCharge")]
        public decimal TotalBeforeTax_ServiceCharge { get; set; }

        [JsonPropertyName("GroupID")]
        public string GroupID { get; set; } = string.Empty;

        [JsonPropertyName("CashierName")]
        public string CashierName { get; set; } = string.Empty;
        [JsonPropertyName("MasterAccountID")]
        public string MasterAccountID { get; set; } = string.Empty;

    }

    // Request DTO

    public class RequestBillDownloadLinkRequest
    {
        [JsonPropertyName("documentTypeID")]
        public int DocumentTypeID { get; set; }

        [JsonPropertyName("documentID")]
        public string DocumentID { get; set; } = string.Empty;

        [JsonPropertyName("financialDate")]
        public DateTime FinancialDate { get; set; }
    }


    // Response DTO
    public class ReceiptResponse
    {
        [JsonPropertyName("Version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("StatusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("ResponseException")]
        public object? ResponseException { get; set; }

        [JsonPropertyName("Result")]
        public string? Result { get; set; }
    }

    // delete request dto
    public class DeleteCashSalesRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("branchID")]
        public string BranchID { get; set; } = string.Empty;

        [JsonPropertyName("groupID")]
        public string GroupID { get; set; } = string.Empty;

        [JsonPropertyName("inventoryIDs")]
        public string InventoryIDs { get; set; } = string.Empty;

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("defaultVendorID")]
        public string DefaultVendorID { get; set; } = string.Empty;

        [JsonPropertyName("documentID")]
        public string DocumentID { get; set; } = string.Empty;

        [JsonPropertyName("financialDate")]
        public DateTime FinancialDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("redemptionEndDate")]
        public DateTime RedemptionEndDate { get; set; }

        [JsonPropertyName("minQuantityBalanceToShow")]
        public int MinQuantityBalanceToShow { get; set; }

        [JsonPropertyName("branchIDs")]
        public string BranchIDs { get; set; } = string.Empty;

        [JsonPropertyName("groupIDs")]
        public string GroupIDs { get; set; } = string.Empty;

        [JsonPropertyName("newExpiryDate")]
        public DateTime NewExpiryDate { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class SaveCashSalesResult
    {
        [JsonPropertyName("DocumentID")]
        public string DocumentID { get; set; } = string.Empty;
    }

    public class CreateCashSalesRequest
    {
        [JsonPropertyName("objDoc_CashSales")]
        public ObjDocCashSales ObjDoc_CashSales { get; set; } = new();

        [JsonPropertyName("lstDocumentLine")]
        public List<DocumentLine> FirstDocumentLines { get; set; } = new();

        [JsonPropertyName("lstReceiptLines")]
        public List<ReceiptLine> FirstReceiptLines { get; set; } = new();

        [JsonPropertyName("lstARAPOutstanding_MemberCredit")]
        public List<SaveMemberCreditRequest>? LstARAPOutstanding_MemberCredit { get; set; }
    }

    public class ObjDocCashSales
    {
        public string? LockedByCounter { get; set; } = null;
        public string? VerifyStatus { get; set; } = null;
        public bool IsLoading { get; set; } = false;

        public string DocumentID { get; set; } = string.Empty;
        public string BranchID { get; set; } = string.Empty;
        public string EditBranchID { get; set; } = string.Empty;
        public DateTime FinancialDate { get; set; }

        public string AccountID { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        public string TransactionCurrencyID { get; set; } = string.Empty;
        public string LocalCurrencyID { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDateTime { get; set; }
        public DateTime UpdateTimeStamp { get; set; }

        public string CounterID { get; set; } = string.Empty;
        public DateTime TablePaidTime { get; set; }

        public string TransactionCurrencyName { get; set; } = "MYR";
        public string LocalCurrencyName { get; set; } = "MYR";

        public decimal RoundingAmount { get; set; }

        public string GroupID { get; set; } = string.Empty;
        public int SaveAction { get; set; }
    }

    public class DocumentLine
    {
        public decimal DocumentLineTypeID { get; set; }
        public decimal OwnerDocumentTypeID { get; set; }
        public decimal LineOrder { get; set; }
        public string TaxCodeID { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }
        public string FinancialAccountID { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal InventoryTypeID { get; set; }
        public string LineItemID { get; set; } = string.Empty;
        public decimal Discount { get; set; }
        public string AccountID { get; set; } = string.Empty;
        public string UnitOfMeasurementID { get; set; } = string.Empty;
        //~C requested to add SKUName basically just use UnitOfMeasurementID
        public string SKUName { get; set; } = string.Empty;

        public decimal OriginalKitPrice { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string BranchID { get; set; } = string.Empty;

        public decimal ConvertedAmount { get; set; }
        public decimal ExchangeRate { get; set; }
        public string CurrencyID { get; set; } = string.Empty;
        public decimal UnitActualValue { get; set; }
        public string GSTTypeID { get; set; } = string.Empty;
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ConvertedTaxAmount { get; set; }

        public bool IsTaxInclusive { get; set; }

        public decimal SubTotalBeforeGST { get; set; }
        public decimal ConvertedSubTotalBeforeGST { get; set; }

        public string LineItemDisplayCode { get; set; } = string.Empty;
        public string DiningType { get; set; } = string.Empty;
        public string EditBranchID { get; set; } = string.Empty;
        public string GroupID { get; set; } = string.Empty;
        public string RefCompanyName { get; set; } = string.Empty;

        public DateTime FinancialDate { get; set; }
        public List<LstCashSalesSeriesUnconsumedItem> lstCashSales_Series_UnconsumedItem { get; set; } = new();

        [JsonPropertyName("objCustomerServiceRecords")]
        public ObjCustomerServiceRecords ObjCustomerServiceRecords { get; set; } = new();
        public string? SourceDocumentLineID { get; set; }
        public int ActivityTypeID { get; set; }
    }

    public class LstCashSalesSeriesUnconsumedItem
    {
        public bool IsLoading { get; set; }
        public string AutoID { get; set; }
        public DateTime FinancialDate { get; set; }
        public string DocumentID { get; set; }
        public string DisplayCode { get; set; }
        public string DocumentLineID { get; set; }
        public object SourceDocumentLineID { get; set; }
        public string CustomerAccountID { get; set; }
        public string EmployeeID { get; set; }
        public string PackageID { get; set; }
        public string PackageItemID { get; set; }
        public string InventoryID { get; set; }
        public string Description { get; set; }
        public int QuantityPurchased { get; set; }
        public int UnitPrice { get; set; }
        public int TotalPrice { get; set; }
        public int QuantityRedeemed { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int ActivityTypeID { get; set; }
        public int SourceUnitPrice { get; set; }
        public double UnitActualValue { get; set; }
        public double TotalActualValue { get; set; }
        public int SourceUnitActualValue { get; set; }
        public object OptionItems { get; set; }
        public object OptionGroups { get; set; }
        public object OptionBrands { get; set; }
        public string BranchID { get; set; }
        public string GroupID { get; set; }
        public object Matrix { get; set; }
        public object MatrixGroupID { get; set; }
        public object FirstExtensionBy { get; set; }
        public DateTime FirstExtensionDate { get; set; }
        public int FirstExtensionDays { get; set; }
        public object SecondExtensionBy { get; set; }
        public DateTime SecondExtensionDate { get; set; }
        public int SecondExtensionDays { get; set; }
        public object LastExtendedBy { get; set; }
        public DateTime LastExtensionDate { get; set; }
        public object POSReceiptLineID { get; set; }
        public bool IsOpeningBalance { get; set; }
        public int SaveAction { get; set; }
        public bool IsDirty { get; set; }
        public object PackageName { get; set; }
        public object PackageCode { get; set; }
        public int QuantityAvailable { get; set; }
        public int QuantityUtilised { get; set; }
        public int NetBalanceAfterUtilised { get; set; }
        public int CurrentRedeemQuantity { get; set; }
        public object CustomerName { get; set; }
        public object CustomerCode { get; set; }
        public bool IsRedeemable { get; set; }
        public int TotalPVRedeemed { get; set; }
        public int TotalAVRedeemed { get; set; }
        public int BalancePVValue { get; set; }
        public int BalanceAVValue { get; set; }
    }

    public class ObjCustomerServiceRecords
    {
        public decimal RM { get; set; }
    }

    public class ReceiptLine
    {
        public string Description { get; set; } = string.Empty;
        public decimal POSReceiptLineAmount { get; set; }
        public decimal POSReceiptChangeAmount { get; set; }
        public string FinancialAccountID { get; set; } = string.Empty;

        public decimal POSPaymentTypeID { get; set; }
        public string BranchID { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;

        public string CurrencyID { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; }
        public string GroupID { get; set; } = string.Empty;
        public string CurrencyName { get; set; } = "MYR";
        public DateTime FinancialDate { get; set; }
    }

    public class CreateCashSalesResponse
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("DisplayCode")]
        public string DisplayCode { get; set; } = string.Empty;

        [JsonPropertyName("SuccessMessage")]
        public string SuccessMessage { get; set; } = string.Empty;
    }

    public class PaymentLine
    {
        public int PaymentTypeId { get; set; }
        public string PaymentName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string FinancialAccountId { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
    }
}
