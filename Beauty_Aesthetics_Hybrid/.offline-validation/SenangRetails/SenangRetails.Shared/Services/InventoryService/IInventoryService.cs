using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services.InventoryService
{
    public interface IInventoryService
    {
        Task<List<InventoryDM>?> LoadItemsAsync(string branchId = "");
        Task<InventoryDM?> LoadItemAsync(string masterAccountId);
        Task<(bool Success, string Message)> CreateItemAsync(Inventory model);
        Task<(bool Success, string Message, string? NewId)> CreatePackageAsync(Inventory model);
        Task<(bool Success, string Message, string? NewId)> SavePackageFullAsync(InventoryFullCreateRequest request);
        Task<(bool Success, string Message)> UpdatePackageFullAsync(InventoryFullCreateRequest request);
        Task<(bool Success, string Message)> UpdateItemAsync(Inventory model);
        Task<(bool Success, string Message)> DeleteItemAsync(string masterAccountId);
        Task<(bool Success, string Message)> UpdateInventoryDirectAsync(InventoryCreateModel model);
        Task<string> UpdateRemarksPascalAsync(string masterAccountId, string accountName, int inventoryTypeID, string? itemGroupId, string remarks, string? unitOfMeasureID = null, string? unitOfMeasureName = null, decimal? pointToRedeem = null, bool? allowPointRedemption = null, decimal? stockReorderLevel = null);
        Task<Dictionary<string, List<InventoryDM>>?> LoadGroupedItemsAsync(string branchID = "");
        Task<Dictionary<string, List<InventoryDM>>?> GetCurrentBranchGroupedItemsAsync();
        Task<Inventory?> LoadFullPackageAsync(string masterAccountId);
        Task<InventoryFullLoadDetail?> LoadFullPackageDetailAsync(string masterAccountId);
        Task<List<LowStockItem>?> GetStockBelowReorderPointAsync();
        Task<Dictionary<string, StockBalanceItem>?> GetStockBalanceByBranchAndByItemAsync(StockBalanceRequest request);
        Task<bool> CreateStockInGRN(EBI.UC.Doc_Stock_GRN request);
        Task<List<InventoryMovement_PendingAcceptDM>?> GetPendingAcceptDocumentByBranchIdAsync();
        Task<List<InventoryMovement_PendingAcceptDM>?> GetPendingAcceptDocumentDetailsAsync(string documentId);
        Task<ApiResponseRoot<string>?> AcceptStockInAsync(string documentId);
    }
}
