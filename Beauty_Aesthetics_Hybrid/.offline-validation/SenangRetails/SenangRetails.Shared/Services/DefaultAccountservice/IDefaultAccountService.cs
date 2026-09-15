using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services;

public interface IDefaultAccountService
{
    Task<ApiResponseRoot<CreateResponse>?> CreateRecord(DefaultAccountDM objDM);
    Task<ApiResponseRoot<string>?> UpdateRecord(DefaultAccountDM objDM);
    Task<DefaultAccountDM> LoadRecord(string strBranchID);
    Task<DefaultAccount_CentralisedDM> LoadRecordCentralised();
}
