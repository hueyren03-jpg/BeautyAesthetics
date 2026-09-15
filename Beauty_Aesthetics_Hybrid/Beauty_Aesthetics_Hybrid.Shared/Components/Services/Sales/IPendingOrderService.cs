using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public interface IPendingOrderService
{
    event Action? OnOrdersChanged;
    IReadOnlyList<PendingOrder> GetAll();
    PendingOrder? GetById(string id);
    PendingOrder Create(Customer? customer, string branchId, string branchName);
    void SaveOrUpdate(PendingOrder order);
    bool Delete(string id);
    void Clear();
}
