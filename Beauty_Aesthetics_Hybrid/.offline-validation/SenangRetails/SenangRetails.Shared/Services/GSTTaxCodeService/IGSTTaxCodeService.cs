using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services;

public interface IGSTTaxCodeService
{
    Task<ApiResponseRoot<CreateResponse>?> CreateRecord(GSTTaxCodeDM taxcode);
    Task<ApiResponseRoot<string>?> UpdateRecord(GSTTaxCodeDM taxcode);
    Task<ApiResponseRoot<string>?> Delete(string strID);
    Task<ApiResponseRoot<string>?> DeleteByParentID(string strID);
    Task<Dictionary<string, GSTTaxCodeDM>> LoadDictionaryByParentID(string strTaxTypeID);
}
