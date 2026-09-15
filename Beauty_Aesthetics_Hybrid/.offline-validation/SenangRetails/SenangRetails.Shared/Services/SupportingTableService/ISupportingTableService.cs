using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.SupportingTableService
{
    public interface ISupportingTableService
    {
        /// <summary>Load all supporting table entries for a type. typeId=4 = Item Categories.</summary>
        Task<List<SupportingTableItem>?> LoadListByTypeAsync(int typeId);
        Task<(bool Success, string Message)> CreateAsync(SupportingTableModel model);
        Task<(bool Success, string Message)> UpdateAsync(SupportingTableModel model);
        Task<(bool Success, string Message)> DeleteAsync(string supportingTableId);
    }
}
