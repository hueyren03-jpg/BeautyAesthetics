using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.BarcodeSetupService
{
    public interface IBarcodeSetupService
    {
        Task<List<BarcodeReadingSetupModel>> GetRulesAsync();
        Task<bool> SaveRuleAsync(BarcodeReadingSetupModel rule, bool isNew);
        Task<bool> DeleteRuleAsync(string prefix);
        Task<BarcodeReadingSetupModel> GetSetupAsync();
        Task<bool> SaveSetupAsync(BarcodeReadingSetupModel setup);
        Task<ParsedBarcodeResult> ParseBarcodeAsync(string scannedCode);
    }
}
