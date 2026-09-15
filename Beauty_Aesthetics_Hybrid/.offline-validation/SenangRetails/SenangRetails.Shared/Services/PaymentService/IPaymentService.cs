using SenangRetails.Shared.Models.DTOs;
using EBI.DM;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.PaymentService
{
    public interface IPaymentService
    {
        Task<List<Doc_CashSales_POSPaymentLineTypeDM>> GetPaymentMethodsAsync(string branchId, string groupId, string customerId, bool includeInactive = false);
    }
}
