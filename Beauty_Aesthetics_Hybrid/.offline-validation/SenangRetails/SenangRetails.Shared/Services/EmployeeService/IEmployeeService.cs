using EBI.DM;
using SenangRetails.Shared.Models.APIResponse;
using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Services.EmployeeService;

public interface IEmployeeService
{
    Task<List<EmployeeDM>> GetActiveEmployeesByBranch(string strBranchID);
    Task<EmployeeDM> LoadRecord(string strID);
    Task<ApiResponseRoot<CreateResponse>?> CreateEmployee(EmployeeDM employee);
    Task<ApiResponseRoot<string>?> UpdateEmployee(EmployeeDM employee);
}
