using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services;
using Beauty_Aesthetics_WebPos.Components.Services.Customers;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

/// <summary>
/// Keeps every page in an appointment's clinical workflow attached to the same
/// customer record. The customer is always refreshed through Customer/LoadRecord.
/// </summary>
public sealed class ClinicalPatientContext(
    ICustomerService customerService,
    AppState appState)
{
    public Customer? CurrentCustomer => appState.SelectedCustomer;

    public string? CustomerId => appState.SelectedCustomer?.SystemID?.Trim();

    public async Task<CustomerOperationResult<Customer>> LoadAsync(
        string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var requestedCustomerId = !string.IsNullOrWhiteSpace(customerId)
            ? customerId.Trim()
            : CustomerId;

        if (string.IsNullOrWhiteSpace(requestedCustomerId))
        {
            appState.ClearSelectedCustomer();
            return CustomerOperationResult<Customer>.Fail(
                "Open this page from an appointment that has a customer.");
        }

        var result = await customerService.LoadCustomerAsync(requestedCustomerId, cancellationToken);
        if (result.Success && result.Value is not null)
        {
            appState.SelectCustomer(result.Value);
        }
        else
        {
            appState.ClearSelectedCustomer();
        }

        return result;
    }

    public string WithCustomer(string route)
    {
        var customerId = CustomerId;
        if (string.IsNullOrWhiteSpace(customerId) ||
            route.Contains("customerId=", StringComparison.OrdinalIgnoreCase))
        {
            return route;
        }

        var separator = route.Contains('?') ? '&' : '?';
        return $"{route}{separator}customerId={Uri.EscapeDataString(customerId)}";
    }

    public static string GetDisplayName(Customer customer)
    {
        var name = $"{customer.FirstName} {customer.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(name)) return name;
        if (!string.IsNullOrWhiteSpace(customer.ExternalCode)) return customer.ExternalCode;
        if (!string.IsNullOrWhiteSpace(customer.IdentificationNumber)) return customer.IdentificationNumber;
        return customer.SystemID ?? "Patient";
    }
}
