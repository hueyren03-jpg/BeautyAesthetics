using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class POSPaymentLineTypeAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public POSPaymentLineTypeAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<List<Doc_CashSales_POSPaymentLineTypeDM>>?> GetSystemControlledSalesSettlementTypeAsync(POSPaymentLineTypeRequest request)
        {
            if (!await SetBearerToken()) return null;

            return await PostAsync<object, ApiResponseRoot<List<Doc_CashSales_POSPaymentLineTypeDM>>>(
                "api/Doc_CashSales_POSPaymentLineType/GetSystemControlledSalesSettlementType",
                request);
        }

        public async Task<ApiResponseRoot<object>?> SaveRecordAsync(
            int posPaymentTypeID,
            string posPaymentTypeName,
            string grouping,
            string visibleInBranch,
            string visibleInGroup,
            string merchantCode,
            string merchantKey,
            string terminalID,
            bool active,
            int paymentIntegrationType = 0)
        {
            if (!await SetBearerToken()) return null;

            var saveAction = posPaymentTypeID == 0 ? "Added" : "Changed";

            return await PostAsync<object, ApiResponseRoot<object>>(
                "api/Doc_CashSales_POSPaymentLineType/CreateRecord",
                new
                {
                    isLoading = true,
                    posPaymentTypeID,
                    posPaymentTypeName,
                    grouping,
                    sorting = 0,
                    receiptGroup = "",
                    financialAccountID = "",
                    arapOutstandingTypeID = 0,
                    visibleInModules = "Sales",
                    visibleInBranch,
                    visibleInGroup,
                    active,
                    financialAccountName = "",
                    picturePath = "",
                    paymentSubGroup = "",
                    balance = "",
                    showBalance = true,
                    isOpenCashDrawer = false,
                    paymentIntegrationType,
                    comPortNo = "",
                    isReferenceNoCompulsory = false,
                    amountClaimMultiplier = 0,
                    pointDeductionRatio = 0,
                    exportToDocumentType = "",
                    exportToAccountCode = "",
                    merchantCode,
                    merchantKey,
                    terminalID,
                    saveAction,
                    isDirty = true
                });
        }

        public async Task<ApiResponseRoot<string>?> DeleteRecordAsync(int posPaymentTypeID)
        {
            if (!await SetBearerToken()) return null;

            return await DeleteAsync<ApiResponseRoot<string>>(
                $"api/Doc_CashSales_POSPaymentLineType/Delete?id={posPaymentTypeID}");
        }
    }
}
