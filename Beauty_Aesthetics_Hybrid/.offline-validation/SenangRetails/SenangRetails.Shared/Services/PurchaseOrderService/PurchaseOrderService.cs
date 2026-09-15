using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.PurchaseOrderService
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IJSRuntime _js;
        private const string StorageKey = "senang_purchase_orders_data";

        private static readonly List<PurchaseOrderModel> SeedPOs = new()
        {
            new PurchaseOrderModel
            {
                PoNo = "PO-20260812-001",
                Date = DateTime.Today.AddDays(-2),
                Branch = "HQ",
                Vendor = "Acme Distribution Sdn Bhd",
                PoType = "Standard",
                PaymentTerm = "Net 30",
                ExpectedDelivery = DateTime.Today.AddDays(5),
                Status = "Issued",
                Remarks = "Urgent restock for weekend promotion",
                Items = new List<PurchaseOrderItemModel>
                {
                    new PurchaseOrderItemModel { ProductId = "P001", ProductName = "Organic Green Tea 250g", Quantity = 50, UnitCost = 15.50m },
                    new PurchaseOrderItemModel { ProductId = "P002", ProductName = "Premium Coffee Beans 500g", Quantity = 30, UnitCost = 28.00m }
                }
            },
            new PurchaseOrderModel
            {
                PoNo = "PO-20260810-002",
                Date = DateTime.Today.AddDays(-4),
                Branch = "B01",
                Vendor = "Global Logistics & Trade",
                PoType = "Express",
                PaymentTerm = "COD",
                ExpectedDelivery = DateTime.Today.AddDays(2),
                Status = "Pending Delivery",
                Remarks = "Standard shipment via courier",
                Items = new List<PurchaseOrderItemModel>
                {
                    new PurchaseOrderItemModel { ProductId = "P003", ProductName = "Natural Skincare Cream 100ml", Quantity = 20, UnitCost = 45.00m }
                }
            }
        };

        public PurchaseOrderService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<PurchaseOrderModel>> GetPurchaseOrdersAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var items = JsonSerializer.Deserialize<List<PurchaseOrderModel>>(json);
                    if (items != null && items.Any())
                        return items;
                }
            }
            catch
            {
            }
            return SeedPOs.ToList();
        }

        public async Task<PurchaseOrderModel?> GetPurchaseOrderAsync(string poNo)
        {
            var pos = await GetPurchaseOrdersAsync();
            return pos.FirstOrDefault(p => p.PoNo.Equals(poNo, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> SavePurchaseOrderAsync(PurchaseOrderModel po, bool isNew)
        {
            var pos = await GetPurchaseOrdersAsync();

            if (isNew)
            {
                if (pos.Any(p => p.PoNo.Equals(po.PoNo, StringComparison.OrdinalIgnoreCase)))
                    return false; // PO No already exists

                pos.Insert(0, po);
            }
            else
            {
                var index = pos.FindIndex(p => p.PoNo.Equals(po.PoNo, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    pos[index] = po;
                }
                else
                {
                    pos.Insert(0, po);
                }
            }

            return await SaveListAsync(pos);
        }

        public async Task<bool> DeletePurchaseOrderAsync(string poNo)
        {
            var pos = await GetPurchaseOrdersAsync();
            pos.RemoveAll(p => p.PoNo.Equals(poNo, StringComparison.OrdinalIgnoreCase));
            return await SaveListAsync(pos);
        }

        private async Task<bool> SaveListAsync(List<PurchaseOrderModel> list)
        {
            try
            {
                var json = JsonSerializer.Serialize(list);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
