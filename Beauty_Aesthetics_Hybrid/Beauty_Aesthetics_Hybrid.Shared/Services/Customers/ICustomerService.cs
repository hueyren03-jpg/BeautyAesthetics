using Beauty_Aesthetics_WebPos.Components.Models;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public interface ICustomerService
{
    Task<CustomerOperationResult<IReadOnlyList<Customer>>> SearchCustomersAsync(
        string keyword = "",
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<Customer>> LoadCustomerAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<CustomerBalanceSnapshot>> GetBalanceSnapshotAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<Customer>> CreateCustomerAsync(
        Customer customer,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<Customer>> UpdateCustomerAsync(
        Customer customer,
        CancellationToken cancellationToken = default);

    Task<CustomerOperationResult<Customer>> DeactivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
