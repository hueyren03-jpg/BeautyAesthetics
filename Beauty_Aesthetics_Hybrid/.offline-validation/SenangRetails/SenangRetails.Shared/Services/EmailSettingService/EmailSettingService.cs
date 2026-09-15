using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.EmailSettingService
{
    public class EmailSettingService : IEmailSettingService
    {
        private readonly SettingAC _ac;

        public EmailSettingService(SettingAC ac)
        {
            _ac = ac;
        }

        public async Task<(EmailSettingDto? item, string message)> GetEmailSettingAsync(string branchId)
        {
            var response = await _ac.GetEmailSettingAsync(branchId);
            if (response == null) return (null, "No response from server.");
            if (response.statusCode == 200 && response.result != null) return (response.result, "");
            return (null, $"Status {response.statusCode}: {response.message ?? "Unknown error."}");
        }

        public async Task<(bool success, string message)> UpdateEmailSettingAsync(EmailSettingDto item)
        {
            var response = await _ac.UpdateEmailSettingAsync(item);
            if (response == null) return (false, "No response from server.");
            return (response.statusCode == 200, response.message ?? "Unknown error.");
        }
    }
}
