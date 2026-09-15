using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient;

public class EmployeeAC : BaseAC
{
    private readonly IStoreTokenService _tokenService;

    public EmployeeAC(IStoreTokenService tokenService) : base()
    {
        _tokenService = tokenService;
    }

    private async Task<bool> SetBearerToken()
    {
        var token = await _tokenService.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;
        return CreateBearerAuthAsync(token);
    }

    public async Task<List<EmployeeDM>> GetActiveEmployeesByBranch(string strBranchID)
    {
        if (!await SetBearerToken()) return new List<EmployeeDM>();

        var apiResponse = await PostAsync<object, ApiResponseRoot<List<EmployeeDM>>>(
            "api/Employee/GetActiveEmployeesByBranch",
            new { Id = strBranchID });
        return apiResponse?.result ?? new List<EmployeeDM>();
    }

    public async Task<EmployeeDM> LoadRecord(string strID)
    {
        if (!await SetBearerToken()) return new EmployeeDM();

        var apiResponse = await PostAsync<object, ApiResponseRoot<EmployeeDM>>(
            "api/Employee/LoadRecord",
            new { Id = strID });
        return apiResponse?.result ?? new EmployeeDM();
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateEmployeeAsync(EmployeeDM request)
    {
        if (!await SetBearerToken()) return null;

        return await PostAsync<EmployeeDM, ApiResponseRoot<CreateResponse>>(
            "api/Employee/CreateRecord",
            request);
    }

    public async Task<ApiResponseRoot<string>?> UpdateEmployeeAsync(EmployeeDM request)
    {
        if (!await SetBearerToken()) return null;

        return await PutAsync<EmployeeDM, ApiResponseRoot<string>>(
            "api/Employee/UpdateRecord",
            request);
    }
}
