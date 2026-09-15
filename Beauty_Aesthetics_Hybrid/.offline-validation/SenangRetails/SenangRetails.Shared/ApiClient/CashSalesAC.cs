using EBI.UC;
using EBI.DM;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.AuthService;
using System.Text.Json;

namespace SenangRetails.Shared.ApiClient
{
    public class CashSalesAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public CashSalesAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }
        public async Task<ApiResponse<CreateCashSalesResponse>?> CreateCashSalesRecordAsync(Doc_CashSales request)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            
            return await PostAsync<Doc_CashSales, ApiResponse<CreateCashSalesResponse>>(
                "api/Doc_CashSales/CreateRecord", request);
        }

        public async Task<ApiResponse<List<Doc_CashSalesDM>>?> GetCashSalesAsync(
            string id,
            DateTime startDate,
            DateTime endDate)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);

            var request = new CashSalesRequest
            {
                Id = id,
                StartDate = startDate,
                EndDate = endDate
            };

            return await PostAsync<CashSalesRequest, ApiResponse<List<Doc_CashSalesDM>>>(
                "api/Doc_CashSales/GetAppSalesList", request);
        }

        public async Task<ApiResponse<string>?> DeleteCashSalesAsync(DeleteCashSalesRequest request)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);

            return await PostAsync<DeleteCashSalesRequest, ApiResponse<string>>(
                "api/Doc_CashSales/DeleteDM", request);
        }

        public async Task<ApiResponse<Doc_CashSales>?> LoadRecordAsync(string documentId)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token)) CreateBearerAuthAsync(token);

            var request = new LoadWithIdReq { Id = documentId };
            return await PostAsync<LoadWithIdReq, ApiResponse<Doc_CashSales>>(
                "api/Doc_CashSales/LoadRecord", request);
        }

        public async Task<ApiResponse<string>?> RequestBillDownloadLinkAsync(int docTypeId, string docId, DateTime date)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token)) CreateBearerAuthAsync(token);

            var request = new RequestBillDownloadLinkRequest
            {
                DocumentTypeID = docTypeId,
                DocumentID = docId,
                FinancialDate = date
            };

            var payloadJson = JsonSerializer.Serialize(request);
            Console.WriteLine($"[DEBUG] RequestBillDownloadLink payload: {payloadJson}");

            return await PostAsync<RequestBillDownloadLinkRequest, ApiResponse<string>>(
                "api/Doc_CashSales/RequestBillDownloadLink", request);
        }

        public async Task<ApiResponse<string>?> RequestEInvoiceDirectSubmitAsync(string id)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token)) CreateBearerAuthAsync(token);

            var request = new LoadWithIdReq { Id = id };

            return await PostAsync<LoadWithIdReq, ApiResponse<string>>(
                "api/Doc_CashSales/RequestEInvoice_DirectSubmitMethod", request);
        }

        public async Task<JsonElement> GetThermalReceiptRawAsync(string id, int documentTypeId)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token)) CreateBearerAuthAsync(token);

            var request = new ThermalReceiptPOSRequest { Id = id, IntDocumentTypeID = documentTypeId };

            return await PostAsync<ThermalReceiptPOSRequest, JsonElement>(
                "POSReceipt_ThermalPOS", request);
        }
    }
}
