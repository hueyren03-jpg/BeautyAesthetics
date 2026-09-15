using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services;

public class GSTTaxCodeService : IGSTTaxCodeService
{
    private readonly GSTTaxCodeAC _gSTTaxCodeAC;

    public GSTTaxCodeService(GSTTaxCodeAC gSTTaxCodeAC)
    {
        _gSTTaxCodeAC = gSTTaxCodeAC;
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateRecord(GSTTaxCodeDM taxcode)
    {
        return await _gSTTaxCodeAC.CreateRecord(taxcode);
    }

    public async Task<ApiResponseRoot<string>?> Delete(string strID)
    {
        return await _gSTTaxCodeAC.Delete(strID);
    }

    public async Task<ApiResponseRoot<string>?> DeleteByParentID(string strID)
    {
        return await _gSTTaxCodeAC.DeleteByParentID(strID);
    }

    public async Task<Dictionary<string, GSTTaxCodeDM>> LoadDictionaryByParentID(string strTaxTypeID)
    {
        Dictionary<string, GSTTaxCodeDM> lst = await _gSTTaxCodeAC.LoadDictionaryByParentID(strTaxTypeID);
        return lst ?? new Dictionary<string, GSTTaxCodeDM>();
    }

    public async Task<ApiResponseRoot<string>?> UpdateRecord(GSTTaxCodeDM taxcode)
    {
        return await _gSTTaxCodeAC.UpdateRecord(taxcode);
    }
}
