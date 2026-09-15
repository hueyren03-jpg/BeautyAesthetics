using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using EBI.DM;

namespace SenangRetails.Shared.Services.PointConversionService
{
    public class PointConversionService : IPointConversionService
    {
        private readonly PointConversionAC _pointAC;

        public PointConversionService(PointConversionAC pointAC)
        {
            _pointAC = pointAC;
        }

        public async Task<ApiResponseRoot<PointConversionResponse>> CreatePointConversionFormulaAsync(CashSales_PointConversionFormulaDM model)
        {
            try
            {
                var response = await _pointAC.CreatePointConversionFormulaAsync(model);

                if (response != null)
                {
                    return response;
                }

                return new ApiResponseRoot<PointConversionResponse>
                {
                    statusCode = 500,
                    message = "API returned an empty response."
                };
            }
            catch (Exception ex)
            {
                return new ApiResponseRoot<PointConversionResponse>
                {
                    statusCode = 500,
                    message = ex.Message
                };
            }
        }

        public async Task<List<CashSales_PointConversionFormulaDM>> GetAllPointConversionsAsync()
        {
            try
            {
                var response = await _pointAC.GetPointConversionFormulaAsync();

                if (response != null && response.StatusCode == 200 && response.Result != null)
                {
                    return response.Result;
                }

                return new List<CashSales_PointConversionFormulaDM>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching point rules: {ex.Message}");
                return new List<CashSales_PointConversionFormulaDM>();
            }
        }

        public async Task<ApiResponseRoot<string>> UpdatePointConversionFormulaAsync(CashSales_PointConversionFormulaDM model)
        {
            try
            {
                var response = await _pointAC.UpdatePointConversionFormulaAsync(model);

                if (response != null)
                {
                    return response;
                }

                return new ApiResponseRoot<string>
                {
                    statusCode = 500,
                    message = "API returned an empty response."
                };
            }
            catch (Exception ex)
            {
                return new ApiResponseRoot<string>
                {
                    statusCode = 500,
                    message = ex.Message
                };
            }
        }

        public async Task<ApiResponseRoot<string>> DeletePointConversionFormulaAsync(string id)
        {
            try
            {
                var response = await _pointAC.DeletePointConversionFormulaAsync(id);

                if (response != null)
                {
                    return response;
                }

                return new ApiResponseRoot<string>
                {
                    statusCode = 500,
                    message = "API returned an empty response during deletion."
                };
            }
            catch (Exception ex)
            {
                return new ApiResponseRoot<string>
                {
                    statusCode = 500,
                    message = ex.Message
                };
            }
        }
    }
}

