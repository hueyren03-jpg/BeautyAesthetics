using Beauty_Aesthetics_WebPos.Components.Models.Employee;

namespace Beauty_Aesthetics_WebPos.Components.Services.Employees;

public interface IEmployeeService
{
    Task<EmployeeOperationResult<IReadOnlyList<Employee>>> GetAllEmployeesAsync(
        CancellationToken cancellationToken = default);

    Task<EmployeeOperationResult<Employee>> LoadEmployeeAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<EmployeeOperationResult<Employee>> CreateEmployeeAsync(
        Employee employee,
        CancellationToken cancellationToken = default);

    Task<EmployeeOperationResult<Employee>> UpdateEmployeeAsync(
        Employee employee,
        CancellationToken cancellationToken = default);

    Task<EmployeeOperationResult<Employee>> DeactivateEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken = default);

    Task<EmployeeOperationResult<IReadOnlyList<Employee>>> GetActiveEmployeesByBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default);
}
