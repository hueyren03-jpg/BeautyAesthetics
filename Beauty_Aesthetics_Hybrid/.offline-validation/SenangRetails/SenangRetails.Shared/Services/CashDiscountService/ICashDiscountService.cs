using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;
using System.Collections.ObjectModel;

namespace SenangRetails.Shared.Services.CashDiscountService;

public interface ICashDiscountService
{
    Task<ApiResponseRoot<CreateResponse>?> CreateRecord(CashDiscountDM objDiscount);
    Task<ApiResponseRoot<string>?> UpdateRecord(CashDiscountDM objDiscount);
    Task<string> Delete(string strID);
    Task<ObservableCollection<CashDiscountDM>> LoadProxy();
    Task<CashDiscountDM> LoadRecord(string strID);
}
