using EBI.DM;
using EBI.UC;
using EBI.Enum;
using Microsoft.JSInterop;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.BranchService;
using SenangRetails.Shared.Services.DataLayer.Offline;
using SenangRetails.Shared.Services.FileDownloadService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.TaxRateService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using static SenangRetails.Shared.Pages.Orders;

namespace SenangRetails.Shared.Services.CashSalesService
{
    public class CashSalesService : ICashSalesService
    {
        private readonly CashSalesAC _ac;
        private readonly AppState _appState;
        private readonly BranchAC _branchAC;
        private readonly IJSRuntime _js;
        private readonly IFileDownloadService _fileDownloadService;
        private readonly IInventoryService _inventoryService;
        private readonly IOfflineCashSalesStorage _offlineStorage;

        public CashSalesService(
            CashSalesAC ac,
            AppState appState,
            BranchAC branchAC,
            IJSRuntime JS,
            IFileDownloadService fileDownloadService,
            IInventoryService inventoryService,
            IOfflineCashSalesStorage offlineStorage)
        {
            _ac = ac;
            _appState = appState;
            _branchAC = branchAC;
            _js = JS;
            _fileDownloadService = fileDownloadService;
            _inventoryService = inventoryService;
            _offlineStorage = offlineStorage;
        }

        public async Task<(bool Success, string Message, string? DisplayCode, string? DocumentId)> CompletePaymentAsync(Doc_CashSales order, List<PaymentLine> payments)
        {
            try
            {
                string branchId = _appState.SelectedBranchID;
                if (string.IsNullOrEmpty(branchId))
                {
                    try
                    {
                        branchId = await _js.InvokeAsync<string>("localStorage.getItem", "currentBranch");

                        if (!string.IsNullOrEmpty(branchId))
                        {
                            _appState.SelectedBranchID = branchId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading from localStorage: {ex.Message}");
                    }
                }
                
                var groupId = _appState.SelectedBranchGroupID;
                var now = DateTime.Now;

                string gstTypeId = string.Empty;
                var branchRecord = await _branchAC.LoadRecordAsync(branchId);
                if (branchRecord?.statusCode == 200 && branchRecord.result != null)
                    gstTypeId = branchRecord.result.TaxTypeID ?? string.Empty;

                var header = new EBI.DM.Doc_CashSalesDM
                {
                    DocumentID = order?.objDoc_CashSales?.DocumentID ?? string.Empty,
                    DisplayCode = order?.objDoc_CashSales?.DisplayCode ?? string.Empty,
                    SaveAction = order?.objDoc_CashSales?.SaveAction ?? EBI.Enum.EntityState.Added,
                    IsDirty = true,
                    IsLocked = order?.objDoc_CashSales?.IsLocked ?? false,
                    IsVoid = order?.objDoc_CashSales?.IsVoid ?? false,
                    TourismTax = order?.objDoc_CashSales?.TourismTax ?? 0m,
                    HeritageTax = order?.objDoc_CashSales?.HeritageTax ?? 0m,
                    BranchID = string.IsNullOrEmpty(order?.objDoc_CashSales?.BranchID) ? (branchId ?? "") : order.objDoc_CashSales.BranchID,
                    EditBranchID = string.IsNullOrEmpty(order?.objDoc_CashSales?.EditBranchID) ? (branchId ?? "") : order.objDoc_CashSales.EditBranchID,
                    GroupID = string.IsNullOrEmpty(order?.objDoc_CashSales?.GroupID) ? (branchRecord?.result?.BranchGroupID ?? groupId ?? "") : order.objDoc_CashSales.GroupID,
                    LocalCurrencyName = string.IsNullOrEmpty(order?.objDoc_CashSales?.LocalCurrencyName) ? (branchRecord?.result?.CurrencyName ?? "MYR") : order.objDoc_CashSales.LocalCurrencyName,
                    FinancialDate = order?.objDoc_CashSales?.FinancialDate == default ? now : order.objDoc_CashSales.FinancialDate,
                    AccountID = order?.objDoc_CashSales?.AccountID ?? string.Empty, 
                    AccountName = order?.objDoc_CashSales?.AccountName ?? string.Empty, 
                    DocumentTypeID = order?.objDoc_CashSales?.DocumentTypeID ?? 5,
                    ReferenceNumber = order?.objDoc_CashSales?.ReferenceNumber ?? string.Empty,
                    Phone = order?.objDoc_CashSales?.Phone ?? string.Empty,
                    CashierName = order?.objDoc_CashSales?.CashierName ?? string.Empty,
                    SeatNo = order?.objDoc_CashSales?.SeatNo ?? string.Empty,
                    TotalBeforeTax_ServiceCharge = order?.objDoc_CashSales?.TotalBeforeTax_ServiceCharge ?? 0m,
                    CreatedDateTime = order?.objDoc_CashSales?.CreatedDateTime == default ? now : order.objDoc_CashSales.CreatedDateTime,
                    UpdateTimeStamp = order?.objDoc_CashSales?.UpdateTimeStamp == default ? now : order.objDoc_CashSales.UpdateTimeStamp,
                    TablePaidTime = order?.objDoc_CashSales?.TablePaidTime == default ? now : order.objDoc_CashSales.TablePaidTime,
                    TransactionCurrencyID = string.IsNullOrEmpty(order?.objDoc_CashSales?.TransactionCurrencyID) ? (branchRecord?.result?.CurrencyID ?? "MYR") : order.objDoc_CashSales.TransactionCurrencyID,
                    TransactionCurrencyName = string.IsNullOrEmpty(order?.objDoc_CashSales?.TransactionCurrencyName) ? (branchRecord?.result?.CurrencyName ?? "MYR") : order.objDoc_CashSales.TransactionCurrencyName,
                    ExchangeRate = (order?.objDoc_CashSales == null || order.objDoc_CashSales.ExchangeRate == 0) ? 1m : order.objDoc_CashSales.ExchangeRate,
                    FriendlyDocumentName = order?.objDoc_CashSales?.FriendlyDocumentName 
                };

                var docLines = new ObservableCollection<DocumentLineTableDM>();
                int lineOrder = 1;
                decimal totalTax = 0;
                decimal totalBeforeTax = 0;

                foreach (var item in order.lstDocumentLine)
                {
                    decimal gross = item.UnitPrice * item.Quantity;
                    decimal discount = Math.Max(0, item.Discount);
                    if (discount > gross)
                        discount = gross;

                    OrderLineTaxCalculator.ComputeLineAmounts(
                        item.UnitPrice,
                        item.Quantity,
                        discount,
                        item.TaxPercentage,
                        item.IsTaxInclusive,
                        out decimal beforeGst,
                        out decimal lineTax);

                    decimal lineSubTotalIncTax = Math.Round(beforeGst + lineTax, 2, MidpointRounding.AwayFromZero);

                    var line = new DocumentLineTableDM
                    {
                        DocumentLineTypeID = 1,
                        LineOrder = lineOrder++,
                        LineItemID = item.LineItemID,
                        InventoryItemAccountID = item.InventoryItemAccountID,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = discount,
                        RefCompanyName = !string.IsNullOrEmpty(item.RefCompanyName) ? item.RefCompanyName : item.Memo,
                        Memo = !string.IsNullOrEmpty(item.Memo) ? item.Memo : item.RefCompanyName,
                        UnitOfMeasurementID = item.UnitOfMeasurementID,
                        SKUName = item.UnitOfMeasurementID,
                        TaxCodeID = item.TaxCodeID ?? string.Empty,
                        GSTTypeID = gstTypeId,
                        TaxPercentage = item.TaxPercentage,
                        IsTaxInclusive = item.IsTaxInclusive,
                        SubTotal = lineSubTotalIncTax,
                        SubTotalBeforeGST = beforeGst,
                        ConvertedSubTotalBeforeGST = beforeGst,
                        TaxAmount = lineTax,
                        ConvertedTaxAmount = lineTax,
                        ConvertedAmount = lineSubTotalIncTax,
                        ExchangeRate = item.ExchangeRate == 0 ? 1m : item.ExchangeRate,
                        InventoryTypeID = item.InventoryTypeID,
                        LineItemDisplayCode = item.LineItemDisplayCode,
                        BranchID = string.IsNullOrEmpty(item.BranchID) ? branchId : item.BranchID,
                        GroupID = string.IsNullOrEmpty(item.GroupID) ? groupId : item.GroupID,
                        FinancialDate = item.FinancialDate == default ? now : item.FinancialDate,
                        GSTTaxPointDate = header.DocumentTypeID == 52 ? default : (item.GSTTaxPointDate == default ? now : item.GSTTaxPointDate),
                        EditBranchID = string.IsNullOrEmpty(item.EditBranchID) ? branchId : item.EditBranchID,
                        CurrencyID = string.IsNullOrEmpty(item.CurrencyID) ? (branchRecord?.result?.CurrencyID ?? string.Empty) : item.CurrencyID,
                        DocumentLineID = string.IsNullOrEmpty(item.DocumentLineID) ? (lineOrder - 1).ToString("D2") : item.DocumentLineID,
                        ActivityTypeID = item.ActivityTypeID,
                        SourceDocumentLineID = item.SourceDocumentLineID,
                        KitMemberID = item.KitMemberID,
                        RedeemedMinutes = item.RedeemedMinutes,
                        OwnerDocumentTypeID = item.OwnerDocumentTypeID != 0 ? item.OwnerDocumentTypeID : header.DocumentTypeID,
                        MemberDiscount = item.MemberDiscount,
                        MembershipCredit = item.MembershipCredit,
                        MemberCreditAccountID = item.MemberCreditAccountID,
                        MemberTypeID = item.MemberTypeID,
                    };

                    if (item.lstSalesCommissionByDocumentLine != null)
                    {
                        foreach (var comm in item.lstSalesCommissionByDocumentLine)
                        {
                            line.lstSalesCommissionByDocumentLine.Add(comm);
                        }
                    }

                    // Copy ARAPOutstanding_MemberCredit (for credits/membership credit associated with packages or topups)
                    if (item.lstARAPOutstanding_MemberCredit != null && item.lstARAPOutstanding_MemberCredit.Count > 0)
                    {
                        foreach (var credit in item.lstARAPOutstanding_MemberCredit)
                        {
                            var newCredit = new ARAPOutstanding_MemberCreditDM
                            {
                                AccountID = string.IsNullOrEmpty(credit.AccountID) ? header.AccountID : credit.AccountID,
                                FinancialDate = credit.FinancialDate == default ? header.FinancialDate : credit.FinancialDate,
                                DueDate = credit.DueDate,
                                DocumentID = string.IsNullOrEmpty(credit.DocumentID) ? (order.objDoc_CashSales?.DocumentID ?? string.Empty) : credit.DocumentID,
                                DisplayCode = string.IsNullOrEmpty(credit.DisplayCode) ? header.DisplayCode : credit.DisplayCode,
                                DocumentTypeID = credit.DocumentTypeID == 0 ? header.DocumentTypeID : credit.DocumentTypeID,
                                DocumentTypeName = string.IsNullOrEmpty(credit.DocumentTypeName) ? header.FriendlyDocumentName : credit.DocumentTypeName,
                                DocumentLineID = string.IsNullOrEmpty(credit.DocumentLineID) ? line.DocumentLineID : credit.DocumentLineID,
                                ItemDescription = credit.ItemDescription,
                                CurrencyID = string.IsNullOrEmpty(credit.CurrencyID) ? header.TransactionCurrencyID : credit.CurrencyID,
                                CurrencyName = string.IsNullOrEmpty(credit.CurrencyName) ? header.TransactionCurrencyName : credit.CurrencyName,
                                ExchangeRate = credit.ExchangeRate == 0 ? header.ExchangeRate : credit.ExchangeRate,
                                InterOutletRatio = credit.InterOutletRatio,
                                InterOutletAmount = credit.InterOutletAmount,
                                MGMTier = credit.MGMTier,
                                TotalAmount = credit.TotalAmount,
                                BranchID = string.IsNullOrEmpty(credit.BranchID) ? header.BranchID : credit.BranchID,
                                GroupID = string.IsNullOrEmpty(credit.GroupID) ? header.GroupID : credit.GroupID,
                                LineItemID = credit.LineItemID,
                                MemberTypeID = credit.MemberTypeID,
                                SaveAction = EBI.Enum.EntityState.Added,
                                IsDirty = true
                            };
                            line.lstARAPOutstanding_MemberCredit.Add(newCredit);
                        }
                    }

                    // Copy Series/Package Unconsumed Items
                    bool copiedUnconsumed = false;
                    if (item.lstCashSales_Series_UnconsumedItem != null && item.lstCashSales_Series_UnconsumedItem.Count > 0)
                    {
                        copiedUnconsumed = true;
                        foreach (var unconsumed in item.lstCashSales_Series_UnconsumedItem)
                        {
                            var newUnconsumed = new CashSales_Series_UnconsumedItemDM
                            {
                                DocumentID = string.IsNullOrEmpty(unconsumed.DocumentID) ? (order.objDoc_CashSales?.DocumentID ?? string.Empty) : unconsumed.DocumentID,
                                CustomerAccountID = string.IsNullOrEmpty(unconsumed.CustomerAccountID) ? header.AccountID : unconsumed.CustomerAccountID,
                                PackageID = unconsumed.PackageID,
                                InventoryID = unconsumed.InventoryID,
                                Description = unconsumed.Description,
                                QuantityPurchased = unconsumed.QuantityPurchased,
                                QuantityRedeemed = unconsumed.QuantityRedeemed,
                                UnitPrice = unconsumed.UnitPrice,
                                TotalPrice = unconsumed.TotalPrice,
                                PackageItemID = unconsumed.PackageItemID,
                                DocumentLineID = string.IsNullOrEmpty(unconsumed.DocumentLineID) ? line.DocumentLineID : unconsumed.DocumentLineID,
                                EmployeeID = unconsumed.EmployeeID,
                                ActivityTypeID = unconsumed.ActivityTypeID,
                                SourceUnitPrice = unconsumed.SourceUnitPrice,
                                UnitActualValue = unconsumed.UnitActualValue,
                                TotalActualValue = unconsumed.TotalActualValue,
                                SourceUnitActualValue = unconsumed.SourceUnitActualValue,
                                OptionItems = unconsumed.OptionItems,
                                OptionBrands = unconsumed.OptionBrands,
                                OptionGroups = unconsumed.OptionGroups,
                                ExpiryDate = unconsumed.ExpiryDate,
                                BranchID = string.IsNullOrEmpty(unconsumed.BranchID) ? header.BranchID : unconsumed.BranchID,
                                GroupID = string.IsNullOrEmpty(unconsumed.GroupID) ? header.GroupID : unconsumed.GroupID,
                                FinancialDate = unconsumed.FinancialDate == default ? header.FinancialDate : unconsumed.FinancialDate,
                                SaveAction = EBI.Enum.EntityState.Added,
                                IsDirty = true
                            };
                            line.lstCashSales_Series_UnconsumedItem.Add(newUnconsumed);
                        }
                    }

                    // Copy Unconsumed Time Items
                    if (item.lstCashSales_UnconsumedTime != null && item.lstCashSales_UnconsumedTime.Count > 0)
                    {
                        copiedUnconsumed = true;
                        foreach (var unconsumedTime in item.lstCashSales_UnconsumedTime)
                        {
                            var newUnconsumedTime = new CashSales_Series_UnconsumedItemDM
                            {
                                DocumentID = string.IsNullOrEmpty(unconsumedTime.DocumentID) ? (order.objDoc_CashSales?.DocumentID ?? string.Empty) : unconsumedTime.DocumentID,
                                CustomerAccountID = string.IsNullOrEmpty(unconsumedTime.CustomerAccountID) ? header.AccountID : unconsumedTime.CustomerAccountID,
                                PackageID = unconsumedTime.PackageID,
                                InventoryID = unconsumedTime.InventoryID,
                                Description = unconsumedTime.Description,
                                QuantityPurchased = unconsumedTime.QuantityPurchased,
                                QuantityRedeemed = unconsumedTime.QuantityRedeemed,
                                UnitPrice = unconsumedTime.UnitPrice,
                                TotalPrice = unconsumedTime.TotalPrice,
                                PackageItemID = unconsumedTime.PackageItemID,
                                DocumentLineID = string.IsNullOrEmpty(unconsumedTime.DocumentLineID) ? line.DocumentLineID : unconsumedTime.DocumentLineID,
                                EmployeeID = unconsumedTime.EmployeeID,
                                ActivityTypeID = unconsumedTime.ActivityTypeID,
                                SourceUnitPrice = unconsumedTime.SourceUnitPrice,
                                UnitActualValue = unconsumedTime.UnitActualValue,
                                TotalActualValue = unconsumedTime.TotalActualValue,
                                SourceUnitActualValue = unconsumedTime.SourceUnitActualValue,
                                OptionItems = unconsumedTime.OptionItems,
                                OptionBrands = unconsumedTime.OptionBrands,
                                OptionGroups = unconsumedTime.OptionGroups,
                                ExpiryDate = unconsumedTime.ExpiryDate,
                                BranchID = string.IsNullOrEmpty(unconsumedTime.BranchID) ? header.BranchID : unconsumedTime.BranchID,
                                GroupID = string.IsNullOrEmpty(unconsumedTime.GroupID) ? header.GroupID : unconsumedTime.GroupID,
                                FinancialDate = unconsumedTime.FinancialDate == default ? header.FinancialDate : unconsumedTime.FinancialDate,
                                SaveAction = EBI.Enum.EntityState.Added,
                                IsDirty = true
                            };
                            line.lstCashSales_UnconsumedTime.Add(newUnconsumedTime);
                        }
                    }

                    // Fallback to legacy LoadFullPackageAsync if no unconsumed series/time items or credits were copied from the client
                    if ((line.lstCashSales_Series_UnconsumedItem.Count == 0 && line.lstCashSales_UnconsumedTime.Count == 0 && line.lstARAPOutstanding_MemberCredit.Count == 0)
                        && (item.InventoryTypeID == 5 || item.InventoryTypeID == 7 || item.InventoryTypeID == (int)EnumInventoryType.Package || item.InventoryTypeID == (int)EnumInventoryType.TopUp))
                    {
                        var packageDetail = await _inventoryService.LoadFullPackageAsync(item.LineItemID);

                        if (packageDetail?.objInventory != null)
                        {
                            var objInv = packageDetail.objInventory;
                            objInv.lstMembershipCredit = ParseMembershipCredit(objInv.MembershipCredit);
                            if (objInv.lstPackage != null && objInv.lstPackage.Count > 0)
                            {
                                foreach (var pkgItem in objInv.lstPackage)
                                {
                                    var unconsumed = new CashSales_Series_UnconsumedItemDM
                                    {
                                        InventoryID = pkgItem.InventoryID, 
                                        Description = pkgItem.Description,
                                        QuantityPurchased = (int)(pkgItem.Quantity * item.Quantity), 
                                        UnitPrice = (int)pkgItem.UnitPrice,
                                        TotalPrice = (int)(pkgItem.UnitPrice * pkgItem.Quantity * item.Quantity),
                                        ExpiryDate = objInv.ValidityDays > 0 ? now.AddDays(objInv.ValidityDays) : new DateTime(2049, 12, 31), 
                                        UnitActualValue = pkgItem.UnitActualValue,
                                        TotalActualValue = (pkgItem.UnitActualValue * pkgItem.Quantity * item.Quantity),
                                        PackageID = item.LineItemID, 
                                        PackageItemID = pkgItem.AutoID,
                                        CustomerAccountID = header.AccountID,
                                        BranchID = branchId,
                                        GroupID = branchRecord.result.BranchGroupID ?? "",
                                        FinancialDate = now,
                                        SaveAction = EBI.Enum.EntityState.Added,
                                        IsDirty = true
                                    };

                                    if (pkgItem.PackageQuantityTypeID == 0) // Service
                                    {
                                        line.lstCashSales_Series_UnconsumedItem.Add(unconsumed);
                                    }
                                    else if (pkgItem.PackageQuantityTypeID == 1) // Time
                                    {
                                        line.lstCashSales_UnconsumedTime.Add(unconsumed);
                                    }
                                }
                            }

                            if (objInv.lstMembershipCredit != null && objInv.lstMembershipCredit.Count > 0)
                            {
                                foreach (var credit in objInv.lstMembershipCredit)
                                {
                                    var newCredit = new ARAPOutstanding_MemberCreditDM
                                    {
                                        AccountID = header.AccountID,
                                        FinancialDate = header.FinancialDate,
                                        DueDate = objInv.ValidityDays > 0 ? header.FinancialDate.Date.AddDays(objInv.ValidityDays) : new DateTime(2049, 12, 31),
                                        DocumentID = order.objDoc_CashSales?.DocumentID ?? string.Empty,
                                        DisplayCode = header.DisplayCode,
                                        DocumentTypeID = header.DocumentTypeID,
                                        DocumentTypeName = header.FriendlyDocumentName,
                                        DocumentLineID = line.DocumentLineID,
                                        ItemDescription = $"{line.Description}{(line.Quantity == 1m ? "" : $"(x {line.Quantity})")}",
                                        CurrencyID = header.TransactionCurrencyID,
                                        CurrencyName = header.TransactionCurrencyName,
                                        ExchangeRate = header.ExchangeRate,
                                        InterOutletRatio = 1m,
                                        InterOutletAmount = credit.MemberCredit * line.Quantity,
                                        MGMTier = "",
                                        TotalAmount = credit.MemberCredit * line.Quantity,
                                        BranchID = header.BranchID,
                                        GroupID = header.GroupID,
                                        LineItemID = line.LineItemID,
                                        MemberTypeID = credit.MemberTypeID,
                                        SaveAction = EBI.Enum.EntityState.Added,
                                        IsDirty = true
                                    };
                                    line.lstARAPOutstanding_MemberCredit.Add(newCredit);
                                }
                            }
                        }
                    }

                    // Copy lstPackageItems (for bundled/promotion item details)
                    if (item.lstPackageItems != null && item.lstPackageItems.Count > 0)
                    {
                        foreach (var pkgItem in item.lstPackageItems)
                        {
                            var newPkgItem = new CashSales_PromotionItemDetailsDM
                            {
                                DocumentID = string.IsNullOrEmpty(pkgItem.DocumentID) ? (order.objDoc_CashSales?.DocumentID ?? string.Empty) : pkgItem.DocumentID,
                                DocumentLineID = string.IsNullOrEmpty(pkgItem.DocumentLineID) ? line.DocumentLineID : pkgItem.DocumentLineID,
                                ActivityTypeID = pkgItem.ActivityTypeID,
                                PromotionID = pkgItem.PromotionID,
                                PromotionDetailID = pkgItem.PromotionDetailID,
                                HeaderCaption = pkgItem.HeaderCaption,
                                Description = pkgItem.Description,
                                InventoryID = pkgItem.InventoryID,
                                IsVoided = pkgItem.IsVoided,
                                Quantity = pkgItem.Quantity,
                                TotalPrice = pkgItem.TotalPrice,
                                PromotionUnitOriginalPrice = pkgItem.PromotionUnitOriginalPrice,
                                PrinterName = pkgItem.PrinterName,
                                CondimentGroupID = pkgItem.CondimentGroupID,
                                MatrixGroupID = pkgItem.MatrixGroupID,
                                PromotionMethod = pkgItem.PromotionMethod,
                                PromotionAmountFactor = pkgItem.PromotionAmountFactor,
                                Memo = pkgItem.Memo,
                                OptionItems = pkgItem.OptionItems,
                                OptionGroups = pkgItem.OptionGroups,
                                OptionBrands = pkgItem.OptionBrands,
                                IsConfirmed = pkgItem.IsConfirmed,
                                InventoryTypeID = pkgItem.InventoryTypeID,
                                SaveAction = EBI.Enum.EntityState.Added,
                                IsDirty = true
                            };
                            line.lstPackageItems.Add(newPkgItem);
                        }
                    }
                    
                    docLines.Add(line);
                    totalBeforeTax += beforeGst;
                    totalTax += lineTax;
                }

                decimal grandTotalBeforeRounding = totalBeforeTax + totalTax;
                decimal roundedTotal = Math.Round(grandTotalBeforeRounding * 20, MidpointRounding.AwayFromZero) / 20m;
                header.RoundingAmount = roundedTotal - grandTotalBeforeRounding;
                header.TotalBeforeTax = totalBeforeTax;
                header.TaxAmount = totalTax;
                header.TotalAfterTax = roundedTotal;

                decimal amountDue = roundedTotal;
                decimal totalTendered = payments != null ? payments.Sum(p => p.Amount) : 0m;
                decimal changeDue = totalTendered > amountDue ? totalTendered - amountDue : 0m;

                var receiptLines = new ObservableCollection<Doc_CashSales_POSReceiptLinesDM>();
                if (header.DocumentTypeID == 52)
                {
                    if (order?.lstReceiptLines != null)
                    {
                        foreach (var receiptLine in order.lstReceiptLines)
                        {
                            receiptLines.Add(new Doc_CashSales_POSReceiptLinesDM
                            {
                                AccountID = string.IsNullOrEmpty(receiptLine.AccountID) ? header.AccountID : receiptLine.AccountID,
                                AccountTypeID = receiptLine.AccountTypeID == 0 ? 3 : receiptLine.AccountTypeID,
                                Description = receiptLine.Description,
                                POSPaymentTypeID = receiptLine.POSPaymentTypeID,
                                POSReceiptLineAmount = receiptLine.POSReceiptLineAmount,
                                POSReceiptChangeAmount = receiptLine.POSReceiptChangeAmount,
                                FinancialAccountID = receiptLine.FinancialAccountID,
                                BankName = receiptLine.BankName,
                                BranchID = string.IsNullOrEmpty(receiptLine.BranchID) ? (branchId ?? "") : receiptLine.BranchID,
                                GroupID = string.IsNullOrEmpty(receiptLine.GroupID) ? (groupId ?? "") : receiptLine.GroupID,
                                FinancialDate = receiptLine.FinancialDate == default ? now : receiptLine.FinancialDate,
                                ExchangeRate = receiptLine.ExchangeRate == 0 ? 1 : receiptLine.ExchangeRate,
                                CurrencyID = string.IsNullOrEmpty(receiptLine.CurrencyID) ? header.TransactionCurrencyID : receiptLine.CurrencyID,
                                CurrencyName = string.IsNullOrEmpty(receiptLine.CurrencyName) ? header.TransactionCurrencyName : receiptLine.CurrencyName,
                                SaveAction = EBI.Enum.EntityState.Added,
                                IsDirty = true
                            });
                        }
                    }
                }
                else
                {
                    var paymentList = payments != null ? payments.ToList() : new List<PaymentLine>();
                    if (header.TotalAfterTax > 0)
                    {
                        for (int i = 0; i < paymentList.Count; i++)
                        {
                            var p = paymentList[i];
                            bool isLast = i == paymentList.Count - 1;
                            decimal changeOnLine = isLast && changeDue > 0 ? -changeDue : 0m;

                            receiptLines.Add(new Doc_CashSales_POSReceiptLinesDM
                            {
                                AccountID = header.AccountID,
                                AccountTypeID = 3,
                                Description = p.PaymentName,
                                POSPaymentTypeID = p.PaymentTypeId,
                                POSReceiptLineAmount = p.Amount,
                                POSReceiptChangeAmount = changeOnLine,
                                FinancialAccountID = p.FinancialAccountId,
                                BankName = p.BankName,
                                BranchID = branchId,
                                GroupID = groupId,
                                FinancialDate = now,
                                ExchangeRate = 1,
                                CurrencyID = header.TransactionCurrencyID,
                                CurrencyName = header.TransactionCurrencyName,
                                SaveAction = EBI.Enum.EntityState.Added,
                                IsDirty = true
                            });
                        }
                    }
                }

                var request = new EBI.UC.Doc_CashSales
                {
                    objDoc_CashSales = header,
                    lstDocumentLine = docLines,
                    lstReceiptLines = receiptLines
                };

                // Populate root-level collections from lines for API processing
                foreach (var line in docLines)
                {
                    if (line.lstARAPOutstanding_MemberCredit != null)
                    {
                        foreach (var credit in line.lstARAPOutstanding_MemberCredit)
                        {
                            request.lstARAPOutstanding_MemberCredit.Add(credit);
                        }
                    }
                    if (line.lstCashSales_Series_UnconsumedItem != null)
                    {
                        foreach (var unconsumed in line.lstCashSales_Series_UnconsumedItem)
                        {
                            request.lstCashSales_Series_UnconsumedItem.Add(unconsumed);
                        }
                    }
                    if (line.lstCashSales_UnconsumedTime != null)
                    {
                        foreach (var unconsumedTime in line.lstCashSales_UnconsumedTime)
                        {
                            request.lstCashSales_UnconsumedTime.Add(unconsumedTime);
                        }
                    }
                }

                try
                {
                    var debugJson = JsonSerializer.Serialize(request);
                    Console.WriteLine($"[DEBUG] CashSales/Payment API Request Body: {debugJson}");
                }
                catch (Exception debugEx)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize payment request: {debugEx.Message}");
                }

                ApiResponse<CreateCashSalesResponse>? response = null;
                try
                {
                    response = await _ac.CreateCashSalesRecordAsync(request);

                    if (response?.StatusCode == 200 && response.Result != null)
                    {
                        return (true, "Payment Successful", response.Result.DisplayCode, response.Result.Id);
                    }
                }
                catch (Exception netEx)
                {
                    Console.WriteLine($"[CashSalesService] Online API error/timeout: {netEx.Message}. Falling back to SQLite offline storage.");
                }

                // If online payment failed or network is unreachable -> Save offline to SQLite!
                try
                {
                    var offlineEntity = await _offlineStorage.SaveOfflineSaleAsync(request, payments);
                    return (true, "Payment Recorded Offline (Pending Sync)", offlineEntity.LocalDisplayCode, offlineEntity.LocalId);
                }
                catch (Exception offlineEx)
                {
                    return (false, $"Failed to process payment (Online: {response?.Message ?? "Network Error"}, Local: {offlineEx.Message})", null, null);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null, null);
            }
        }

