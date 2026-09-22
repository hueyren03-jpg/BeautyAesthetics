using System.Net;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services;

public sealed class AppointmentService
{
    private static readonly DateTime SqlMinDate = new(1900, 1, 1);
    private const string AppointmentMetadataPrefix = "[[beauty-appointment:";
    private const string AppointmentMetadataSuffix = "]]";
    private readonly AppointmentAC appointmentAC;
    private readonly Dictionary<string, AppointmentDM> records = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> localIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> recurrencePatterns = new(StringComparer.OrdinalIgnoreCase);
    private int nextLocalId = 1;

    public AppointmentService(AppointmentAC appointmentAC) => this.appointmentAC = appointmentAC;

    public event Action? OnChange;
    public IReadOnlyList<AppointmentStatusOption> Statuses { get; private set; } = Array.Empty<AppointmentStatusOption>();

    public async Task<ApiCallResult<IReadOnlyList<AppointmentStatusOption>>> LoadStatusesAsync(CancellationToken cancellationToken = default)
    {
        var result = await appointmentAC.GetStatusListAsync(cancellationToken);
        if (!result.Success)
            return ApiCallResult<IReadOnlyList<AppointmentStatusOption>>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to load appointment statuses.");

        Statuses = ParseStatuses(result.Value).DistinctBy(x => x.Value).OrderBy(x => x.Value).ToList();
        return ApiCallResult<IReadOnlyList<AppointmentStatusOption>>.Ok(result.StatusCode, Statuses);
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> GetAllAppointmentsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var result = await appointmentAC.GetAllAppointmentsAsync(cancellationToken);
        var mapped = MapListResult(result);
        if (!mapped.Success || mapped.Value is null) return mapped;

        return ExpandResultForRange(mapped, startDate, endDate);
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> GetAppointmentsByCustomerAsync(string customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var result = await appointmentAC.GetAppointmentsAsync(new AppointmentCustomerRequestDTO { CustomerId = customerId, StartDate = startDate, EndDate = endDate }, cancellationToken);
        return ExpandResultForRange(MapListResult(result), startDate, endDate);
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> GetAppointmentsByEmployeeAsync(string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var result = await appointmentAC.GetByEmployeeAsync(new AppointmentEmployeeRequestDTO { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate }, cancellationToken);
        return ExpandResultForRange(MapListResult(result), startDate, endDate);
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> GetAppointmentsByEmployeeAndStatusAsync(string employeeId, int status, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var result = await appointmentAC.GetByEmployeeAndStatusAsync(new AppointmentEmployeeStatusRequestDTO { EmployeeId = employeeId, Status = status, StartDate = startDate, EndDate = endDate }, cancellationToken);
        return ExpandResultForRange(MapListResult(result), startDate, endDate);
    }

    public async Task<ApiCallResult<Appointment>> AddAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var error = Validate(appointment);
        if (error is not null) return ApiCallResult<Appointment>.Failure(HttpStatusCode.BadRequest, error);

        var record = ToRecord(appointment, null, EntityState.Added);
        var result = await appointmentAC.CreateAppointmentAsync(record, cancellationToken);
        if (!result.Success)
            return ApiCallResult<Appointment>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to create the appointment.");

        if (!string.IsNullOrWhiteSpace(result.Value) && !string.Equals(result.Value, "Success", StringComparison.OrdinalIgnoreCase))
            record.AppointmentID = result.Value;

        var saved = ToAppointment(record);
        PreserveClientValues(saved, appointment);
        RememberRecurrence(record.AppointmentID, appointment);
        Cache(record);
        OnChange?.Invoke();
        return ApiCallResult<Appointment>.Ok(result.StatusCode, saved);
    }

    public async Task<ApiCallResult<Appointment>> UpdateAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var error = Validate(appointment);
        if (error is not null) return ApiCallResult<Appointment>.Failure(HttpStatusCode.BadRequest, error);
        if (string.IsNullOrWhiteSpace(appointment.AppointmentId))
            return ApiCallResult<Appointment>.Failure(HttpStatusCode.BadRequest, "The selected appointment has no API record ID.");

        records.TryGetValue(appointment.AppointmentId, out var existing);
        var record = ToRecord(appointment, existing, EntityState.Changed);
        var result = await appointmentAC.UpdateRecordAsync(record, cancellationToken);
        if (!result.Success)
            return ApiCallResult<Appointment>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to update the appointment.");

        var saved = ToAppointment(record);
        PreserveClientValues(saved, appointment);
        RememberRecurrence(record.AppointmentID, appointment);
        Cache(record);
        OnChange?.Invoke();
        return ApiCallResult<Appointment>.Ok(result.StatusCode, saved);
    }

    public async Task<ApiCallResult<Appointment>> CancelAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var status = Statuses.FirstOrDefault(x => x.Name.Contains("cancel", StringComparison.OrdinalIgnoreCase));
        if (status is null)
            return ApiCallResult<Appointment>.Failure(HttpStatusCode.BadRequest, "The API did not provide a Cancelled appointment status.");

        appointment.Label = status.Value;
        appointment.StatusName = status.Name;
        return await UpdateAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<ApiCallResult<Appointment>> DeleteAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appointment.AppointmentId))
        {
            return ApiCallResult<Appointment>.Failure(
                HttpStatusCode.BadRequest,
                "The selected appointment has no API record ID.");
        }

        var cancelledStatus = Statuses.FirstOrDefault(status =>
            status.Name.Contains("cancel", StringComparison.OrdinalIgnoreCase));
        if (cancelledStatus is null || appointment.Label != cancelledStatus.Value)
        {
            return ApiCallResult<Appointment>.Failure(
                HttpStatusCode.BadRequest,
                "Cancel the appointment before deleting it.");
        }

        var result = await appointmentAC.DeleteAppointmentAsync(appointment.AppointmentId, cancellationToken);
        if (!result.Success)
        {
            return ApiCallResult<Appointment>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to delete the appointment.");
        }

        records.Remove(appointment.AppointmentId);
        localIds.Remove(appointment.AppointmentId);
        OnChange?.Invoke();
        return ApiCallResult<Appointment>.Ok(result.StatusCode, appointment);
    }
    private ApiCallResult<IReadOnlyList<Appointment>> MapListResult(ApiCallResult<List<AppointmentDM>> result)
    {
        if (!result.Success || result.Value is null)
            return ApiCallResult<IReadOnlyList<Appointment>>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to load appointments.");

        records.Clear();
        var items = result.Value
            .DistinctBy(GetRecordIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(x => { var appointment = ToAppointment(x); Cache(x); return appointment; })
            .OrderBy(x => x.Start)
            .ToList();
        return ApiCallResult<IReadOnlyList<Appointment>>.Ok(result.StatusCode, items);
    }

    private ApiCallResult<IReadOnlyList<Appointment>> ExpandResultForRange(
        ApiCallResult<IReadOnlyList<Appointment>> mapped,
        DateTime startDate,
        DateTime endDate)
    {
        if (!mapped.Success || mapped.Value is null) return mapped;

        var expanded = mapped.Value
            .SelectMany(appointment => ExpandForRange(appointment, startDate, endDate))
            .OrderBy(appointment => appointment.Start)
            .ToList();
        return ApiCallResult<IReadOnlyList<Appointment>>.Ok(mapped.StatusCode, expanded);
    }
    private static string GetRecordIdentity(AppointmentDM record)
    {
        if (!string.IsNullOrWhiteSpace(record.AppointmentID)) return $"appointment:{record.AppointmentID}";
        if (!string.IsNullOrWhiteSpace(record.BookingID)) return $"booking:{record.BookingID}";

        return string.Join("|",
            record.CustomerID,
            record.EmployeeID,
            record.StartTime.Ticks,
            record.EndTime.Ticks,
            record.InventoryIDs,
            record.AppointmentSubject);
    }
    private void Cache(AppointmentDM record)
    {
        if (string.IsNullOrWhiteSpace(record.AppointmentID)) return;

        records[record.AppointmentID] = record;
        if (!string.IsNullOrWhiteSpace(record.RecurranceInfo))
            recurrencePatterns[record.AppointmentID] = record.RecurranceInfo;
    }

    private Appointment ToAppointment(AppointmentDM record)
    {
        var key = First(record.AppointmentID, record.BookingID, Guid.NewGuid().ToString("N"));
        var employeeId = First(record.EmployeeID, record.SalesPersonCode);
        var metadata = ReadAppointmentMetadata(record.Memo);
        var recurrenceInfo = First(
            record.RecurranceInfo,
            metadata.RecurrencePattern,
            !string.IsNullOrWhiteSpace(record.AppointmentID) && recurrencePatterns.TryGetValue(record.AppointmentID, out var cachedRecurrence)
                ? cachedRecurrence
                : null);
        var start = NormalizeDate(record.StartTime, DateTime.Today.AddHours(9));
        var end = NormalizeEnd(record.StartTime, record.EndTime);
        var isAllDay = record.AllDay || metadata.IsAllDay || IsWorkingHoursAppointment(start, end);
        if (isAllDay)
        {
            start = start.Date.AddHours(9);
            end = start.Date.AddHours(18);
        }

        return new Appointment
        {
            Id = GetLocalId(key),
            AppointmentId = record.AppointmentID ?? string.Empty,
            BookingId = record.BookingID ?? string.Empty,
            CustomerId = record.CustomerID ?? string.Empty,
            CustomerName = record.CustomerName ?? string.Empty,
            RequestedBy = record.RequestedBy ?? string.Empty,
            EmployeeId = employeeId,
            StaffName = First(record.ManualEmployeeID, employeeId),
            SalesPersonCode = record.SalesPersonCode ?? string.Empty,
            ServiceItem = First(record.SalesDescriptions, record.AppointmentSubject),
            ServiceInventoryId = record.InventoryIDs ?? string.Empty,
            Start = start,
            End = end,
            IsAllDay = isAllDay,
            IsRecurring = !string.IsNullOrWhiteSpace(recurrenceInfo),
            RecurrencePattern = ParseRecurrencePattern(recurrenceInfo),
            Remarks = RemoveAppointmentMetadata(record.Memo),
            Location = First(record.AppointmentLocation, record.BranchID),
            Room = record.ReminderInfo ?? string.Empty,
            BranchId = First(record.AppointmentLocation, record.BranchID),
            BusyStatus = record.BusyStatus,
            StatusName = Statuses.FirstOrDefault(x => x.Value == record.Label)?.Name ?? string.Empty,
            Label = record.Label,
            PaymentStatus = record.PaymentStatus,
            AppointmentType = record.AppointmentType,
            Subject = record.AppointmentSubject ?? string.Empty
        };
    }

    private static AppointmentDM ToRecord(Appointment appointment, AppointmentDM? existing, EntityState saveAction)
    {
        var now = DateTime.Now;
        var record = existing ?? new AppointmentDM { CreatedDateTime = now, TransactionTimeStamp = now };
        record.AppointmentID = appointment.AppointmentId;
        record.BookingID = appointment.BookingId;
        record.EmployeeID = appointment.EmployeeId;
        record.ManualEmployeeID = appointment.EmployeeId;
        record.StartTime = NormalizeApiDate(appointment.Start, now);
        record.EndTime = NormalizeApiDate(appointment.End, appointment.Start.AddMinutes(30));
        record.BusyStatus = appointment.BusyStatus;
        record.Label = appointment.Label;
        record.Memo = BuildMemoWithAppointmentMetadata(appointment);
        record.AllDay = appointment.IsAllDay;
        record.RecurranceInfo = appointment.IsRecurring ? SerializeRecurrencePattern(appointment.RecurrencePattern) : string.Empty;
        record.ReminderInfo = appointment.Room;
        record.CustomerID = appointment.CustomerId;
        record.CustomerName = appointment.CustomerName;
        var branchId = string.IsNullOrWhiteSpace(appointment.BranchId) ? "HQ" : appointment.BranchId.Trim();
        record.BranchID = branchId;
        record.PaymentStatus = appointment.PaymentStatus;
        record.AppointmentSubject = First(appointment.Subject, appointment.ServiceItem, appointment.CustomerName);
        record.AppointmentLocation = branchId;
        record.AppointmentType = appointment.AppointmentType;
        record.ModifiedDateTime = now;
        record.TransactionTimeStamp = now;
        record.InventoryIDs = appointment.ServiceInventoryId;
        record.SalesDescriptions = appointment.ServiceItem;
        record.SalesPersonCode = existing?.SalesPersonCode ?? string.Empty;
        record.RequestedBy = appointment.RequestedBy;
        record.SaveAction = saveAction;
        record.IsDirty = true;
        return record;
    }

    private static void PreserveClientValues(Appointment saved, Appointment source)
    {
        // A move changes placement only. Always retain the exact duration that
        // the user dragged, even if the mutation response omits or normalizes it.
        var duration = source.End - source.Start;
        saved.Start = source.Start;
        saved.End = source.Start.Add(duration);
        saved.Room = source.Room;
        saved.Phone = source.Phone;
        saved.Tags = source.Tags.ToList();
        saved.IsAllDay = source.IsAllDay;
        saved.IsRecurring = source.IsRecurring;
        saved.RecurrencePattern = source.RecurrencePattern;
        if (source.Id != 0)
        {
            saved.Id = source.Id;
        }
    }
    private void RememberRecurrence(string? appointmentId, Appointment appointment)
    {
        if (string.IsNullOrWhiteSpace(appointmentId)) return;
        if (!appointment.IsRecurring)
        {
            recurrencePatterns.Remove(appointmentId);
            return;
        }

        recurrencePatterns[appointmentId] = SerializeRecurrencePattern(appointment.RecurrencePattern);
    }
    private int GetLocalId(string key)
    {
        if (localIds.TryGetValue(key, out var id)) return id;
        id = nextLocalId++;
        localIds[key] = id;
        return id;
    }

    private static string? Validate(Appointment appointment)
    {
        if (string.IsNullOrWhiteSpace(appointment.CustomerName)) return "Customer name is required.";
        if (appointment.Start < SqlMinDate) return "Appointment start date is invalid.";
        if (appointment.End <= appointment.Start) return "Appointment end time must be after the start time.";
        if (string.IsNullOrWhiteSpace(appointment.EmployeeId)) return "Select an employee.";
        return null;
    }

    private static DateTime NormalizeDate(DateTime value, DateTime fallback)
    {
        if (value < SqlMinDate) return fallback;

        // This API stores clinic-local wall-clock values. Some responses still
        // append a UTC marker, so converting them would incorrectly add the
        // device offset (9:00 AM becomes 5:00 PM in Malaysia).
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static DateTime NormalizeApiDate(DateTime value, DateTime fallback)
    {
        var normalized = value >= SqlMinDate ? value : fallback;
        if (normalized.Kind == DateTimeKind.Utc)
        {
            normalized = normalized.ToLocalTime();
        }

        // The appointment API and its SQL data use clinic-local wall-clock values.
        // Send an unspecified DateTime so JSON does not shift the selected slot to UTC.
        return DateTime.SpecifyKind(normalized, DateTimeKind.Unspecified);
    }
    private IEnumerable<Appointment> ExpandForRange(Appointment appointment, DateTime rangeStart, DateTime rangeEnd)
    {
        if (!appointment.IsRecurring)
        {
            if (OverlapsRange(appointment, rangeStart, rangeEnd)) yield return appointment;
            yield break;
        }

        var occurrenceStart = appointment.Start;
        var duration = appointment.End - appointment.Start;
        while (occurrenceStart < rangeStart)
        {
            occurrenceStart = AddRecurrenceInterval(occurrenceStart, appointment.RecurrencePattern);
        }

        while (occurrenceStart <= rangeEnd)
        {
            var occurrence = new Appointment
            {
                Id = GetLocalId($"{appointment.AppointmentId}:{occurrenceStart.Ticks}"),
                AppointmentId = appointment.AppointmentId,
                BookingId = appointment.BookingId,
                CustomerId = appointment.CustomerId,
                CustomerName = appointment.CustomerName,
                RequestedBy = appointment.RequestedBy,
                Phone = appointment.Phone,
                Location = appointment.Location,
                ServiceItem = appointment.ServiceItem,
                ServiceInventoryId = appointment.ServiceInventoryId,
                EmployeeId = appointment.EmployeeId,
                StaffName = appointment.StaffName,
                Room = appointment.Room,
                Start = occurrenceStart,
                End = occurrenceStart.Add(duration),
                IsAllDay = appointment.IsAllDay,
                IsRecurring = true,
                RecurrencePattern = appointment.RecurrencePattern,
                Remarks = appointment.Remarks,
                BusyStatus = appointment.BusyStatus,
                StatusName = appointment.StatusName,
                Label = appointment.Label,
                PaymentStatus = appointment.PaymentStatus,
                AppointmentType = appointment.AppointmentType,
                Subject = appointment.Subject,
                BranchId = appointment.BranchId,
                SalesPersonCode = appointment.SalesPersonCode,
                Tags = appointment.Tags.ToList()
            };

            if (OverlapsRange(occurrence, rangeStart, rangeEnd)) yield return occurrence;
            occurrenceStart = AddRecurrenceInterval(occurrenceStart, appointment.RecurrencePattern);
        }
    }

    private static bool OverlapsRange(Appointment appointment, DateTime rangeStart, DateTime rangeEnd) =>
        appointment.Start <= rangeEnd && appointment.End > rangeStart;

    private static DateTime AddRecurrenceInterval(DateTime value, string? pattern) => pattern switch
    {
        "Daily" => value.AddDays(1),
        "Monthly" => value.AddMonths(1),
        _ => value.AddDays(7)
    };

    private static bool IsWorkingHoursAppointment(DateTime start, DateTime end) =>
        start.Date == end.Date &&
        start.TimeOfDay == TimeSpan.FromHours(9) &&
        end.TimeOfDay == TimeSpan.FromHours(18);
    private static DateTime NormalizeEnd(DateTime start, DateTime end)
    {
        var normalizedStart = NormalizeDate(start, DateTime.Today.AddHours(9));
        var normalizedEnd = NormalizeDate(end, normalizedStart.AddMinutes(30));
        return normalizedEnd > normalizedStart
            ? normalizedEnd
            : normalizedStart.AddMinutes(30);
    }

    private static string BuildMemoWithAppointmentMetadata(Appointment appointment)
    {
        var remarks = RemoveAppointmentMetadata(appointment.Remarks);
        var recurrence = appointment.IsRecurring ? SerializeRecurrencePattern(appointment.RecurrencePattern) : string.Empty;
        var marker = $"{AppointmentMetadataPrefix}allDay={(appointment.IsAllDay ? 1 : 0)};repeat={recurrence}{AppointmentMetadataSuffix}";
        return string.IsNullOrWhiteSpace(remarks) ? marker : $"{remarks}\n{marker}";
    }

    private static (bool IsAllDay, string RecurrencePattern) ReadAppointmentMetadata(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo)) return (false, string.Empty);
        var start = memo.LastIndexOf(AppointmentMetadataPrefix, StringComparison.Ordinal);
        if (start < 0) return (false, string.Empty);
        var end = memo.IndexOf(AppointmentMetadataSuffix, start, StringComparison.Ordinal);
        if (end < 0) return (false, string.Empty);

        var payloadStart = start + AppointmentMetadataPrefix.Length;
        var payload = memo[payloadStart..end];
        var allDay = payload.Contains("allDay=1", StringComparison.OrdinalIgnoreCase);
        var repeatStart = payload.IndexOf("repeat=", StringComparison.OrdinalIgnoreCase);
        var recurrence = repeatStart < 0 ? string.Empty : payload[(repeatStart + "repeat=".Length)..].Trim();
        return (allDay, ParseRecurrencePattern(recurrence));
    }

    private static string RemoveAppointmentMetadata(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo)) return string.Empty;
        var start = memo.LastIndexOf(AppointmentMetadataPrefix, StringComparison.Ordinal);
        if (start < 0) return memo;
        var end = memo.IndexOf(AppointmentMetadataSuffix, start, StringComparison.Ordinal);
        if (end < 0) return memo;
        return memo.Remove(start, end + AppointmentMetadataSuffix.Length - start).Trim();
    }
    private static string ParseRecurrencePattern(string? recurrenceInfo)
    {
        if (string.IsNullOrWhiteSpace(recurrenceInfo)) return string.Empty;

        var normalized = recurrenceInfo.Trim();
        if (normalized.Equals("Daily", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("FREQ=DAILY", StringComparison.OrdinalIgnoreCase))
            return "Daily";
        if (normalized.Equals("Monthly", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("FREQ=MONTHLY", StringComparison.OrdinalIgnoreCase))
            return "Monthly";
        if (normalized.Equals("Weekly", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("FREQ=WEEKLY", StringComparison.OrdinalIgnoreCase))
            return "Weekly";

        if (normalized.Contains("MONTH", StringComparison.OrdinalIgnoreCase)) return "Monthly";
        if (normalized.Contains("DAILY", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("DAY", StringComparison.OrdinalIgnoreCase)) return "Daily";
        return "Weekly";
    }

    private static string SerializeRecurrencePattern(string? pattern) => pattern?.Trim().ToLowerInvariant() switch
    {
        "daily" => "Daily",
        "monthly" => "Monthly",
        _ => "Weekly"
    };

    private static string First(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static IEnumerable<AppointmentStatusOption> ParseStatuses(JsonElement element)
    {
        IEnumerable<JsonElement> source = element.ValueKind switch
        {
            JsonValueKind.Array => element.EnumerateArray(),
            JsonValueKind.Object => element.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.Array).SelectMany(x => x.Value.EnumerateArray()),
            _ => Enumerable.Empty<JsonElement>()
        };

        var index = 0;
        foreach (var item in source)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                yield return new AppointmentStatusOption(index++, item.GetString() ?? $"Status {index}");
                continue;
            }
            if (item.ValueKind != JsonValueKind.Object) continue;
            var value = ReadInt(item, "value", "id", "status", "busyStatus", "appointmentStatusID") ?? index;
            var name = ReadString(item, "name", "text", "statusName", "description", "appointmentStatus", "appointmentStatusName") ?? $"Status {value}";
            yield return new AppointmentStatusOption(value, name);
            index++;
        }
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
            if (names.Any(x => string.Equals(x, property.Name, StringComparison.OrdinalIgnoreCase)) && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        return null;
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Any(x => string.Equals(x, property.Name, StringComparison.OrdinalIgnoreCase))) continue;
            if (property.Value.TryGetInt32(out var value)) return value;
            if (int.TryParse(property.Value.ToString(), out value)) return value;
        }
        return null;
    }
}

