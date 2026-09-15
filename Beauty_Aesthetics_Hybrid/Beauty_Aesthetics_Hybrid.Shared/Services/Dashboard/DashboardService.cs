using System.Globalization;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Customers;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly WebDashboardAC dashboardAC;
    private readonly AppointmentService appointmentService;
    private readonly ICustomerService customerService;

    public DashboardService(
        WebDashboardAC dashboardAC,
        AppointmentService appointmentService,
        ICustomerService customerService)
    {
        this.dashboardAC = dashboardAC;
        this.appointmentService = appointmentService;
        this.customerService = customerService;
    }

    public async Task<DashboardSnapshot> LoadAsync(
        DateTime startDate,
        DateTime endDate,
        string period,
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var performanceTask = TryLoadAsync(() => dashboardAC.GetBranchPerformanceSummaryAsync(
            new DashboardDateRangeRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                InventoryTypeID = 0,
                BranchID = branchId
            },
            cancellationToken), "Unable to load branch performance.");
        var memberTask = TryLoadAsync(
            () => dashboardAC.GetMemberStatisticSummaryAsync(branchId, cancellationToken),
            "Unable to load member statistics.");
        var stockTask = TryLoadAsync(
            () => dashboardAC.GetStockBelowReorderPointAsync(branchId, cancellationToken),
            "Unable to load low-stock items.");
        var followUpTask = TryLoadAsync(() => dashboardAC.GetCustomerLastVisitAsync(
            new CustomerLastVisitRequest
            {
                DaysRangeFrom = 30,
                DaysRangeTo = 90,
                PageNumber = 1,
                PageSize = 20
            },
            cancellationToken), "Unable to load customer follow-ups.");
        var appointmentTask = TryLoadAsync(
            () => appointmentService.GetAllAppointmentsAsync(startDate, endDate, cancellationToken),
            "Unable to load appointments.");
        var customerTask = TryLoadAsync(
            () => customerService.SearchCustomersAsync(string.Empty, cancellationToken),
            "Unable to load birthday members.");
        var packageTask = TryLoadAsync(() => dashboardAC.GetMemberOtherBalanceDetailAsync(
            new MemberOtherBalanceDetailRequest
            {
                Id = branchId,
                StartDate = endDate,
                BalanceType = "Package"
            },
            cancellationToken), "Unable to load expiring packages.");

        await Task.WhenAll(
            performanceTask,
            memberTask,
            stockTask,
            followUpTask,
            appointmentTask,
            customerTask,
            packageTask);

        var performanceLoad = await performanceTask;
        var memberLoad = await memberTask;
        var stockLoad = await stockTask;
        var followUpLoad = await followUpTask;
        var appointmentLoad = await appointmentTask;
        var customerLoad = await customerTask;
        var packageLoad = await packageTask;
        var performance = performanceLoad.Value;
        var members = memberLoad.Value;
        var stock = stockLoad.Value;
        var followUps = followUpLoad.Value;
        var appointments = appointmentLoad.Value;
        var customers = customerLoad.Value;
        var packages = packageLoad.Value;
        var errors = new List<string>();
        AddLoadError(errors, performanceLoad.Error);
        AddLoadError(errors, memberLoad.Error);
        AddLoadError(errors, stockLoad.Error);
        AddLoadError(errors, followUpLoad.Error);
        AddLoadError(errors, appointmentLoad.Error);
        AddLoadError(errors, customerLoad.Error);
        AddLoadError(errors, packageLoad.Error);
        if (performance is not null) AddError(errors, performance.Success, performance.IsUnauthorized, performance.ErrorMessage);
        if (members is not null) AddError(errors, members.Success, members.IsUnauthorized, members.ErrorMessage);
        if (stock is not null) AddError(errors, stock.Success, stock.IsUnauthorized, stock.ErrorMessage);
        if (followUps is not null) AddError(errors, followUps.Success, followUps.IsUnauthorized, followUps.ErrorMessage);
        if (appointments is not null) AddError(errors, appointments.Success, appointments.IsUnauthorized, appointments.ErrorMessage);
        if (packages is not null) AddError(errors, packages.Success, packages.IsUnauthorized, packages.ErrorMessage);
        if (customers is not null && !customers.Success && !string.IsNullOrWhiteSpace(customers.ErrorMessage))
            errors.Add(customers.ErrorMessage);

        var performanceSummary = performance?.Success == true
            ? MapBranchPerformance(performance.Value)
            : new BranchPerformanceSummaryDTO();
        var appointmentRecords = appointments?.Success == true && appointments.Value is not null
            ? appointments.Value
            : [];

        return new DashboardSnapshot
        {
            TotalSales = performanceSummary.Sales,
            TransactionCount = performanceSummary.TransCount,
            ItemsSold = (int)Math.Round(performanceSummary.Quantity),
            NewCustomers = GetNewCustomerCount(members?.Value, period),
            AppointmentsTotal = appointmentRecords.Count,
            AppointmentsCompleted = appointmentRecords.Count(IsCompleted),
            AppointmentsRemaining = appointmentRecords.Count(IsRemaining),
            UpcomingAppointments = MapUpcomingAppointments(appointmentRecords),
            BirthdayMembers = customers?.Success == true && customers.Value is not null
                ? MapBirthdayMembers(customers.Value, startDate, endDate, branchId)
                : [],
            ExpiringPackages = packages?.Success == true
                ? MapExpiringPackages(packages.Value, endDate)
                : [],
            LowStockItems = stock?.Success == true ? MapLowStock(stock.Value) : [],
            CustomerFollowUps = followUps?.Success == true ? MapFollowUps(followUps.Value) : [],
            ErrorMessage = errors.Count == 0 ? null : string.Join(" ", errors.Distinct())
        };
    }

    private static async Task<SafeLoad<T>> TryLoadAsync<T>(Func<Task<T>> load, string errorMessage)
    {
        try
        {
            return new SafeLoad<T>(await load(), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or ObjectDisposedException)
        {
            return new SafeLoad<T>(default, errorMessage);
        }
    }

    private static void AddLoadError(List<string> errors, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error)) errors.Add(error);
    }

    private static bool IsCompleted(Appointment appointment) => appointment.Label is 2 or 4;

    private static bool IsRemaining(Appointment appointment) => appointment.Label is not (2 or 4 or 5 or 6);

    private static IReadOnlyList<DashboardAppointment> MapUpcomingAppointments(
        IReadOnlyList<Appointment> appointments) =>
        appointments
            .Where(IsRemaining)
            .Where(appointment => appointment.End >= DateTime.Now)
            .OrderBy(appointment => appointment.Start)
            .Select(appointment => new DashboardAppointment(
                appointment.Start.ToString("dd/MM/yyyy hh:mm tt"),
                string.IsNullOrWhiteSpace(appointment.CustomerName) ? "Unknown customer" : appointment.CustomerName,
                string.IsNullOrWhiteSpace(appointment.ServiceItem) ? "Appointment" : appointment.ServiceItem))
            .Take(20)
            .ToList();

    private static IReadOnlyList<DashboardBirthdayMember> MapBirthdayMembers(
        IReadOnlyList<Customer> customers,
        DateTime startDate,
        DateTime endDate,
        string branchId) =>
        customers
            .Where(customer => customer.DateOfBirth.HasValue)
            .Where(customer => string.IsNullOrWhiteSpace(customer.BranchId) ||
                string.Equals(customer.BranchId, branchId, StringComparison.OrdinalIgnoreCase))
            .Select(customer => new { Customer = customer, Birthday = BirthdayInRange(customer.DateOfBirth!.Value, startDate, endDate) })
            .Where(item => item.Birthday.HasValue)
            .OrderBy(item => item.Birthday)
            .Select(item => new DashboardBirthdayMember(
                string.Join(" ", new[] { item.Customer.FirstName, item.Customer.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
                item.Birthday!.Value.ToString("dd/MM/yyyy"),
                item.Customer.ContactNumber1))
            .Take(20)
            .ToList();

    private static DateTime? BirthdayInRange(DateTime birthDate, DateTime startDate, DateTime endDate)
    {
        for (var year = startDate.Year; year <= endDate.Year; year++)
        {
            var day = Math.Min(birthDate.Day, DateTime.DaysInMonth(year, birthDate.Month));
            var birthday = new DateTime(year, birthDate.Month, day);
            if (birthday >= startDate.Date && birthday <= endDate.Date) return birthday;
        }

        return null;
    }

    private static IReadOnlyList<DashboardExpiringPackage> MapExpiringPackages(JsonElement result, DateTime referenceDate) =>
        EnumerateRecords(result, includeSingleObject: true)
            .Select(record => new
            {
                MemberName = ReadString(record, "CustomerName", "MemberName", "AccountName", "Name"),
                PackageName = ReadString(record, "PackageName", "ItemDescription", "SalesDescription", "Description"),
                ExpiryDate = ReadDate(record, "ExpiryDate", "DateTo", "ValidTo", "EndDate")
            })
            .Where(item => item.ExpiryDate.HasValue && !string.IsNullOrWhiteSpace(item.PackageName))
            .Select(item => new DashboardExpiringPackage(
                item.MemberName ?? "Unknown member",
                item.PackageName!,
                Math.Max(0, (item.ExpiryDate!.Value.Date - referenceDate.Date).Days)))
            .Where(item => item.DaysLeft <= 30)
            .OrderBy(item => item.DaysLeft)
            .Take(20)
            .ToList();

    private static BranchPerformanceSummaryDTO MapBranchPerformance(JsonElement result)
    {
        var record = result.ValueKind switch
        {
            JsonValueKind.Object => result,
            JsonValueKind.Array when result.GetArrayLength() > 0 => result[0],
            _ => default
        };

        if (record.ValueKind != JsonValueKind.Object)
        {
            return new BranchPerformanceSummaryDTO();
        }

        return new BranchPerformanceSummaryDTO
        {
            BranchID = ReadString(record, "BranchID"),
            Branch = ReadString(record, "Branch"),
            Sales = ReadDecimal(record, "Sales"),
            TransCount = (int)Math.Round(ReadDecimal(record, "TransCount")),
            Quantity = ReadDecimal(record, "Quantity")
        };
    }

    private static void AddError(List<string> errors, bool success, bool unauthorized, string? message)
    {
        if (!success && !unauthorized && !string.IsNullOrWhiteSpace(message))
        {
            errors.Add(message);
        }
    }

    private static int GetNewCustomerCount(MemberStatisticSummaryDTO? summary, string period)
    {
        if (summary is null)
        {
            return 0;
        }

        return period switch
        {
            "day" => summary.Today,
            "month" => summary.ThisMonth,
            _ => 0
        };
    }

    private static IReadOnlyList<DashboardLowStockItem> MapLowStock(JsonElement result)
    {
        return EnumerateRecords(result)
            .Select(record => new DashboardLowStockItem(
                ReadString(record, "InventoryName", "SalesDescription", "AccountName", "Name") ?? "Unnamed item",
                ReadDecimal(record, "QuantityBalance", "StockBalance", "CurrentStock", "Balance", "Quantity"),
                ReadString(record, "UnitOfMeasureName", "UOM", "Unit") ?? string.Empty))
            .Take(20)
            .ToList();
    }

    private static IReadOnlyList<DashboardCustomerFollowUp> MapFollowUps(JsonElement result)
    {
        return EnumerateRecords(result)
            .Select(record => new DashboardCustomerFollowUp(
                ReadString(record, "CustomerName", "AccountName", "Name") ?? "Unknown customer",
                ReadDateLabel(record, "LastVisitDate", "LastVisit", "FinancialDate", "VisitDate"),
                ReadString(record, "ServiceName", "SalesDescription", "Service") ?? "Last visit"))
            .Take(20)
            .ToList();
    }

    private static IEnumerable<JsonElement> EnumerateRecords(JsonElement element, bool includeSingleObject = false)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().ToArray();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in new[] { "items", "records", "data", "result" })
        {
            var property = FindProperty(element, name);
            if (property.HasValue && property.Value.ValueKind == JsonValueKind.Array)
            {
                return property.Value.EnumerateArray().ToArray();
            }
        }

        return includeSingleObject && element.EnumerateObject().Any() ? [element] : [];
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var property = FindProperty(element, name);
            if (property.HasValue && property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static decimal ReadDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var property = FindProperty(element, name);
            if (!property.HasValue)
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetDecimal(out var number))
            {
                return number;
            }

            if (property.Value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(property.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return 0;
    }

    private static string ReadDateLabel(JsonElement element, params string[] names)
    {
        var raw = ReadString(element, names);
        return DateTime.TryParse(raw, out var date)
            ? date.ToString("dd/MM/yyyy")
            : raw ?? "No visit date";
    }

    private static DateTime? ReadDate(JsonElement element, params string[] names)
    {
        var raw = ReadString(element, names);
        return DateTime.TryParse(raw, out var date) && date.Year > 1900 ? date : null;
    }

    private static JsonElement? FindProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private sealed record SafeLoad<T>(T? Value, string? Error);
}
