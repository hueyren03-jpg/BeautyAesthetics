using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.ARReceiptService
{
    public interface IARReceiptService
    {
        Task<List<ud_ARAPPaymentOffSetLineDM>> RetrieveSettlementLinesAsync(string customerId, string branchGroupId);
        Task<ApiResponseRoot<object>?> CreateARReceiptRecordAsync(Doc_ARReceipt request);
    }
}
