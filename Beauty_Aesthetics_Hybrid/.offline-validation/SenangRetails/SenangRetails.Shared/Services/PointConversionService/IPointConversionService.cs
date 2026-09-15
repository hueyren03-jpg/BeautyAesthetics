using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using EBI.DM;

namespace SenangRetails.Shared.Services.PointConversionService
{
    public interface IPointConversionService
    {
        Task<ApiResponseRoot<PointConversionResponse>> CreatePointConversionFormulaAsync(CashSales_PointConversionFormulaDM model);
        Task<List<CashSales_PointConversionFormulaDM>> GetAllPointConversionsAsync();
        Task<ApiResponseRoot<string>> UpdatePointConversionFormulaAsync(CashSales_PointConversionFormulaDM model);
        Task<ApiResponseRoot<string>> DeletePointConversionFormulaAsync(string id);
    }
}

