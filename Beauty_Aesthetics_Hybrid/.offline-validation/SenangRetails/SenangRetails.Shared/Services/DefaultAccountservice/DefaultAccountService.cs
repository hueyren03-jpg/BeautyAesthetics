using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;
    
namespace SenangRetails.Shared.Services;

public class DefaultAccountService : IDefaultAccountService
{
    private readonly DefaultAccountAC _defaultAccountAC;

    public DefaultAccountService(DefaultAccountAC defaultAccountAC)
    {
        _defaultAccountAC = defaultAccountAC;
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateRecord(DefaultAccountDM objDM)
    {
        return await _defaultAccountAC.CreateRecord(objDM);
    }

    public async Task<DefaultAccountDM> LoadRecord(string strBranchID)
    {
        return await _defaultAccountAC.LoadRecord(strBranchID);
    }

    public async Task<DefaultAccount_CentralisedDM> LoadRecordCentralised()
    {
        return await _defaultAccountAC.LoadRecordCentralised();
    }

    public async Task<ApiResponseRoot<string>?> UpdateRecord(DefaultAccountDM objDM)
    {
        return await _defaultAccountAC.UpdateRecord(objDM);
    }
}