namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public sealed record CustomerOperationResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    public static CustomerOperationResult<T> Ok(T value)
    {
        return new CustomerOperationResult<T>(true, value, null);
    }

    public static CustomerOperationResult<T> Fail(string message)
    {
        return new CustomerOperationResult<T>(false, default, message);
    }
}
