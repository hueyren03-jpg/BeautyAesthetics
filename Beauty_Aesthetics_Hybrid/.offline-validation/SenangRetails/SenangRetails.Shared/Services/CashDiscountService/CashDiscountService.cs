using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;
using System.Collections.ObjectModel;

namespace SenangRetails.Shared.Services.CashDiscountService;

public class CashDiscountService : ICashDiscountService
{
    private readonly CashDiscountAC _ac;

    public CashDiscountService(CashDiscountAC ac)
    {
        _ac = ac;
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateRecord(CashDiscountDM objDiscount)
    {
        return await _ac.CreateAsync(objDiscount);
    }

    public async Task<string> Delete(string strID)
    {
        var response = await _ac.DeleteAsync(strID);
        return "ok";
    }

    public async Task<ObservableCollection<CashDiscountDM>> LoadProxy()
    {
        return await _ac.LoadProxy();
    }

    public async Task<CashDiscountDM> LoadRecord(string strID)
    {
        return await _ac.LoadRecord(strID);
    }

    public async Task<ApiResponseRoot<string>?> UpdateRecord(CashDiscountDM objDiscount)
    {
        return await _ac.UpdateAsync(objDiscount);
    }
}
