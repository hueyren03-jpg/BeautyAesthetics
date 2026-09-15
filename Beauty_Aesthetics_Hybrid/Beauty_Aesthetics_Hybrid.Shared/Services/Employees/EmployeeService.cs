using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Models.Employee;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.Components.Services.Employees;

public sealed class EmployeeService : IEmployeeService
{
    private const string NewEmployeeMasterAccountPlaceholder = "string";
    private static readonly DateTime SqlMinDate = new(1753, 1, 1);

    private readonly EmployeeAC employeeAC;
    private readonly Dictionary<string, EmployeeDM> employeeRecords = new(StringComparer.OrdinalIgnoreCase);

    public EmployeeService(EmployeeAC employeeAC)
    {
        this.employeeAC = employeeAC;
    }

    public async Task<EmployeeOperationResult<IReadOnlyList<Employee>>> GetAllEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await employeeAC.GetAllEmployeesAsync(cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return EmployeeOperationResult<IReadOnlyList<Employee>>.Fail(ToEmployeeError(result.ErrorMessage));
        }

        employeeRecords.Clear();

        var employees = result.Value
            .Select(MapAndCache)
            .ToList();

        return EmployeeOperationResult<IReadOnlyList<Employee>>.Ok(employees);
    }

    public async Task<EmployeeOperationResult<Employee>> LoadEmployeeAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return EmployeeOperationResult<Employee>.Fail("Employee ID is required.");
        }

        var result = await employeeAC.LoadRecordAsync(id, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return EmployeeOperationResult<Employee>.Fail(ToEmployeeError(result.ErrorMessage));
        }

        return EmployeeOperationResult<Employee>.Ok(MapAndCache(result.Value));
    }

    public async Task<EmployeeOperationResult<Employee>> CreateEmployeeAsync(
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        var record = FromEmployee(employee, EBI.Enum.EntityState.Added);
        var result = await employeeAC.CreateRecordAsync(record, cancellationToken);

        if (!result.Success)
        {
            return EmployeeOperationResult<Employee>.Fail(ToEmployeeError(result.ErrorMessage));
        }

        return EmployeeOperationResult<Employee>.Ok(MapAndCache(record));
    }

    public async Task<EmployeeOperationResult<Employee>> UpdateEmployeeAsync(
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        var record = employeeRecords.TryGetValue(employee.Code, out var cachedRecord)
            ? cachedRecord
            : FromEmployee(employee, EBI.Enum.EntityState.Changed);

        ApplyEmployee(record, employee, EBI.Enum.EntityState.Changed);
        EnsureSqlSafeDates(record);

        var result = await employeeAC.UpdateRecordAsync(record, cancellationToken);
        if (!result.Success)
        {
            return EmployeeOperationResult<Employee>.Fail(ToEmployeeError(result.ErrorMessage));
        }

        return EmployeeOperationResult<Employee>.Ok(MapAndCache(record));
    }

    public async Task<EmployeeOperationResult<Employee>> DeactivateEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return EmployeeOperationResult<Employee>.Fail("Employee ID is required.");
        }

        EmployeeDM? record;
        if (!employeeRecords.TryGetValue(employeeCode, out record))
        {
            var loadResult = await employeeAC.LoadRecordAsync(employeeCode, cancellationToken);
            if (!loadResult.Success || loadResult.Value is null)
            {
                return EmployeeOperationResult<Employee>.Fail(ToEmployeeError(loadResult.ErrorMessage));
            }

            record = loadResult.Value;
        }

        record.AccountStatus = "Inactive";
        record.IsDirty = true;
        record.SaveAction = EBI.Enum.EntityState.Changed;
        EnsureSqlSafeDates(record);

        var result = await employeeAC.UpdateRecordAsync(record, cancellationToken);
        if (!result.Success)
        {
            return EmployeeOperationResult<Employee>.Fail(ToEmployeeError(result.ErrorMessage));
        }

        return EmployeeOperationResult<Employee>.Ok(MapAndCache(record));
    }

    public async Task<EmployeeOperationResult<IReadOnlyList<Employee>>> GetActiveEmployeesByBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await employeeAC.GetActiveEmployeesByBranchAsync(branchId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return EmployeeOperationResult<IReadOnlyList<Employee>>.Fail(ToEmployeeError(result.ErrorMessage));
        }

        var employees = result.Value
            .Select(MapAndCache)
            .ToList();

        return EmployeeOperationResult<IReadOnlyList<Employee>>.Ok(employees);
    }

    private Employee MapAndCache(EmployeeDM record)
    {
        var employee = ToEmployee(record);
        if (!string.IsNullOrWhiteSpace(employee.Code))
        {
            employeeRecords[employee.Code] = record;
        }

        return employee;
    }

    private static Employee ToEmployee(EmployeeDM record)
    {
        var code = FirstNonEmpty(record.MasterAccountID, record.DisplayCode, record.SalesPersonCode, record.AlphaCode) ?? string.Empty;
        var employeeLevel = FirstNonEmpty(record.EmployeeTypeName, record.EmployeeTypeID) ?? string.Empty;
        var workingShift = FirstNonEmpty(record.WorkingShiftName, record.WorkingShift) ?? string.Empty;
        var allocationGroup = FirstNonEmpty(record.CommissionAutoAllocationGroupName, record.CommissionAutoAllocationGroupID) ?? string.Empty;
        var commissionScheme = FirstNonEmpty(record.CommissionSchemeName, record.CommissionSchemeID) ?? string.Empty;

        return new Employee
        {
            Code = code,
            Name = record.AccountName ?? string.Empty,
            SalesPersonCode = record.SalesPersonCode ?? string.Empty,
            BranchId = record.BranchID ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(record.AccountStatus) ? "Active" : record.AccountStatus,
            IsServiceStaff = record.IsSalesPerson,
            EmployeeLevel = employeeLevel,
            JobTitle = record.JobTitle ?? string.Empty,
            MobilePhone = record.Phone ?? string.Empty,
            Gender = record.Gender ?? string.Empty,
            Nric = record.NRIC ?? string.Empty,
            DateOfBirth = BuildBirthday(record),
            MaxDiscountLimit = record.MaxDiscountLimit,
            WorkingShift = workingShift,
            BasicPay = record.BasicPay,
            DateHired = NormalizeDate(record.DateHired),
            DateResigned = NormalizeDate(record.DateResigned),
            AutoAllocationGroup = allocationGroup,
            CommissionScheme = commissionScheme,
            ImagePath = record.ImagePath ?? string.Empty,
            MaxPurchaseAmount = record.MonthlyPurchaseLimit,
            Remark = record.Remarks ?? string.Empty
        };
    }

    private static EmployeeDM FromEmployee(Employee employee, EBI.Enum.EntityState saveAction)
    {
        var record = new EmployeeDM
        {
            MasterAccountID = NormalizeApiId(employee.Code, saveAction),
            CreatedDateTime = DateTime.UtcNow,
            ModifiedDateTime = DateTime.UtcNow,
            DateHired = SqlMinDate,
            DateResigned = SqlMinDate,
            UpdateTimeStamp = SqlMinDate,
            AppFirstLoginDate = SqlMinDate
        };

        ApplyEmployee(record, employee, saveAction);
        EnsureSqlSafeDates(record);
        return record;
    }

    private static void ApplyEmployee(EmployeeDM record, Employee employee, EBI.Enum.EntityState saveAction)
    {
        record.MasterAccountID = string.IsNullOrWhiteSpace(employee.Code)
            ? NormalizeApiId(record.MasterAccountID, saveAction)
            : employee.Code.Trim();
        record.AccountName = employee.Name;
        record.AccountStatus = string.IsNullOrWhiteSpace(employee.Status) ? "Active" : employee.Status;
        record.SalesPersonCode = employee.SalesPersonCode;
        record.JobTitle = employee.JobTitle;
        record.Gender = employee.Gender;
        record.NRIC = employee.Nric;
        record.Phone = employee.MobilePhone;
        record.MaxDiscountLimit = employee.MaxDiscountLimit;
        record.WorkingShiftName = employee.WorkingShift;
        record.BasicPay = employee.BasicPay;
        record.DateHired = ToSqlDate(employee.DateHired);
        record.DateResigned = ToSqlDate(employee.DateResigned);
        record.ImagePath = employee.ImagePath;
        record.EmployeeTypeName = employee.EmployeeLevel;
        record.CommissionAutoAllocationGroupName = employee.AutoAllocationGroup;
        record.CommissionSchemeName = employee.CommissionScheme;
        record.MonthlyPurchaseLimit = employee.MaxPurchaseAmount;
        record.BranchID = string.IsNullOrWhiteSpace(employee.BranchId) ? record.BranchID : employee.BranchId;
        record.IsSalesPerson = employee.IsServiceStaff || !string.IsNullOrWhiteSpace(employee.SalesPersonCode);
        record.IsNotSalesPerson = !record.IsSalesPerson;
        record.Remarks = employee.Remark;
        record.AccountTypeID = record.AccountTypeID == 0 ? 6 : record.AccountTypeID;
        record.IsDirty = true;
        record.SaveAction = saveAction;
        ApplyBirthday(record, employee.DateOfBirth);
    }

    private static void EnsureSqlSafeDates(EmployeeDM record)
    {
        record.CreatedDateTime = ToSqlDate(record.CreatedDateTime, DateTime.UtcNow);
        record.ModifiedDateTime = DateTime.UtcNow;
        record.DateHired = ToSqlDate(record.DateHired);
        record.DateResigned = ToSqlDate(record.DateResigned);
        record.UpdateTimeStamp = ToSqlDate(record.UpdateTimeStamp);
        record.AppFirstLoginDate = ToSqlDate(record.AppFirstLoginDate);
    }

    private static DateTime? BuildBirthday(EmployeeDM record)
    {
        if (record.BirthdayYear <= 1900 || record.BirthdayMonth is < 1 or > 12 || record.BirthdayDay is < 1 or > 31)
        {
            return null;
        }

        try
        {
            return new DateTime(record.BirthdayYear, record.BirthdayMonth, record.BirthdayDay);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static void ApplyBirthday(EmployeeDM record, DateTime? birthday)
    {
        if (!birthday.HasValue)
        {
            record.BirthdayYear = 0;
            record.BirthdayMonth = 0;
            record.BirthdayDay = 0;
            return;
        }

        record.BirthdayYear = birthday.Value.Year;
        record.BirthdayMonth = birthday.Value.Month;
        record.BirthdayDay = birthday.Value.Day;
    }

    private static DateTime? NormalizeDate(DateTime value)
    {
        return value <= SqlMinDate ? null : value;
    }

    private static DateTime ToSqlDate(DateTime? value)
    {
        return value.HasValue && value.Value >= SqlMinDate
            ? value.Value
            : SqlMinDate;
    }

    private static DateTime ToSqlDate(DateTime value, DateTime fallback)
    {
        return value >= SqlMinDate ? value : fallback;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string NormalizeApiId(string? value, EBI.Enum.EntityState saveAction)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return saveAction == EBI.Enum.EntityState.Added
            ? NewEmployeeMasterAccountPlaceholder
            : string.Empty;
    }

    private static string ToEmployeeError(string? errorMessage)
    {
        return string.IsNullOrWhiteSpace(errorMessage)
            ? "Unable to complete the employee request."
            : errorMessage;
    }
}
