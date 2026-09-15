using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Text;
using EBI.DM;

namespace SenangRetails.Shared.ApiClient
{
    public class PointConversionAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public PointConversionAC(IStoreTokenService tokenService) : base()
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<PointConversionResponse>> CreatePointConversionFormulaAsync(CashSales_PointConversionFormulaDM requestBody)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            return await PostAsync<CashSales_PointConversionFormulaDM, ApiResponseRoot<PointConversionResponse>>("api/CashSales_PointConversionFormula/CreateRecord", requestBody);
        }

        public async Task<ApiResponse<List<CashSales_PointConversionFormulaDM>>?> GetPointConversionFormulaAsync()
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);
            return await PostAsync<object, ApiResponse<List<CashSales_PointConversionFormulaDM>>>("api/CashSales_PointConversionFormula/LoadProxy", new { });
        }

        public async Task<ApiResponseRoot<string>> UpdatePointConversionFormulaAsync(CashSales_PointConversionFormulaDM requestBody)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);

            var result = await PutAsync<CashSales_PointConversionFormulaDM, ApiResponseRoot<string>>(
                "api/CashSales_PointConversionFormula/UpdateRecord", requestBody);

            return result ?? throw new HttpRequestException("API returned null response during update");
        }

        public async Task<ApiResponseRoot<string>> DeletePointConversionFormulaAsync(string id)
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                CreateBearerAuthAsync(token);

            string endpoint = $"api/CashSales_PointConversionFormula/Delete?id={id}";

            var result = await DeleteAsync<ApiResponseRoot<string>>(endpoint);

            return result ?? throw new HttpRequestException("API returned null response during deletion");
        }
    }
}