        public async Task<List<Doc_CashSalesDM>> GetSalesHistoryAsync(string branchId, DateTime date)
        {
            var resultList = new List<Doc_CashSalesDM>();

            // 1. Fetch Online Sales
            try
            {
                var startDate = date.Date;
                var endDate = date.Date.AddDays(1).AddTicks(-1);

                var response = await _ac.GetCashSalesAsync(branchId, startDate, endDate);

                if (response?.StatusCode == 200 && response.Result != null)
                {
                    resultList.AddRange(response.Result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CashSalesService] Unable to fetch online sales history: {ex.Message}");
            }

            // 2. Fetch Local Offline Sales and merge into history
            try
            {
                var localSales = await _offlineStorage.GetAllLocalSalesAsync(branchId, date);
                foreach (var local in localSales)
                {
                    bool exists = resultList.Any(x => (!string.IsNullOrEmpty(local.ServerDocumentId) && x.DocumentID == local.ServerDocumentId) || x.DisplayCode == local.LocalDisplayCode);
                    if (!exists)
                    {
                        resultList.Insert(0, new Doc_CashSalesDM
                        {
                            DocumentID = string.IsNullOrEmpty(local.ServerDocumentId) ? local.LocalId : local.ServerDocumentId,
                            DisplayCode = local.Status == Data.Entities.SyncStatus.Synced && !string.IsNullOrEmpty(local.ServerDisplayCode)
                                ? local.ServerDisplayCode
                                : $"{local.LocalDisplayCode} ⚡(Offline)",
                            BranchID = local.BranchId,
                            FinancialDate = local.FinancialDate,
                            AccountName = local.AccountName,
                            TotalAfterTax = local.TotalAmount
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CashSalesService] Unable to fetch local offline sales history: {ex.Message}");
            }

            return resultList;
        }

        public async Task<(bool Success, string Message)> DeleteSaleAsync(string documentId)
        {
            try
            {
                var request = new DeleteCashSalesRequest
                {
                    DocumentID = documentId,
                    Reason = "-", 
                    FinancialDate = default,
                    EndDate = default,
                    RedemptionEndDate = default,
                    NewExpiryDate = default
                };

                var response = await _ac.DeleteCashSalesAsync(request);

                if (response?.StatusCode == 200)
                {
                    return (true, response.Result ?? "Deleted successfully");
                }

                return (false, response?.Message ?? "Delete failed");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(string EInvoiceUrl, string BillUrl)> GenerateInvoiceLinksAsync(string documentId)
        {
            var response = await _ac.LoadRecordAsync(documentId);
            if (response?.StatusCode != 200 || response.Result?.objDoc_CashSales == null)
                return (string.Empty, string.Empty);

            var record = response.Result.objDoc_CashSales;
            string einvoiceUrl = string.Empty;
            string billUrl = string.Empty;

            if (record.eInvoiceStatus == "Valid" && !string.IsNullOrEmpty(record.eInvoiceDocumentUid))
            {
                var branchResponse = await _branchAC.LoadRecordAsync(record.BranchID);

                bool isLive = false;
                if (branchResponse?.statusCode == 200 && branchResponse.result != null)
                {
                    var branch = branchResponse.result;
                    isLive = branch.eInvoiceLiveDate > DateTime.MinValue &&
                             record.FinancialDate >= branch.eInvoiceLiveDate;
                }

                string baseUrl = isLive
                    ? "https://myinvois.hasil.gov.my"
                    : "https://preprod.myinvois.hasil.gov.my";

                einvoiceUrl = $"{baseUrl}/{record.eInvoiceDocumentUid}/share/{record.eInvoiceLongId}";
            }
            else
            {
                var submitResponse = await _ac.RequestEInvoiceDirectSubmitAsync(string.IsNullOrEmpty(record.DocumentID) ? documentId : record.DocumentID);
                if (submitResponse?.StatusCode == 200 && submitResponse.Result != null) einvoiceUrl = submitResponse.Result;
            }

            bool isRedemption = record.DocumentTypeID == 52 || 
                                (response.Result.lstDocumentLine != null && 
                                 response.Result.lstDocumentLine.Any(x => x.ActivityTypeID == 2 || x.ActivityTypeID == 5 || x.ActivityTypeID == 6));

            int docTypeId = isRedemption ? 52 : record.DocumentTypeID;
            string docId = isRedemption ? documentId : (string.IsNullOrEmpty(record.DocumentID) ? documentId : record.DocumentID);
            DateTime finDate = isRedemption ? DateTime.Now : record.FinancialDate;

            var billResponse = await _ac.RequestBillDownloadLinkAsync(docTypeId, docId, finDate);
            billUrl = billResponse?.Result ?? string.Empty;

            return (einvoiceUrl, billUrl);
        }

        public async Task<bool> DownloadReceiptPdfAsync(string documentId)
        {
            try
            {
                int docTypeId = 5; // default to sales
                var recordResponse = await _ac.LoadRecordAsync(documentId);
                if (recordResponse != null && recordResponse.StatusCode == 200 && recordResponse.Result != null)
                {
                    var record = recordResponse.Result;
                    if (record.lstDocumentLine != null && record.lstDocumentLine.Any(line => line.OwnerDocumentTypeID == 52))
                    {
                        docTypeId = 52;
                    }
                }

                var jsonRoot = await _ac.GetThermalReceiptRawAsync(documentId, docTypeId);

                string base64Pdf = string.Empty;

                if (jsonRoot.TryGetProperty("Result", out var outer) || jsonRoot.TryGetProperty("result", out outer))
                {
                    if (outer.ValueKind == JsonValueKind.Object)
                    {
                        if (outer.TryGetProperty("Result", out var inner))
                        {
                            base64Pdf = inner.GetString() ?? "";
                        }
                    }
                    else if (outer.ValueKind == JsonValueKind.String)
                    {
                        base64Pdf = outer.GetString() ?? "";
                    }
                }

                if (!string.IsNullOrEmpty(base64Pdf))
                {
                    var fileName = $"Thermal_Receipt_{documentId}.pdf";
                    await _fileDownloadService.DownloadBinaryFileAsync(fileName, base64Pdf, "application/pdf");

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service Error: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string Message, string? DisplayCode, string? DocumentId)> RedeemPackageAsync(Order order)
        {
            try
            {
                string branchId = _appState.SelectedBranchID;
                if (string.IsNullOrEmpty(branchId))
                {
                    try
                    {
                        branchId = await _js.InvokeAsync<string>("localStorage.getItem", "currentBranch");

                        if (!string.IsNullOrEmpty(branchId))
                        {
                            _appState.SelectedBranchID = branchId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading from localStorage: {ex.Message}");
                    }
                }

                var groupId = _appState.SelectedBranchGroupID;
                var now = DateTime.Now;

                string gstTypeId = string.Empty;
                var branchRecord = await _branchAC.LoadRecordAsync(branchId);
                if (branchRecord?.statusCode == 200 && branchRecord.result != null)
                    gstTypeId = branchRecord.result.TaxTypeID ?? string.Empty;

                var docLines = new ObservableCollection<DocumentLineTableDM>();
                int lineOrder = 1;
                decimal totalTax = 0;
                decimal totalBeforeTax = 0;

                foreach (var item in order.Items)
                {
                    decimal gross = item.Price * item.Quantity;
                    decimal discount = Math.Max(0, item.Discount);
                    if (discount > gross)
                        discount = gross;

                    OrderLineTaxCalculator.ComputeLineAmounts(
                        item.Price,
                        item.Quantity,
                        discount,
                        item.TaxRate,
                        item.IsTaxInclusive,
                        out decimal beforeGst,
                        out decimal lineTax);

                    decimal lineSubTotalIncTax = Math.Round(beforeGst + lineTax, 2, MidpointRounding.AwayFromZero);
                    
                    docLines.Add(new DocumentLineTableDM
                    {
                        LineOrder = lineOrder++,
                        OwnerDocumentTypeID = 52, // Redemption Document Owner Type
                        Description = item.Name,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price,
                        Discount = discount,
                        RefCompanyName = item.Remarks,
                        Memo = item.Remarks,
                        UnitOfMeasurementID = item.UnitOfMeasurementID,
                        SKUName = item.UnitOfMeasurementID,
                        TaxCodeID = item.TaxCodeID ?? string.Empty,
                        GSTTypeID = gstTypeId,
                        TaxPercentage = item.TaxRate,
                        IsTaxInclusive = item.IsTaxInclusive,
                        SubTotal = lineSubTotalIncTax,
                        SubTotalBeforeGST = beforeGst,
                        ConvertedSubTotalBeforeGST = beforeGst,
                        TaxAmount = lineTax,
                        ConvertedTaxAmount = lineTax,
                        ConvertedAmount = lineSubTotalIncTax,
                        ExchangeRate = 1,
                        InventoryTypeID = item.InventoryTypeID,
                        LineItemID = item.Id,
                        LineItemDisplayCode = item.DisplayCode,
                        AccountID = order.Customer?.Id ?? string.Empty,
                        OriginalKitPrice = item.Price,
                        BranchID = branchId,
                        GroupID = groupId,
                        ActivityTypeID = 2, // PackageRedemption
                        SourceDocumentLineID = item.RedemptionSourceID,
                        CurrencyID = branchRecord?.result?.CurrencyID ?? string.Empty,

                        FinancialDate = now,
                    });

                    totalBeforeTax += beforeGst;
                    totalTax += lineTax;
                }

                // (ReceiptLines and obj_DocCashsales is empty for redemption)
                var request = new EBI.UC.Doc_CashSales
                {
                    lstDocumentLine = docLines
                };

                try
                {
                    var debugJson = JsonSerializer.Serialize(request);
                    Console.WriteLine($"[DEBUG] Redemption Package API Request Body: {debugJson}");
                }
                catch (Exception debugEx)
                {
                    Console.WriteLine($"[DEBUG] Failed to serialize redemption request: {debugEx.Message}");
                }

                var response = await _ac.CreateCashSalesRecordAsync(request);

                if (response?.StatusCode == 200 && response.Result != null)
                {
                    return (true, "Redemption Successful", response.Result.DisplayCode, response.Result.Id);
                }

                return (false, response?.Message ?? "Redemption Failed", null, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null, null);
            }
        }
        private static System.Collections.ObjectModel.ObservableCollection<Inventory_MembershipCreditDM> ParseMembershipCredit(string? membershipCreditStr)
        {
            var list = new System.Collections.ObjectModel.ObservableCollection<Inventory_MembershipCreditDM>();
            if (string.IsNullOrWhiteSpace(membershipCreditStr)) return list;
            var rows = membershipCreditStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var row in rows)
            {
                var columns = row.Split(',');
                if (columns.Length >= 2)
                {
                    var memberTypeId = columns[0].Trim();
                    if (decimal.TryParse(columns[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var creditAmount))
                    {
                        list.Add(new Inventory_MembershipCreditDM
                        {
                            MemberTypeID = memberTypeId,
                            MemberCredit = creditAmount,
                            SaveAction = EBI.Enum.EntityState.NotChanged,
                            IsDirty = true
                        });
                    }
                }
            }
            return list;
        }
    }
}
