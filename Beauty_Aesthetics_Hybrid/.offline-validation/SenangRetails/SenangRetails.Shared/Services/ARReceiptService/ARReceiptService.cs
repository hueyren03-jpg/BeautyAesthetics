using EBI.DM;
using EBI.UC;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.ARReceiptService
{
    public class ARReceiptService : IARReceiptService
    {
        private readonly ARReceiptAC _ac;

        public ARReceiptService(ARReceiptAC ac)
        {
            _ac = ac;
        }

        public async Task<List<ud_ARAPPaymentOffSetLineDM>> RetrieveSettlementLinesAsync(string customerId, string branchGroupId)
        {
            var response = await _ac.RetrieveSettlementLinesAsync(customerId, branchGroupId);
            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }
            return new List<ud_ARAPPaymentOffSetLineDM>();
        }

        public async Task<ApiResponseRoot<object>?> CreateARReceiptRecordAsync(Doc_ARReceipt request)
        {
            return await _ac.CreateARReceiptRecordAsync(request);
        }
    }
}
