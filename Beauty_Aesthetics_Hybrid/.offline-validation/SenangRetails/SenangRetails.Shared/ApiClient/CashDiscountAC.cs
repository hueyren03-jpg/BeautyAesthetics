using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System.Collections.ObjectModel;

namespace SenangRetails.Shared.ApiClient;

public class CashDiscountAC : BaseAC
{
    private readonly IStoreTokenService _tokenService;

    public CashDiscountAC(IStoreTokenService tokenService) : base()
    {
        _tokenService = tokenService;
    }

    private async Task<bool> SetBearerToken()
    {
        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;
        return CreateBearerAuthAsync(token);
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateAsync(CashDiscountDM request)
    {
        if (!await SetBearerToken()) return null;

        return await PostAsync<CashDiscountDM, ApiResponseRoot<CreateResponse>>(
            "api/CashDiscount/CreateRecord",
            request);
    }

    public async Task<ApiResponseRoot<string>?> UpdateAsync(CashDiscountDM request)
    {
        if (!await SetBearerToken()) return null;

        return await PutAsync<CashDiscountDM, ApiResponseRoot<string>>(
            "api/CashDiscount/UpdateRecord",
            request);
    }

    public async Task<CashDiscountDM> LoadRecord(string strID)
    {
        if (!await SetBearerToken()) return new CashDiscountDM();

        var apiResponse = await PostAsync<object, ApiResponseRoot<CashDiscountDM>>(
            "api/CashDiscount/LoadRecord",
            new { Id = strID });
        return apiResponse?.result ?? new CashDiscountDM();
    }

    public async Task<ObservableCollection<CashDiscountDM>> LoadProxy()
    {
        if (!await SetBearerToken()) return new ObservableCollection<CashDiscountDM>();

        var apiResponse = await GetAsync<ApiResponseRoot<ObservableCollection<CashDiscountDM>>>(
            "api/CashDiscount/LoadProxy");
        return apiResponse?.result ?? new ObservableCollection<CashDiscountDM>();
    }

    public async Task<ApiResponseRoot<object>?> DeleteAsync(string strID)
    {
        if (!await SetBearerToken()) return null;
        return await DeleteAsync<ApiResponseRoot<object>>(
            $"api/GSTTaxCode/Delete?id={strID}");
    }

}

