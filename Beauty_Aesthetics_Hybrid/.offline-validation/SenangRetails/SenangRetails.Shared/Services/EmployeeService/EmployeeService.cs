using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services.EmployeeService;

public class EmployeeService : IEmployeeService
{
    private readonly EmployeeAC _employeeAC;

    public EmployeeService(EmployeeAC employeeAc)
    {
        _employeeAC = employeeAc;
    }

    public async Task<ApiResponseRoot<CreateResponse>?> CreateEmployee(EmployeeDM employee)
    {
        return await _employeeAC.CreateEmployeeAsync(employee);
    }

    public async Task<List<EmployeeDM>> GetActiveEmployeesByBranch(string strBranchID)
    {
        List<EmployeeDM> employees = await _employeeAC.GetActiveEmployeesByBranch(strBranchID);
        return employees ?? new List<EmployeeDM>();
    }

    public async Task<EmployeeDM> LoadRecord(string strID)
    {
        EmployeeDM employee = await _employeeAC.LoadRecord(strID);
        return employee ?? new EmployeeDM();
    }

    public async Task<ApiResponseRoot<string>?> UpdateEmployee(EmployeeDM employee)
    {
        return await _employeeAC.UpdateEmployeeAsync(employee);
    }


}