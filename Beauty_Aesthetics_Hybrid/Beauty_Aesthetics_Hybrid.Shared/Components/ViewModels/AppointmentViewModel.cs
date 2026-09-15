using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class AppointmentViewModel
{
    private readonly AppointmentService appointmentService;

    public AppointmentViewModel(AppointmentService appointmentService) => this.appointmentService = appointmentService;

    public List<Appointment> Appointments { get; private set; } = new();
    public DateTime SelectedDate { get; set; } = DateTime.Today;

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> LoadAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var start = SelectedDate.Date;
        var end = start.AddDays(1).AddTicks(-1);
        var result = await appointmentService.GetAllAppointmentsAsync(start, end, cancellationToken);
        if (result.Success && result.Value is not null) Appointments = result.Value.ToList();
        return result;
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> LoadByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.GetAppointmentsByCustomerAsync(customerId, SelectedDate.Date, SelectedDate.Date.AddDays(1).AddTicks(-1), cancellationToken);
        if (result.Success && result.Value is not null) Appointments = result.Value.ToList();
        return result;
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> LoadByEmployeeAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.GetAppointmentsByEmployeeAsync(employeeId, SelectedDate.Date, SelectedDate.Date.AddDays(1).AddTicks(-1), cancellationToken);
        if (result.Success && result.Value is not null) Appointments = result.Value.ToList();
        return result;
    }

    public async Task<ApiCallResult<IReadOnlyList<Appointment>>> LoadByEmployeeAndStatusAsync(string employeeId, int status, CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.GetAppointmentsByEmployeeAndStatusAsync(employeeId, status, SelectedDate.Date, SelectedDate.Date.AddDays(1).AddTicks(-1), cancellationToken);
        if (result.Success && result.Value is not null) Appointments = result.Value.ToList();
        return result;
    }

    public async Task<ApiCallResult<Appointment>> AddAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.AddAppointmentAsync(appointment, cancellationToken);
        if (!result.Success || result.Value is null) return result;

        ApiCallResult<IReadOnlyList<Appointment>>? reloadResult = null;
        var dayStart = appointment.Start.Date;
        var dayEnd = dayStart.AddDays(1).AddTicks(-1);

        // Confirm the create against the API instead of displaying a local-only copy.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(300, cancellationToken);
            }

            reloadResult = await appointmentService.GetAllAppointmentsAsync(dayStart, dayEnd, cancellationToken);
            if (!reloadResult.Success || reloadResult.Value is null)
            {
                continue;
            }

            var persisted = reloadResult.Value.FirstOrDefault(item => IsPersistedCreate(item, result.Value));
            if (persisted is not null)
            {
                Appointments = reloadResult.Value.OrderBy(item => item.Start).ToList();
                return ApiCallResult<Appointment>.Ok(result.StatusCode, persisted);
            }
        }

        if (reloadResult is { Success: false })
        {
            return ApiCallResult<Appointment>.Failure(
                reloadResult.StatusCode,
                reloadResult.ErrorMessage ?? "The appointment was submitted, but the schedule could not be reloaded.");
        }

        return ApiCallResult<Appointment>.Failure(
            result.StatusCode,
            "The API accepted the request, but the new appointment was not returned by the server. Please try again.");
    }

    public async Task<ApiCallResult<Appointment>> UpdateAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.UpdateAppointmentAsync(appointment, cancellationToken);
        if (!result.Success || result.Value is null) return result;

        MergeSavedAppointment(result.Value);
        return result;
    }

    private void MergeSavedAppointment(Appointment savedAppointment)
    {
        var existingIndex = Appointments.FindIndex(existing => IsSameAppointment(existing, savedAppointment));
        if (existingIndex >= 0)
        {
            Appointments[existingIndex] = savedAppointment;
        }
        else
        {
            Appointments.Add(savedAppointment);
        }

        Appointments = Appointments.OrderBy(item => item.Start).ToList();
    }

    private static bool IsSameAppointment(Appointment left, Appointment right)
    {
        if (left.Id > 0 && right.Id > 0 && left.Id == right.Id)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(left.AppointmentId) &&
            !string.IsNullOrWhiteSpace(right.AppointmentId))
        {
            return string.Equals(left.AppointmentId, right.AppointmentId, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(left.BookingId) &&
            !string.IsNullOrWhiteSpace(right.BookingId))
        {
            return string.Equals(left.BookingId, right.BookingId, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.CustomerId, right.CustomerId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.CustomerName, right.CustomerName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.EmployeeId, right.EmployeeId, StringComparison.OrdinalIgnoreCase) &&
               left.Start == right.Start &&
               left.End == right.End;
    }
    private static bool IsPersistedCreate(Appointment persisted, Appointment submitted)
    {
        if (!string.IsNullOrWhiteSpace(submitted.AppointmentId) &&
            string.Equals(persisted.AppointmentId, submitted.AppointmentId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sameCustomer = !string.IsNullOrWhiteSpace(submitted.CustomerId)
            ? string.Equals(persisted.CustomerId, submitted.CustomerId, StringComparison.OrdinalIgnoreCase)
            : string.Equals(persisted.CustomerName, submitted.CustomerName, StringComparison.OrdinalIgnoreCase);

        return sameCustomer &&
               string.Equals(persisted.EmployeeId, submitted.EmployeeId, StringComparison.OrdinalIgnoreCase) &&
               Math.Abs((persisted.Start - submitted.Start).TotalSeconds) < 1 &&
               Math.Abs((persisted.End - submitted.End).TotalSeconds) < 1;
    }
    public Task<ApiCallResult<Appointment>> CancelAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default) =>
        appointmentService.CancelAppointmentAsync(appointment, cancellationToken);

    public async Task<ApiCallResult<Appointment>> DeleteAppointmentAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        var result = await appointmentService.DeleteAppointmentAsync(appointment, cancellationToken);
        if (result.Success)
        {
            Appointments = Appointments
                .Where(existing => !IsSameAppointment(existing, appointment))
                .ToList();
        }

        return result;
    }
}
