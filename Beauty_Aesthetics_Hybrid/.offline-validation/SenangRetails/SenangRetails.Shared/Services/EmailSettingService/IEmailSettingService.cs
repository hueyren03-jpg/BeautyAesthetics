using SenangRetails.Shared.Models.DTOs;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.EmailSettingService
{
    public interface IEmailSettingService
    {
        Task<(EmailSettingDto? item, string message)> GetEmailSettingAsync(string branchId);
        Task<(bool success, string message)> UpdateEmailSettingAsync(EmailSettingDto item);
    }
}
