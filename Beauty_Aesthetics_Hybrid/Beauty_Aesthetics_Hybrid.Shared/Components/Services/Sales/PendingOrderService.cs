using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public sealed class PendingOrderService : IPendingOrderService
{
    private readonly List<PendingOrder> _orders = new();
    private readonly object _lock = new();
    private int _sequenceNumber = 1;

    public event Action? OnOrdersChanged;

    public IReadOnlyList<PendingOrder> GetAll()
    {
        lock (_lock)
        {
            return _orders.Select(o => o.Clone()).OrderByDescending(o => o.UpdatedAt).ToList();
        }
    }

    public PendingOrder? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        lock (_lock)
        {
            return _orders.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase))?.Clone();
        }
    }

    public PendingOrder Create(Customer? customer, string branchId, string branchName)
    {
        lock (_lock)
        {
            var orderNumber = $"HOLD-{DateTime.Now:yyyyMMdd}-{_sequenceNumber:D3}";
            _sequenceNumber++;

            var order = new PendingOrder
            {
                Id = Guid.NewGuid().ToString("N"),
                OrderNumber = orderNumber,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                AccountId = customer?.SystemID ?? string.Empty,
                CustomerName = customer != null ? $"{customer.FirstName} {customer.LastName}".Trim() : "Walk-in Customer",
                CustomerContact = customer?.ContactNumber1 ?? string.Empty,
                CustomerEmail = customer?.Email ?? string.Empty,
                MembershipType = customer?.MembershipType ?? "Standard",
                BranchId = branchId ?? string.Empty,
                BranchName = branchName ?? string.Empty,
                Items = new List<TransactionItem>(),
                Payments = new List<TransactionPayment>()
            };

            _orders.Add(order);
            NotifyChanged();
            return order.Clone();
        }
    }

    public void SaveOrUpdate(PendingOrder order)
    {
        if (order == null) return;

        lock (_lock)
        {
            var existingIndex = _orders.FindIndex(o => string.Equals(o.Id, order.Id, StringComparison.OrdinalIgnoreCase));
            var cloned = order.Clone();
            cloned.UpdatedAt = DateTime.Now;

            if (existingIndex >= 0)
            {
                _orders[existingIndex] = cloned;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(cloned.OrderNumber))
                {
                    cloned.OrderNumber = $"HOLD-{DateTime.Now:yyyyMMdd}-{_sequenceNumber:D3}";
                    _sequenceNumber++;
                }
                _orders.Add(cloned);
            }

            NotifyChanged();
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        lock (_lock)
        {
            var index = _orders.FindIndex(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _orders.RemoveAt(index);
                NotifyChanged();
                return true;
            }
            return false;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _orders.Clear();
            NotifyChanged();
        }
    }

    private void NotifyChanged()
    {
        try
        {
            OnOrdersChanged?.Invoke();
        }
        catch
        {
        }
    }
}
