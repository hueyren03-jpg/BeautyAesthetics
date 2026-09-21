using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using EBI.DM;

namespace Beauty_Aesthetics_WebPos.Components.Services.Customers;

public sealed class CustomerService : ICustomerService
{
    private const string NewCustomerMasterAccountPlaceholder = "string";
    private static readonly DateTime SqlMinDate = new(1753, 1, 1);

    private readonly CustomerAC customerAC;
    private readonly AppFeedbackService feedback;
    private readonly Dictionary<Guid, CustomerDM> customerRecords = new();

    public CustomerService(CustomerAC customerAC, AppFeedbackService feedback)
    {
        this.customerAC = customerAC;
        this.feedback = feedback;
    }

    public async Task<CustomerOperationResult<IReadOnlyList<Customer>>> SearchCustomersAsync(
        string keyword = "",
        CancellationToken cancellationToken = default)
    {
        var result = await customerAC.SearchByWordAsync(keyword ?? string.Empty, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<IReadOnlyList<Customer>>.Fail(ToCustomerError(result.ErrorMessage));
        }

        customerRecords.Clear();

        var customers = result.Value
            .Select(MapAndCache)
            .ToList();

        return CustomerOperationResult<IReadOnlyList<Customer>>.Ok(customers);
    }

    public async Task<CustomerOperationResult<Customer>> LoadCustomerAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return CustomerOperationResult<Customer>.Fail("Customer ID is required.");
        }

        var result = await customerAC.LoadRecordAsync(id, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return CustomerOperationResult<Customer>.Fail(ToCustomerError(result.ErrorMessage));
        }

        return CustomerOperationResult<Customer>.Ok(MapAndCache(result.Value));
    }

    public async Task<CustomerOperationResult<CustomerBalanceSnapshot>> GetBalanceSnapshotAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return CustomerOperationResult<CustomerBalanceSnapshot>.Fail("Customer ID is required.");
        }

        var summaryResult = await customerAC.GetMemberBalanceSummaryAsync(customerId, cancellationToken);
        if (!summaryResult.Success || summaryResult.Value is null)
        {
            return CustomerOperationResult<CustomerBalanceSnapshot>.Fail(ToCustomerError(summaryResult.ErrorMessage));
        }

        var packageResult = await customerAC.GetPackageBalanceDetailsAsync(customerId, cancellationToken);
        if (!packageResult.Success || packageResult.Value is null)
        {
            return CustomerOperationResult<CustomerBalanceSnapshot>.Fail(ToCustomerError(packageResult.ErrorMessage));
        }

        var creditResult = await customerAC.GetCreditBalanceDetailsAsync(customerId, cancellationToken);
        if (!creditResult.Success || creditResult.Value is null)
        {
            return CustomerOperationResult<CustomerBalanceSnapshot>.Fail(ToCustomerError(creditResult.ErrorMessage));
        }

        return CustomerOperationResult<CustomerBalanceSnapshot>.Ok(new CustomerBalanceSnapshot
        {
            Summary = summaryResult.Value,
            Packages = packageResult.Value,
            Credits = creditResult.Value
        });
    }
    public async Task<CustomerOperationResult<Customer>> CreateCustomerAsync(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var record = FromCustomer(customer, EBI.Enum.EntityState.Added);
        var result = await customerAC.CreateRecordAsync(record, cancellationToken);

        if (!result.Success)
        {
            var message = ToCustomerError(result.ErrorMessage);
            feedback.Error(message, "Customer not created");
            return CustomerOperationResult<Customer>.Fail(message);
        }

        if (result.Value is not null)
        {
            record.MasterAccountID = string.IsNullOrWhiteSpace(result.Value.Id)
                ? record.MasterAccountID
                : result.Value.Id;
            record.DisplayCode = string.IsNullOrWhiteSpace(result.Value.DisplayCode)
                ? record.DisplayCode
                : result.Value.DisplayCode;
        }

        var created = MapAndCache(record);
        feedback.Success("Customer created successfully.", "Customer created");
        return CustomerOperationResult<Customer>.Ok(created);
    }

    public async Task<CustomerOperationResult<Customer>> UpdateCustomerAsync(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var record = customerRecords.TryGetValue(customer.Id, out var cachedRecord)
            ? cachedRecord
            : FromCustomer(customer, EBI.Enum.EntityState.Changed);

        ApplyCustomer(record, customer, EBI.Enum.EntityState.Changed);
        EnsureSqlSafeDates(record);

        var result = await customerAC.UpdateRecordAsync(record, cancellationToken);
        if (!result.Success)
        {
            var message = ToCustomerError(result.ErrorMessage);
            feedback.Error(message, "Customer not updated");
            return CustomerOperationResult<Customer>.Fail(message);
        }

        var updated = MapAndCache(record, customer.Id);
        feedback.Success("Customer details updated successfully.", "Customer updated");
        return CustomerOperationResult<Customer>.Ok(updated);
    }

    public async Task<CustomerOperationResult<Customer>> DeactivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (!customerRecords.TryGetValue(customerId, out var record))
        {
            return CustomerOperationResult<Customer>.Fail("Customer record is not loaded.");
        }

        record.AccountStatus = "Inactive";
        record.IsDirty = true;
        record.SaveAction = EBI.Enum.EntityState.Changed;
        EnsureSqlSafeDates(record);

        var result = await customerAC.UpdateRecordAsync(record, cancellationToken);
        if (!result.Success)
        {
            var message = ToCustomerError(result.ErrorMessage);
            feedback.Error(message, "Customer not deactivated");
            return CustomerOperationResult<Customer>.Fail(message);
        }

        var deactivated = MapAndCache(record, customerId);
        feedback.Success("Customer was deactivated successfully.", "Customer deactivated");
        return CustomerOperationResult<Customer>.Ok(deactivated);
    }

    private Customer MapAndCache(CustomerDM record)
    {
        var customer = ToCustomer(record);
        customerRecords[customer.Id] = record;
        return customer;
    }

    private Customer MapAndCache(CustomerDM record, Guid customerId)
    {
        var customer = ToCustomer(record);
        customer.Id = customerId;
        customerRecords[customer.Id] = record;
        return customer;
    }

    private static Customer ToCustomer(CustomerDM record)
    {
        var fullName = FirstNonEmpty(record.AccountName, record.DisplayCode, record.MasterAccountID) ?? string.Empty;
        var membershipType = FirstNonEmpty(record.MembershipTypeName, record.PriceGroupID, record.MembershipTypeID) ?? string.Empty;

        return new Customer
        {
            SystemID = record.MasterAccountID,
            AccountStatus = string.IsNullOrWhiteSpace(record.AccountStatus) ? "Active" : record.AccountStatus,
            IsVip = string.Equals(record.PriceGroupID, "VIP", StringComparison.OrdinalIgnoreCase)
                || (record.MembershipTypeName?.Contains("VIP", StringComparison.OrdinalIgnoreCase) ?? false),
            FirstName = fullName,
            LastName = string.Empty,
            IdentificationNumber = record.NRIC ?? string.Empty,
            Email = record.Email ?? string.Empty,
            DateOfBirth = BuildBirthday(record),
            Gender = record.Gender ?? string.Empty,
            ContactNumber1 = FirstNonEmpty(record.Phone, record.Contact) ?? string.Empty,
            ContactNumber2 = record.Phone == record.Contact ? string.Empty : record.Contact ?? string.Empty,
            Consultant = FirstNonEmpty(record.PreferredEmployee, record.SalesPersonName) ?? string.Empty,
            ConsultantId = FirstNonEmpty(record.EmployeeID, record.SalesPersonID) ?? string.Empty,
            MembershipType = membershipType,
            MembershipTypeId = record.MembershipTypeID ?? string.Empty,
            BranchId = record.BranchID ?? string.Empty,
            Source = FirstNonEmpty(record.CustomerSourceName, record.CustomerSourceID) ?? string.Empty,
            ExternalCode = FirstNonEmpty(record.LocalCode, record.DisplayCode, record.AlphaCode) ?? string.Empty,
            InvoiceIdentificationType = record.NRICType ?? string.Empty,
            InvoiceIdentificationNumber = record.NRIC ?? string.Empty,
            TaxIdentificationNumber = record.TIN ?? string.Empty,
            Address1 = record.Address1 ?? string.Empty,
            Address2 = record.Address2 ?? string.Empty,
            Country = record.Country ?? string.Empty,
            State = record.CountryState ?? string.Empty,
            City = record.City ?? string.Empty,
            Postcode = record.ZipCode ?? string.Empty,
            DrugAllergies = record.IsAllergy ? "Yes" : string.Empty,
            CurrentIllness = record.CurrentCondition ?? string.Empty,
            CustomerTag = record.CustomerGroupName ?? string.Empty,
            AlertAllergies = record.Alert ?? string.Empty,
            Race = record.RaceName ?? string.Empty,
            PreferredLanguage = record.LanguageName ?? string.Empty,
            Referrer = record.Referer ?? string.Empty,
            MaritalStatus = record.MaritalStatus ?? string.Empty,
            Notes = record.Comment ?? string.Empty,
            PhotoPath = record.ImagePath,
            PhotoFileName = record.ImageFileName
        };
    }

    private static CustomerDM FromCustomer(Customer customer, EBI.Enum.EntityState saveAction)
    {
        var record = new CustomerDM
        {
            MasterAccountID = NormalizeApiId(customer.SystemID, saveAction),
            CreatedDateTime = DateTime.UtcNow,
            ModifiedDateTime = DateTime.UtcNow,
            AccountSince = DateTime.Today,
            MembershipSince = DateTime.MinValue,
            MembershipValidFrom = DateTime.MinValue,
            MembershipValidTo = DateTime.MinValue,
            DateGSTStatusVerified = DateTime.MinValue,
            UpdateTimeStamp = DateTime.MinValue
        };

        ApplyCustomer(record, customer, saveAction);
        EnsureSqlSafeDates(record);
        return record;
    }

    private static void ApplyCustomer(CustomerDM record, Customer customer, EBI.Enum.EntityState saveAction)
    {
        record.AccountName = $"{customer.FirstName} {customer.LastName}".Trim();
        record.MasterAccountID = string.IsNullOrWhiteSpace(customer.SystemID)
            ? NormalizeApiId(record.MasterAccountID, saveAction)
            : customer.SystemID.Trim();
        record.LocalCode = customer.ExternalCode;
        record.NRIC = customer.UseExistingIdentification
            ? customer.IdentificationNumber
            : FirstNonEmpty(customer.InvoiceIdentificationNumber, customer.IdentificationNumber);
        record.Email = customer.Email;
        record.Phone = customer.ContactNumber1;
        record.Contact = string.IsNullOrWhiteSpace(customer.ContactNumber2) ? customer.ContactNumber1 : customer.ContactNumber2;
        record.Gender = customer.Gender;
        record.SalesPersonName = customer.Consultant;
        record.PreferredEmployee = customer.Consultant;
        record.SalesPersonID = customer.ConsultantId;
        record.EmployeeID = customer.ConsultantId;
        record.MembershipTypeID = customer.MembershipTypeId;
        record.MembershipTypeName = customer.MembershipType;
        record.BranchID = customer.BranchId;
        record.CustomerSourceName = customer.Source;
        record.NRICType = customer.InvoiceIdentificationType;
        record.TIN = customer.TaxIdentificationNumber;
        record.Address1 = customer.Address1;
        record.Address2 = customer.Address2;
        record.Country = customer.Country;
        record.CountryState = customer.State;
        record.City = customer.City;
        record.ZipCode = customer.Postcode;
        record.IsAllergy = !string.IsNullOrWhiteSpace(customer.DrugAllergies)
            && !string.Equals(customer.DrugAllergies.Trim(), "No", StringComparison.OrdinalIgnoreCase);
        record.CurrentCondition = customer.CurrentIllness;
        record.CustomerGroupName = customer.CustomerTag;
        record.Alert = customer.AlertAllergies;
        record.RaceName = customer.Race;
        record.LanguageName = customer.PreferredLanguage;
        record.Referer = customer.Referrer;
        record.MaritalStatus = customer.MaritalStatus;
        record.Comment = customer.Notes;
        record.ImagePath = customer.PhotoPath;
        record.ImageFileName = customer.PhotoFileName;
        record.PriceGroupID = customer.IsVip ? "VIP" : null;
        record.AccountStatus = string.IsNullOrWhiteSpace(customer.AccountStatus)
            ? (string.IsNullOrWhiteSpace(record.AccountStatus) ? "Active" : record.AccountStatus)
            : customer.AccountStatus;
        record.AccountTypeID = record.AccountTypeID == 0 ? 3 : record.AccountTypeID;
        record.IsDirty = true;
        record.SaveAction = saveAction;
        ApplyBirthday(record, customer.DateOfBirth);
    }

    private static void EnsureSqlSafeDates(CustomerDM record)
    {
        record.CreatedDateTime = ToSqlDate(record.CreatedDateTime, DateTime.UtcNow);
        record.ModifiedDateTime = DateTime.UtcNow;
        record.AccountSince = ToSqlDate(record.AccountSince, DateTime.Today);
        record.MembershipSince = ToSqlDate(record.MembershipSince);
        record.MembershipValidFrom = ToSqlDate(record.MembershipValidFrom);
        record.MembershipValidTo = ToSqlDate(record.MembershipValidTo);
        record.DateGSTStatusVerified = ToSqlDate(record.DateGSTStatusVerified);
        record.UpdateTimeStamp = ToSqlDate(record.UpdateTimeStamp);
    }

    private static DateTime? BuildBirthday(CustomerDM record)
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

    private static void ApplyBirthday(CustomerDM record, DateTime? birthday)
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static DateTime ToSqlDate(DateTime value)
    {
        return value >= SqlMinDate ? value : SqlMinDate;
    }

    private static DateTime ToSqlDate(DateTime value, DateTime fallback)
    {
        return value >= SqlMinDate ? value : fallback;
    }

    private static string NormalizeApiId(string? value, EBI.Enum.EntityState saveAction)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return saveAction == EBI.Enum.EntityState.Added
            ? NewCustomerMasterAccountPlaceholder
            : string.Empty;
    }

    private static string ToCustomerError(string? errorMessage)
    {
        return string.IsNullOrWhiteSpace(errorMessage)
            ? "Unable to complete the customer request."
            : errorMessage;
    }
}
