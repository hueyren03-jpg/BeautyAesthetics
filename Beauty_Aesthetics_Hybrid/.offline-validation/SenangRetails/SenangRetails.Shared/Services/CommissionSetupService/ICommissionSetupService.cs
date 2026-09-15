using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.CommissionSetupService
{
    public interface ICommissionSetupService
    {
        Task<List<CommissionSchemeModel>> GetSchemesAsync();
        Task<CommissionSchemeModel?> GetSchemeByIdAsync(string id);
        Task<bool> SaveSchemeAsync(CommissionSchemeModel scheme);
        Task<bool> DeleteSchemeAsync(string id);
        Task<(decimal amount, string type)> GetCommissionForStaffAsync(string? schemeId, string inEvent, decimal unitPrice);
    }
}
