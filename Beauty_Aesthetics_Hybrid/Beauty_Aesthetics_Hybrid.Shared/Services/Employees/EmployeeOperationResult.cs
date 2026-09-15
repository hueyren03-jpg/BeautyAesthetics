namespace Beauty_Aesthetics_WebPos.Components.Services.Employees;

public sealed record EmployeeOperationResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    public static EmployeeOperationResult<T> Ok(T value)
    {
        return new EmployeeOperationResult<T>(true, value, null);
    }

    public static EmployeeOperationResult<T> Fail(string message)
    {
        return new EmployeeOperationResult<T>(false, default, message);
    }
}
