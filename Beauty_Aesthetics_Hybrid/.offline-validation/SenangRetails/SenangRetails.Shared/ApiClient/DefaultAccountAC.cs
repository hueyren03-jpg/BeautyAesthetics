using EBI.DM;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient;

public class DefaultAccountAC : BaseAC
{
    private readonly IStoreTokenService _tokenService;

    public DefaultAccountAC(IStoreTokenService tokenService) : base()
    {
        _tokenService = tokenService;
    }

    private async Task<bool> SetBearerToken()
    {
        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;
        return CreateBearerAuthAsync(token);
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateRecord(DefaultAccountDM objDM)
    {
        if (!await SetBearerToken()) return null;

        return await PostAsync<DefaultAccountDM, ApiResponseRoot<CreateResponse>>(
            "api/DefaultAccount/CreateRecord",
            objDM);
    }

    public async Task<ApiResponseRoot<string>?> UpdateRecord(DefaultAccountDM objDM)
    {
        if (!await SetBearerToken()) return null;

        return await PostAsync<DefaultAccountDM, ApiResponseRoot<string>>(
            "api/DefaultAccount/UpdateRecord",
            objDM);
    }

    public async Task<DefaultAccountDM> LoadRecord(string strBranchID)
    {
        if (!await SetBearerToken()) return new DefaultAccountDM();

        var apiResponse = await PostAsync<object, ApiResponseRoot<DefaultAccountDM>>(
            "api/DefaultAccount/LoadRecord",
            new { id = strBranchID });

        return apiResponse?.result ?? new DefaultAccountDM();
    }

    public async Task<DefaultAccount_CentralisedDM> LoadRecordCentralised()
    {
        if (!await SetBearerToken()) return new DefaultAccount_CentralisedDM();

        var apiResponse = await PostAsync<object, ApiResponseRoot<DefaultAccount_CentralisedDM>>(
            "api/DefaultAccount/LoadRecordCentralised",
            null);

        return apiResponse?.result ?? new DefaultAccount_CentralisedDM();
    }

}
