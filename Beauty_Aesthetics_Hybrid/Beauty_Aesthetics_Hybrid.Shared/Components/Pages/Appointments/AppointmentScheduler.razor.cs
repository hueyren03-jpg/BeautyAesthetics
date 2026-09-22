using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services;
using Beauty_Aesthetics_WebPos.Components.Services.Customers;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Pages
{
    public partial class AppointmentScheduler : ComponentBase, IDisposable
    {
        [Inject]
        public AppointmentService AppointmentService { get; set; } = null!;

        [Inject]
        public IEmployeeService EmployeeService { get; set; } = null!;

        [Inject]
        public ICustomerService CustomerService { get; set; } = null!;

        [Inject]
        public IServiceItemService ServiceItemService { get; set; } = null!;

        [Inject]
        public IBranchLookupService BranchLookupService { get; set; } = null!;

        [Inject]
        public IJSRuntime JS { get; set; } = null!;

        [Inject]
        public NavigationManager NavigationManager { get; set; } = null!;

        [Inject]
        public AppFeedbackService Feedback { get; set; } = null!;

        private AppointmentViewModel ViewModel { get; set; } = null!;

        private DateTime SelectedDate
        {
            get => ViewModel.SelectedDate;
            set
            {
                if (ViewModel.SelectedDate != value)
                {
                    ViewModel.SelectedDate = value;
                    _ = ReloadAppointmentsAsync();
                    _ = InvokeAsync(StateHasChanged);
                }
            }
        }

        private List<Staff> StaffList = new();
        private readonly Dictionary<string, bool> staffVisibility = new();

        private List<string> RoomList = new()
        {
            "Room A",
            "Room B",
            "Room C"
        };

        private IReadOnlyList<BranchLookupItem> branchOptions = Array.Empty<BranchLookupItem>();
        private List<string> ServicesList = new();
        private IReadOnlyList<Customer> customerOptions = Array.Empty<Customer>();
        private IReadOnlyList<ServiceViewModel.ServiceItem> serviceOptions = Array.Empty<ServiceViewModel.ServiceItem>();
        private IReadOnlyList<AppointmentStatusOption> appointmentStatuses = Array.Empty<AppointmentStatusOption>();
        private bool isAppointmentLoading;
        private bool appointmentReloadPending;
        private bool isAppointmentSaving;
        private bool appointmentCreateSubmitted;
        private string? appointmentError;
        private string selectedCustomerFilterId = string.Empty;
        private string selectedEmployeeFilterId = string.Empty;
        private string selectedEmployeeLevelFilter = string.Empty;
        private int? selectedStatusFilter;
        private bool isMobileScheduler;
        private string mobileSelectedEmployeeId = string.Empty;
        private string mobileSelectedRoom = string.Empty;

        private class TimeSlotInfo
        {
            public string DisplayText { get; set; } = "";
            public int Hour { get; set; }
            public int Minute { get; set; }
            public bool ShowLabel { get; set; }
        }

        private List<TimeSlotInfo> TimeSlots = new();

        private bool showModal = false;
        private Appointment editingAppointment = new();
        private Staff? selectedStaff;
        private bool isSidebarExpanded = false;
        private bool showMobileFilters = false;
        private Staff? dragStartStaff;
        private int? dragStartHour;
        private int? dragStartMinute;
        private Appointment? draggedAppointment;
        private bool isAppointmentDragging = false;
        private int? draggingAppointmentId;
        private int draggingAppointmentDurationMinutes = 0;
        private int dragOffsetMinutes = 0;
        private Staff? dragTargetStaff;
        private int? dragTargetHour;
        private int? dragTargetMinute;
        private string? dragTargetRoom;
        private bool wasDragging = false;
        private bool isDeleteDropOver = false;
        private bool showMobileAppointmentSheet = false;
        private bool showMobileDeleteConfirmation = false;
        private bool isMobileAppointmentDeleting = false;
        private Appointment? mobileActionAppointment;

        // Time range selection state
        private bool isSelectingTimeRange = false;
        private Staff? selectionStaff;
        private string? selectionRoom;
        private TimeSlotInfo? selectionStartSlot;
        private TimeSlotInfo? selectionEndSlot;
        private DateTime? mouseDownTime;
        private bool hasMouseMoved = false;
        private const int CLICK_THRESHOLD_MS = 200;
        private const int MOVE_THRESHOLD_PX = 5;

        private bool showDateSelector = false;
        private bool focusDateInputPending = false;
        private bool showPickerSupportChecked = false;
        private bool supportsShowPicker = false;
        private ElementReference dateInputRef;

        private enum SidebarView
        {
            QuickActions,
            StaffVisibility,
            Queue,
            Settings
        }

        private SidebarView currentSidebarView = SidebarView.QuickActions;

        private enum QueueStatus
        {
            Waiting,
            Served,
            Cancelled
        }

        private QueueStatus selectedQueueTab = QueueStatus.Waiting;

        private class QueueItem
        {
            public int Id { get; set; }
            public string CustomerName { get; set; } = "";
            public string Phone { get; set; } = "";
            public string Service { get; set; } = "";
            public string Remarks { get; set; } = "";
            public QueueStatus Status { get; set; } = QueueStatus.Waiting;
            public DateTime AddedAt { get; set; } = DateTime.Now;
            public DateTime? ServedAt { get; set; }
            public DateTime? CancelledAt { get; set; }
            public string? ServedBy { get; set; }
        }

        private List<QueueItem> queueItems = new();
        private int nextQueueId = 1;
        private bool showQueueModal = false;
        private QueueItem newQueueItem = new();
        private bool showQueueDetailsModal = false;
        private QueueItem? selectedQueueItem;

        // Settings modal
        private bool showAdvancedOption = false;
        private bool showWorkingHoursOnly = true;
        private DateTime settingsCalendarDate = DateTime.Today;

        // Advanced options
        private enum ViewType
        {
            DayView,
            ListView,
            TimelineView
        }

        private enum GroupByOption
        {
            None,
            Staff,
            Room
        }

        private enum TimeScaleOption
        {
            OneHour,
            ThirtyMinutes,
            FifteenMinutes,
            TenMinutes,
            FiveMinutes
        }

        private ViewType currentViewType = ViewType.DayView;
        private GroupByOption currentGroupBy = GroupByOption.Staff;
        private TimeScaleOption currentTimeScale = TimeScaleOption.OneHour;
        private bool showDayViewDropdown = false;
        private bool showTimelineViewDropdown = false;
        private bool showTimeScaleDropdown = false;
        private bool IsTimeScaleControlEnabled => currentViewType == ViewType.DayView || currentViewType == ViewType.TimelineView;
        private bool IsWorkingHoursControlEnabled => currentViewType == ViewType.DayView || currentViewType == ViewType.TimelineView;
        private const int WorkingHoursStartMinutes = 9 * 60;
        private const int WorkingHoursEndMinutes = 19 * 60;
        private const int DefaultAppointmentDurationMinutes = 60;

        private void ToggleSidebar()
        {
            isSidebarExpanded = !isSidebarExpanded;
        }

        private void ToggleMobileFilters()
        {
            showMobileFilters = !showMobileFilters;
        }

        private void OpenQuickActions()
        {
            showMobileFilters = false;
            currentSidebarView = SidebarView.QuickActions;
            isSidebarExpanded = true;
        }

        private TimeScaleOption SelectedTimeScale
        {
            get => currentTimeScale;
            set
            {
                if (currentTimeScale != value)
                {
                    SetTimeScale(value);
                }
            }
        }

        private void GoBackToQuickActions()
        {
            currentSidebarView = SidebarView.QuickActions;
            StateHasChanged();
        }

        private void CloseSettingsSidebar()
        {
            isSidebarExpanded = false;
            currentSidebarView = SidebarView.QuickActions;
            StateHasChanged();
        }

        private void ShowStaffVisibility()
        {
            currentSidebarView = SidebarView.StaffVisibility;
            if (!isSidebarExpanded)
            {
                isSidebarExpanded = true;
            }
            StateHasChanged();
        }

        private void CloseStaffSidebar()
        {
            isSidebarExpanded = false;
            currentSidebarView = SidebarView.QuickActions;
            StateHasChanged();
        }

        private void ShowQueue()
        {
            currentSidebarView = SidebarView.Queue;
            if (!isSidebarExpanded)
            {
                isSidebarExpanded = true;
            }
            StateHasChanged();
        }

        private void CloseQueueSidebar()
        {
            isSidebarExpanded = false;
            currentSidebarView = SidebarView.QuickActions;
            StateHasChanged();
        }

        private void AddToQueue()
        {
            newQueueItem = new QueueItem
            {
                Status = QueueStatus.Waiting,
                AddedAt = DateTime.Now
            };
            showQueueModal = true;
            StateHasChanged();
        }

        private void SaveQueueItem()
        {
            if (string.IsNullOrWhiteSpace(newQueueItem.CustomerName))
            {
                Feedback.Warning("Customer name is required before adding to the queue.", "Queue item not added");
                return;
            }

            newQueueItem.Id = nextQueueId++;
            queueItems.Add(newQueueItem);
            var customerName = newQueueItem.CustomerName;
            CloseQueueModal();
            Feedback.Success($"{customerName} was added to the waiting queue.", "Added to queue", 3200);
            StateHasChanged();
        }

        private void CloseQueueModal()
        {
            showQueueModal = false;
            newQueueItem = new QueueItem();
            StateHasChanged();
        }

        private void OpenQueueItemModal(QueueItem item)
        {
            selectedQueueItem = item;
            showQueueDetailsModal = true;
            StateHasChanged();
        }

        private void CloseQueueItemModal()
        {
            showQueueDetailsModal = false;
            selectedQueueItem = null;
            StateHasChanged();
        }

        private void SetQueueTab(QueueStatus status)
        {
            selectedQueueTab = status;
            StateHasChanged();
        }

        private List<QueueItem> GetFilteredQueueItems()
        {
            return queueItems.Where(q => q.Status == selectedQueueTab).OrderBy(q => q.AddedAt).ToList();
        }

        private int GetQueueCount(QueueStatus status)
        {
            return queueItems.Count(q => q.Status == status);
        }

        private string GetWaitingTime(QueueItem item)
        {
            var elapsed = DateTime.Now - item.AddedAt;
            if (elapsed.TotalMinutes < 1)
                return "Just Now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes} min";
            return $"{(int)elapsed.TotalHours} hr {(int)(elapsed.TotalMinutes % 60)} min";
        }

        private void ServeQueueItem(QueueItem item)
        {
            item.Status = QueueStatus.Served;
            item.ServedAt = DateTime.Now;
            item.ServedBy = "Sara";
            CloseQueueItemModalIfNeeded(item);
            Feedback.Success($"{item.CustomerName} is now marked as served.", "Queue updated", 3000);
            StateHasChanged();
        }

        private void CancelQueueItem(QueueItem item)
        {
            item.Status = QueueStatus.Cancelled;
            item.CancelledAt = DateTime.Now;
            CloseQueueItemModalIfNeeded(item);
            Feedback.Info($"{item.CustomerName} was removed from the active queue.", "Queue item cancelled", 3000);
            StateHasChanged();
        }

        private void CloseQueueItemModalIfNeeded(QueueItem item)
        {
            if (selectedQueueItem?.Id == item.Id)
            {
                CloseQueueItemModal();
            }
        }

        private void ShowSettings()
        {
            currentSidebarView = SidebarView.Settings;
            if (!isSidebarExpanded)
            {
                isSidebarExpanded = true;
            }
            settingsCalendarDate = SelectedDate;
            StateHasChanged();
        }


        private void OnSettingsDateSelected(DateTime date)
        {
            settingsCalendarDate = date;
            SelectedDate = date;
            StateHasChanged();
        }

        private List<Appointment> GetAppointmentsForDate(DateTime date)
        {
            return ViewModel.Appointments
                .Where(a => a.Start.Date == date.Date)
                .OrderBy(a => a.Start)
                .ToList();
        }

        private void NavigateCalendarMonth(int months)
        {
            settingsCalendarDate = settingsCalendarDate.AddMonths(months);
            StateHasChanged();
        }

        private void GoToTodayInSettings()
        {
            settingsCalendarDate = DateTime.Today;
            SelectedDate = DateTime.Today;
            StateHasChanged();
        }

        private void SetViewType(ViewType viewType)
        {
            currentViewType = viewType;
            if (!IsTimeScaleControlEnabled)
            {
                showTimeScaleDropdown = false;
            }
            StateHasChanged();
        }

        private void SetGroupBy(GroupByOption groupBy)
        {
            currentGroupBy = groupBy;
            showDayViewDropdown = false;
            showTimelineViewDropdown = false;
            StateHasChanged();
        }

        private void SetTimeScale(TimeScaleOption timeScale)
        {
            currentTimeScale = timeScale;
            showTimeScaleDropdown = false;
            RebuildTimeSlots();
            StateHasChanged();
        }

        private void ToggleTimeScaleDropdown()
        {
            if (!IsTimeScaleControlEnabled)
            {
                return;
            }

            showTimeScaleDropdown = !showTimeScaleDropdown;
            StateHasChanged();
        }

        private void OnWorkingHoursClicked()
        {
            if (!IsWorkingHoursControlEnabled)
            {
                return;
            }

            showWorkingHoursOnly = !showWorkingHoursOnly;
            RebuildTimeSlots();
            StateHasChanged();
        }

        private string GetGroupByText()
        {
            return currentGroupBy switch
            {
                GroupByOption.None => "None",
                GroupByOption.Staff => "Staff",
                GroupByOption.Room => "Room",
                _ => "None"
            };
        }

        private string GetTimeScaleText()
        {
            return currentTimeScale switch
            {
                TimeScaleOption.OneHour => "1 hour",
                TimeScaleOption.ThirtyMinutes => "30 minutes",
                TimeScaleOption.FifteenMinutes => "15 minutes",
                TimeScaleOption.TenMinutes => "10 minutes",
                TimeScaleOption.FiveMinutes => "5 minutes",
                _ => "1 hour"
            };
        }

        private void CloseDropdowns()
        {
            showDayViewDropdown = false;
            showTimelineViewDropdown = false;
            showTimeScaleDropdown = false;
            StateHasChanged();
        }

        private void OnAdvancedOptionChanged()
        {
            if (!showAdvancedOption)
            {
                CloseDropdowns();
            }
        }

        private void OnQueueItemClick(QueueItem item)
        {
            if (item.Status == QueueStatus.Waiting)
            {
                OpenQueueItemModal(item);
            }
        }

        private int GetTimeScaleMinutes()
        {
            return currentTimeScale switch
            {
                TimeScaleOption.OneHour => 60,
                TimeScaleOption.ThirtyMinutes => 30,
                TimeScaleOption.FifteenMinutes => 15,
                TimeScaleOption.TenMinutes => 10,
                TimeScaleOption.FiveMinutes => 5,
                _ => 30
            };
        }

        private int GetScheduleStartMinutes()
        {
            return showWorkingHoursOnly ? WorkingHoursStartMinutes : 0;
        }

        private int GetScheduleEndMinutes()
        {
            return showWorkingHoursOnly ? WorkingHoursEndMinutes : 24 * 60;
        }

        private List<TimeSlotInfo> GenerateTimeSlots()
        {
            var slots = new List<TimeSlotInfo>();
            var minutesPerSlot = GetTimeScaleMinutes();
            var scheduleStartMinutes = GetScheduleStartMinutes();
            var scheduleEndMinutes = GetScheduleEndMinutes();
            var adjustedEndMinutes = scheduleStartMinutes +
                                     (int)Math.Ceiling((scheduleEndMinutes - scheduleStartMinutes) / (double)minutesPerSlot) * minutesPerSlot;

            for (var minutes = scheduleStartMinutes; minutes <= adjustedEndMinutes; minutes += minutesPerSlot)
            {
                var hour = minutes / 60;
                var minute = minutes % 60;
                var displayTime = DateTime.Today.AddMinutes(minutes).ToString("h:mm tt");

                if (minutes > scheduleStartMinutes && minute == 0 && (hour % 24) == 0)
                {
                    continue;
                }

                slots.Add(new TimeSlotInfo
                {
                    Hour = hour,
                    Minute = minute,
                    ShowLabel = minute == 0,
                    DisplayText = displayTime
                });
            }

            return slots;
        }

        private void RebuildTimeSlots()
        {
            TimeSlots = GenerateTimeSlots();
            ResetTimeRangeSelection();
        }

        private DateOnly StartDateValue
        {
            get
            {
                var start = editingAppointment?.Start ?? SelectedDate;
                return DateOnly.FromDateTime(start);
            }
            set
            {
                if (editingAppointment == null) return;
                if (editingAppointment.IsAllDay)
                {
                    editingAppointment.Start = value.ToDateTime(new TimeOnly(9, 0));
                    editingAppointment.End = value.ToDateTime(new TimeOnly(18, 0));
                    return;
                }
                var time = TimeOnly.FromDateTime(editingAppointment.Start);
                editingAppointment.Start = value.ToDateTime(time);
                EnsureEndAfterStart();
            }
        }

        private TimeOnly StartTimeValue
        {
            get
            {
                var start = editingAppointment?.Start ?? SelectedDate.AddHours(9);
                return TimeOnly.FromDateTime(start);
            }
            set
            {
                if (editingAppointment == null) return;
                var date = DateOnly.FromDateTime(editingAppointment.Start);
                editingAppointment.Start = date.ToDateTime(value);
                EnsureEndAfterStart();
            }
        }

        private DateOnly EndDateValue
        {
            get
            {
                var end = editingAppointment?.End ?? SelectedDate.AddHours(10);
                return DateOnly.FromDateTime(end);
            }
            set
            {
                if (editingAppointment == null) return;
                if (editingAppointment.IsAllDay)
                {
                    editingAppointment.End = editingAppointment.Start.Date.AddHours(18);
                    return;
                }
                var time = TimeOnly.FromDateTime(editingAppointment.End);
                editingAppointment.End = value.ToDateTime(time);
                EnsureEndAfterStart();
            }
        }

        private TimeOnly EndTimeValue
        {
            get
            {
                var end = editingAppointment?.End ?? SelectedDate.AddHours(10);
                return TimeOnly.FromDateTime(end);
            }
            set
            {
                if (editingAppointment == null) return;
                var date = DateOnly.FromDateTime(editingAppointment.End);
                editingAppointment.End = date.ToDateTime(value);
                EnsureEndAfterStart();
            }
        }


        protected override async Task OnInitializedAsync()
        {
            ViewModel = new AppointmentViewModel(AppointmentService);
            AppointmentService.OnChange += OnAppointmentServiceChanged;
            RebuildTimeSlots();
            queueItems = new List<QueueItem>();
            nextQueueId = 1;

            await LoadReferenceDataAsync();
            await ReloadAppointmentsAsync();
        }

        private async Task LoadReferenceDataAsync()
        {
            var errors = new List<string>();

            var employeeResult = await EmployeeService.GetAllEmployeesAsync();
            if (employeeResult.Success && employeeResult.Value is not null)
            {
                var colors = new[] { "sara", "rin", "ken" };
                StaffList = employeeResult.Value
                    .Where(employee => string.Equals(employee.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    .Where(employee => !string.IsNullOrWhiteSpace(employee.Code) && !string.IsNullOrWhiteSpace(employee.Name))
                    .GroupBy(employee => employee.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Select((employee, index) => new Staff
                    {
                        Id = employee.Code,
                        Name = employee.Name,
                        Role = employee.EmployeeLevel,
                        Color = colors[index % colors.Length]
                    })
                    .Where(staff => !string.IsNullOrWhiteSpace(staff.Id) && !string.IsNullOrWhiteSpace(staff.Name))
                    .ToList();
            }
            else
            {
                errors.Add(employeeResult.ErrorMessage ?? "Unable to load employees.");
            }

            var customerResult = await CustomerService.SearchCustomersAsync(string.Empty);
            if (customerResult.Success && customerResult.Value is not null)
            {
                customerOptions = customerResult.Value
                    .Where(customer => string.Equals(customer.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(customer => customer.FirstName)
                    .ToList();
            }
            else
            {
                errors.Add(customerResult.ErrorMessage ?? "Unable to load customers.");
            }

            var serviceResult = await ServiceItemService.LoadServiceItemsAsync("HQ");
            if (serviceResult.Success && serviceResult.Value is not null)
            {
                serviceOptions = serviceResult.Value
                    .Where(service => string.Equals(service.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(service => service.ServiceName)
                    .ToList();
                ServicesList = serviceOptions.Select(service => service.ServiceName).Distinct().ToList();
            }
            else
            {
                errors.Add(serviceResult.ErrorMessage ?? "Unable to load services.");
            }

            var branchResult = await BranchLookupService.LoadBranchesAsync();
            if (branchResult.Success && branchResult.Value is not null)
            {
                branchOptions = branchResult.Value;
            }
            else
            {
                errors.Add(branchResult.ErrorMessage ?? "Unable to load branches.");
            }

            var statusResult = await AppointmentService.LoadStatusesAsync();
            if (statusResult.Success && statusResult.Value is not null)
            {
                appointmentStatuses = statusResult.Value;
            }
            else
            {
                errors.Add(statusResult.ErrorMessage ?? "Unable to load appointment statuses.");
            }

            staffVisibility.Clear();
            foreach (var staff in StaffList)
            {
                staffVisibility[staff.Id] = true;
            }

            appointmentError = errors.Count == 0 ? null : string.Join(" ", errors.Distinct());
        }

        private async Task ReloadAppointmentsAsync()
        {
            if (ViewModel is null) return;
            if (isAppointmentLoading)
            {
                appointmentReloadPending = true;
                return;
            }

            isAppointmentLoading = true;
            try
            {
                do
                {
                    appointmentReloadPending = false;
                    var result = !string.IsNullOrWhiteSpace(selectedCustomerFilterId)
                        ? await ViewModel.LoadByCustomerAsync(selectedCustomerFilterId)
                        : !string.IsNullOrWhiteSpace(selectedEmployeeFilterId) && selectedStatusFilter.HasValue
                            ? await ViewModel.LoadByEmployeeAndStatusAsync(selectedEmployeeFilterId, selectedStatusFilter.Value)
                            : !string.IsNullOrWhiteSpace(selectedEmployeeFilterId)
                                ? await ViewModel.LoadByEmployeeAsync(selectedEmployeeFilterId)
                                : await ViewModel.LoadAppointmentsAsync();

                    if (!result.Success)
                    {
                        appointmentError = result.ErrorMessage ?? "Unable to load appointments.";
                        return;
                    }

                    NormalizeAppointmentDisplayValues();
                    EnsureMobileEmployeeSelection();
                    appointmentError = null;
                }
                while (appointmentReloadPending);
            }
            catch (Exception exception)
            {
                appointmentError = $"Unable to load appointments: {exception.Message}";
            }
            finally
            {
                isAppointmentLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }



        private async Task ClearAppointmentFiltersAsync()
        {
            selectedCustomerFilterId = string.Empty;
            selectedEmployeeFilterId = string.Empty;
            selectedEmployeeLevelFilter = string.Empty;
            selectedStatusFilter = null;
            await ReloadAppointmentsAsync();
        }

        private void NormalizeAppointmentDisplayValues()
        {
            foreach (var appointment in ViewModel.Appointments)
            {
                ConstrainAppointmentToWorkingHours(appointment, preserveDuration: false);

                var employee = StaffList.FirstOrDefault(staff =>
                    string.Equals(staff.Id.Trim(), appointment.EmployeeId?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (employee is null && string.IsNullOrWhiteSpace(appointment.EmployeeId))
                {
                    employee = StaffList.FirstOrDefault(staff =>
                        string.Equals(staff.Id.Trim(), appointment.StaffName?.Trim(), StringComparison.OrdinalIgnoreCase))
                        ?? StaffList.FirstOrDefault(staff =>
                            string.Equals(staff.Name.Trim(), appointment.StaffName?.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (employee is not null)
                {
                    appointment.EmployeeId = employee.Id;
                    appointment.StaffName = employee.Name;
                }

                var customer = customerOptions.FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(appointment.CustomerId) &&
                    string.Equals(item.SystemID, appointment.CustomerId, StringComparison.OrdinalIgnoreCase))
                    ?? customerOptions.FirstOrDefault(item =>
                        string.Equals($"{item.FirstName} {item.LastName}".Trim(), appointment.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (customer is not null)
                {
                    appointment.CustomerId = customer.SystemID ?? string.Empty;
                    appointment.CustomerName = $"{customer.FirstName} {customer.LastName}".Trim();
                    appointment.Phone = customer.ContactNumber1;
                }

                EnsureRoomOption(appointment.Room);
            }
        }

        private void OnCustomerSelectionChanged()
        {
            if (editingAppointment is null) return;

            var customer = customerOptions.FirstOrDefault(item =>
                string.Equals($"{item.FirstName} {item.LastName}".Trim(), editingAppointment.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase));

            editingAppointment.CustomerId = customer?.SystemID ?? string.Empty;
            editingAppointment.Phone = customer?.ContactNumber1 ?? string.Empty;
        }

        private void EnsureRoomOption(string? room)
        {
            if (string.IsNullOrWhiteSpace(room) || RoomList.Contains(room.Trim(), StringComparer.OrdinalIgnoreCase)) return;
            RoomList.Add(room.Trim());
        }

        private void PrepareAppointmentReferences(Appointment appointment)
        {
            EnsureRoomOption(appointment.Room);
            var employee = StaffList.FirstOrDefault(staff =>
                string.Equals(staff.Id, appointment.EmployeeId, StringComparison.OrdinalIgnoreCase));

            // Name matching is only for legacy appointments that have no employee ID.
            // It must never override an explicit selection because names are not unique.
            if (employee is null && string.IsNullOrWhiteSpace(appointment.EmployeeId))
            {
                employee = StaffList.FirstOrDefault(staff =>
                    string.Equals(staff.Id, appointment.StaffName, StringComparison.OrdinalIgnoreCase))
                    ?? StaffList.FirstOrDefault(staff =>
                        string.Equals(staff.Name, appointment.StaffName, StringComparison.OrdinalIgnoreCase));
            }
            if (employee is not null)
            {
                appointment.EmployeeId = employee.Id;
                appointment.StaffName = employee.Name;
            }

            var customer = customerOptions.FirstOrDefault(item =>
                string.Equals($"{item.FirstName} {item.LastName}".Trim(), appointment.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (customer is not null)
            {
                appointment.CustomerId = customer.SystemID ?? string.Empty;
                appointment.Phone = customer.ContactNumber1;
            }

                EnsureRoomOption(appointment.Room);

            var service = serviceOptions.FirstOrDefault(item =>
                string.Equals(item.ServiceName, appointment.ServiceItem, StringComparison.OrdinalIgnoreCase));
            if (service is not null)
            {
                appointment.ServiceInventoryId = service.MasterAccountId ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(appointment.BranchId))
            {
                appointment.BranchId = "HQ";
            }

            appointment.BranchId = appointment.BranchId.Trim();
            appointment.Location = appointment.BranchId;
        }


        private string GetStaffHeaderClass(Staff staff)
        {
            return staff.Color switch
            {
                "sara" => "staff-header-sara",
                "rin" => "staff-header-rin",
                "ken" => "staff-header-ken",
                _ => ""
            };
        }

        private string GetRoomHeaderClass(string room)
        {
            var roomIndex = RoomList.IndexOf(room);
            return roomIndex switch
            {
                0 => "staff-header-sara",  // Room A
                1 => "staff-header-rin",   // Room B
                2 => "staff-header-ken",   // Room C
                _ => "staff-header-sara"   // Default
            };
        }

        private string GetDayOfWeekText()
        {
            return ViewModel.SelectedDate.ToString("dddd, dd").ToUpper();
        }

        private string GetDateRangeText()
        {
            var endDate = ViewModel.SelectedDate.AddDays(1);
            return $"{ViewModel.SelectedDate:MMMM dd} - {endDate:dd, yyyy}";
        }

        private static bool IsAppointmentForStaff(Appointment appointment, Staff staff)
        {
            return !string.IsNullOrWhiteSpace(appointment.EmployeeId) &&
                   string.Equals(appointment.EmployeeId.Trim(), staff.Id.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureMobileEmployeeSelection()
        {
            if (!isMobileScheduler || currentGroupBy != GroupByOption.Staff) return;

            var visibleStaff = GetVisibleStaff();
            if (visibleStaff.Count == 0)
            {
                mobileSelectedEmployeeId = string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedEmployeeFilterId))
            {
                var filteredStaff = visibleStaff.FirstOrDefault(staff =>
                    string.Equals(staff.Id.Trim(), selectedEmployeeFilterId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (filteredStaff is not null)
                {
                    mobileSelectedEmployeeId = filteredStaff.Id;
                    return;
                }
            }

            var scheduledEmployeeIds = ViewModel.Appointments
                .Where(appointment => appointment.Start.Date == SelectedDate.Date)
                .Select(appointment => appointment.EmployeeId?.Trim())
                .Where(employeeId => !string.IsNullOrWhiteSpace(employeeId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (scheduledEmployeeIds.Contains(mobileSelectedEmployeeId.Trim())) return;

            var firstScheduledStaff = visibleStaff.FirstOrDefault(staff => scheduledEmployeeIds.Contains(staff.Id.Trim()));
            mobileSelectedEmployeeId = firstScheduledStaff?.Id ?? visibleStaff[0].Id;
        }
        private IReadOnlyList<Staff> GetVisibleStaff()
        {
            return StaffList.FindAll(staff => MatchesEmployeeLevelFilter(staff) && IsStaffVisible(staff));
        }

        private IReadOnlyList<Staff> GetStaffBySelectedLevel()
        {
            return StaffList.FindAll(MatchesEmployeeLevelFilter);
        }

        private IReadOnlyList<string> GetEmployeeLevelOptions()
        {
            return StaffList
                .Select(staff => NormalizeEmployeeLevel(staff.Role))
                .Where(level => !string.IsNullOrWhiteSpace(level))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(level => level)
                .ToList();
        }

        private int GetEmployeeLevelCount(string level)
        {
            return StaffList.Count(staff =>
                string.Equals(NormalizeEmployeeLevel(staff.Role), level, StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesEmployeeLevelFilter(Staff staff)
        {
            return string.IsNullOrWhiteSpace(selectedEmployeeLevelFilter) ||
                   string.Equals(NormalizeEmployeeLevel(staff.Role), selectedEmployeeLevelFilter, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeEmployeeLevel(string? level)
        {
            return string.IsNullOrWhiteSpace(level) ? "Unassigned" : level.Trim();
        }

        private async Task OnEmployeeLevelFilterChangedAsync()
        {
            await ApplyEmployeeLevelFilterAsync();
        }

        private async Task SetEmployeeLevelFilterAsync(string level)
        {
            selectedEmployeeLevelFilter = level;
            await ApplyEmployeeLevelFilterAsync();
        }

        private async Task ApplyEmployeeLevelFilterAsync()
        {
            if (!string.IsNullOrWhiteSpace(selectedEmployeeFilterId) &&
                !GetStaffBySelectedLevel().Any(staff =>
                    string.Equals(staff.Id.Trim(), selectedEmployeeFilterId.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                selectedEmployeeFilterId = string.Empty;
                selectedStatusFilter = null;
            }

            EnsureMobileEmployeeSelection();
            await ReloadAppointmentsAsync();
        }

        private bool IsStaffVisible(Staff staff)
        {
            if (staffVisibility.TryGetValue(staff.Id, out var isVisible))
            {
                return isVisible;
            }

            return true;
        }

        private IReadOnlyList<string> GetVisibleRooms()
        {
            return RoomList;
        }

        private void SelectMobileEmployee(string employeeId)
        {
            mobileSelectedEmployeeId = employeeId;
            StateHasChanged();
        }

        private void SelectMobileRoom(string room)
        {
            mobileSelectedRoom = room;
            StateHasChanged();
        }
        private bool IsAllStaffSelected
        {
            get
            {
                var filteredStaff = GetStaffBySelectedLevel();
                return filteredStaff.Count > 0 && filteredStaff.All(IsStaffVisible);
            }
        }

        private void OnStaffVisibilityChanged(Staff staff, ChangeEventArgs e)
        {
            var isChecked = ParseCheckboxValue(e);
            staffVisibility[staff.Id] = isChecked;
            StateHasChanged();
        }

        private void OnToggleAllStaff(ChangeEventArgs e)
        {
            var isChecked = ParseCheckboxValue(e);
            foreach (var staff in GetStaffBySelectedLevel())
            {
                staffVisibility[staff.Id] = isChecked;
            }
            EnsureMobileEmployeeSelection();
            StateHasChanged();
        }

        private static bool ParseCheckboxValue(ChangeEventArgs e)
        {
            if (e.Value is bool boolValue)
            {
                return boolValue;
            }

            if (e.Value is string stringValue)
            {
                return string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(stringValue, "on", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private string GetStaffInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "S";
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpperInvariant();
            }

            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }

        private string GetStaffAccentClass(Staff staff)
        {
            return staff.Color switch
            {
                "sara" => "staff-accent-blue",
                "rin" => "staff-accent-green",
                "ken" => "staff-accent-amber",
                _ => "staff-accent-slate"
            };
        }

        private void PreviousDate()
        {
            SelectedDate = SelectedDate.AddDays(-1);
            CloseDateSelector();
        }

        private void NextDate()
        {
            SelectedDate = SelectedDate.AddDays(1);
            CloseDateSelector();
        }

        private void ToggleDateSelector()
        {
            showDateSelector = !showDateSelector;
            if (showDateSelector)
            {
                focusDateInputPending = true;
            }
        }

        private void CloseDateSelector()
        {
            showDateSelector = false;
            focusDateInputPending = false;
        }

        private void OnDateSelected(ChangeEventArgs e)
        {
            if (e.Value is string value && DateTime.TryParse(value, out var selected))
            {
                SelectedDate = selected;
            }

            CloseDateSelector();
        }

        private void OnDragStart(DragEventArgs e, Appointment appointment, Staff staff, int hour, int minute)
        {
            wasDragging = true;
            draggedAppointment = appointment;
            isAppointmentDragging = true;
            draggingAppointmentId = appointment.Id;
            draggingAppointmentDurationMinutes = (int)(appointment.End - appointment.Start).TotalMinutes;
            dragStartStaff = staff;
            dragStartHour = hour;
            dragStartMinute = minute;
            var slotStart = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute);
            dragOffsetMinutes = (int)(appointment.Start - slotStart).TotalMinutes;
            e.DataTransfer.EffectAllowed = "move";
            CloseDateSelector();
            // Prevent time range selection during appointment drag
            ResetTimeRangeSelection();
        }

        private void OnDragEnter(DragEventArgs e, Staff staff, int hour, int minute)
        {
            if (!isAppointmentDragging)
            {
                return;
            }
            wasDragging = true;
            dragTargetStaff = staff;
            dragTargetHour = hour;
            dragTargetMinute = minute;
            CloseDateSelector();
            StateHasChanged();
        }

        private void OnDragLeave(DragEventArgs e, Staff staff, TimeSlotInfo timeSlot)
        {
            if (!isAppointmentDragging)
            {
                return;
            }
            if (dragTargetStaff?.Id != staff.Id ||
                dragTargetHour != timeSlot.Hour ||
                dragTargetMinute != timeSlot.Minute)
            {
                return;
            }
            dragTargetStaff = null;
            dragTargetHour = null;
            dragTargetMinute = null;
            StateHasChanged();
        }

        private void OnDragOver(DragEventArgs e)
        {
            if (e.DataTransfer != null)
            {
                e.DataTransfer.DropEffect = "move";
            }
        }

        private void OnDeleteDragEnter(DragEventArgs e)
        {
            if (!isAppointmentDragging || draggedAppointment is null)
            {
                return;
            }

            isDeleteDropOver = true;
            dragTargetStaff = null;
            dragTargetRoom = null;
            dragTargetHour = null;
            dragTargetMinute = null;
            StateHasChanged();
        }

        private void OnDeleteDragLeave(DragEventArgs e)
        {
            if (!isDeleteDropOver)
            {
                return;
            }

            isDeleteDropOver = false;
            StateHasChanged();
        }

        private async Task OnDeleteDrop(DragEventArgs e)
        {
            var appointment = draggedAppointment;
            isDeleteDropOver = false;
            ResetDragState();

            if (appointment is null)
            {
                return;
            }

            if (!IsCancelledAppointment(appointment))
            {
                appointmentError = "Cancel the appointment before deleting it.";
                Feedback.Warning(appointmentError, "Appointment not deleted");
                await InvokeAsync(StateHasChanged);
                return;
            }

            var customer = string.IsNullOrWhiteSpace(appointment.CustomerName)
                ? "this customer"
                : appointment.CustomerName;
            var confirmed = await JS.InvokeAsync<bool>(
                "confirm",
                $"Delete the appointment for {customer} on {appointment.Start:dd/MM/yyyy at hh:mm tt}? This will permanently remove it from the active schedule.");

            if (!confirmed)
            {
                return;
            }

            isAppointmentSaving = true;
            appointmentError = null;

            try
            {
                var result = await ViewModel.DeleteAppointmentAsync(appointment);
                if (!result.Success)
                {
                    appointmentError = result.ErrorMessage ?? "Unable to delete the appointment.";
                    Feedback.Error(appointmentError, "Appointment not deleted");
                    return;
                }

                NormalizeAppointmentDisplayValues();
                Feedback.Success("Appointment deleted successfully.", "Appointment deleted");
            }
            catch (Exception exception)
            {
                appointmentError = $"Unable to delete the appointment: {exception.Message}";
                Feedback.Error(appointmentError, "Appointment not deleted");
            }
            finally
            {
                isAppointmentSaving = false;
                await InvokeAsync(StateHasChanged);
            }
        }
        private async Task OnDrop(DragEventArgs e, Staff targetStaff, int targetHour, int targetMinute)
        {
            var appointment = draggedAppointment;
            if (appointment != null)
            {
                var previousEmployeeId = appointment.EmployeeId;
                var previousStaffName = appointment.StaffName;
                var previousStart = appointment.Start;
                var previousEnd = appointment.End;
                var newStart = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute).AddMinutes(dragOffsetMinutes);
                var duration = appointment.End - appointment.Start;

                appointment.EmployeeId = targetStaff.Id;
                appointment.StaffName = targetStaff.Name;
                appointment.Start = newStart;
                appointment.End = newStart.Add(duration);
                ConstrainAppointmentToWorkingHours(appointment, preserveDuration: true);
                ResetDragState();

                if (!await PersistMovedAppointmentAsync(appointment))
                {
                    appointment.EmployeeId = previousEmployeeId;
                    appointment.StaffName = previousStaffName;
                    appointment.Start = previousStart;
                    appointment.End = previousEnd;
                    await InvokeAsync(StateHasChanged);
                }

                return;
            }

            var slotDuration = GetTimeScaleMinutes();
            editingAppointment = new Appointment
            {
                EmployeeId = targetStaff.Id,
                StaffName = targetStaff.Name,
                Start = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute),
                End = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute).AddMinutes(slotDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
            ResetDragState();
        }
        private void OnDragEnd(DragEventArgs e)
        {
            if (isAppointmentDragging)
            {
                ResetDragState();
            }
        }

        private void OnDragStartRoom(DragEventArgs e, Appointment appointment, string room, int hour, int minute)
        {
            wasDragging = true;
            draggedAppointment = appointment;
            isAppointmentDragging = true;
            draggingAppointmentId = appointment.Id;
            draggingAppointmentDurationMinutes = (int)(appointment.End - appointment.Start).TotalMinutes;
            dragStartHour = hour;
            dragStartMinute = minute;
            var slotStart = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute);
            dragOffsetMinutes = (int)(appointment.Start - slotStart).TotalMinutes;
            e.DataTransfer.EffectAllowed = "move";
            CloseDateSelector();
            ResetTimeRangeSelection();
        }

        private void OnDragEnterRoom(DragEventArgs e, string room, int hour, int minute)
        {
            if (!isAppointmentDragging)
            {
                return;
            }
            wasDragging = true;
            dragTargetRoom = room;
            dragTargetHour = hour;
            dragTargetMinute = minute;
            CloseDateSelector();
            StateHasChanged();
        }

        private void OnDragLeaveRoom(DragEventArgs e, string room, TimeSlotInfo timeSlot)
        {
            if (!isAppointmentDragging)
            {
                return;
            }
            if (dragTargetRoom != room ||
                dragTargetHour != timeSlot.Hour ||
                dragTargetMinute != timeSlot.Minute)
            {
                return;
            }
            dragTargetRoom = null;
            dragTargetHour = null;
            dragTargetMinute = null;
            StateHasChanged();
        }

        private async Task OnDropRoom(DragEventArgs e, string targetRoom, int targetHour, int targetMinute)
        {
            var appointment = draggedAppointment;
            if (appointment != null)
            {
                var previousRoom = appointment.Room;
                var previousStart = appointment.Start;
                var previousEnd = appointment.End;
                var newStart = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute).AddMinutes(dragOffsetMinutes);
                var duration = appointment.End - appointment.Start;

                appointment.Room = targetRoom;
                appointment.Start = newStart;
                appointment.End = newStart.Add(duration);
                ConstrainAppointmentToWorkingHours(appointment, preserveDuration: true);
                ResetDragState();

                if (!await PersistMovedAppointmentAsync(appointment))
                {
                    appointment.Room = previousRoom;
                    appointment.Start = previousStart;
                    appointment.End = previousEnd;
                    await InvokeAsync(StateHasChanged);
                }

                return;
            }

            var slotDuration = GetTimeScaleMinutes();
            editingAppointment = new Appointment
            {
                Room = targetRoom,
                Start = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute),
                End = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute).AddMinutes(slotDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
            ResetDragState();
        }
        private void OnMouseDownRoom(MouseEventArgs e, string room, TimeSlotInfo timeSlot)
        {
            if (e.Button == 0 && !wasDragging && draggedAppointment == null)
            {
                var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
                var slotEnd = slotStart.AddMinutes(GetTimeScaleMinutes());
                var hasAppointments = ViewModel.Appointments
                    .Any(a => a.Room == room &&
                            a.Start.Date == ViewModel.SelectedDate.Date &&
                            a.Start >= slotStart && a.Start < slotEnd);

                if (!hasAppointments)
                {
                    mouseDownTime = DateTime.Now;
                    hasMouseMoved = false;
                    isSelectingTimeRange = false;
                    selectionRoom = room;
                    selectionStartSlot = timeSlot;
                    selectionEndSlot = timeSlot;
                    CloseDateSelector();
                }
            }
        }

        private void OnMouseMoveRoom(MouseEventArgs e, string room, TimeSlotInfo timeSlot)
        {
            if (mouseDownTime.HasValue && selectionRoom == room && draggedAppointment == null)
            {
                if (!hasMouseMoved)
                {
                    hasMouseMoved = true;
                }

                var timeSinceDown = (DateTime.Now - mouseDownTime.Value).TotalMilliseconds;
                if (timeSinceDown > CLICK_THRESHOLD_MS || hasMouseMoved)
                {
                    if (!isSelectingTimeRange)
                    {
                        isSelectingTimeRange = true;
                    }
                    selectionEndSlot = timeSlot;
                    StateHasChanged();
                }
            }
        }

        private void OnMouseUpRoom(MouseEventArgs e, string room, TimeSlotInfo timeSlot)
        {
            if (mouseDownTime.HasValue && selectionRoom == room)
            {
                var timeSinceDown = (DateTime.Now - mouseDownTime.Value).TotalMilliseconds;

                if (timeSinceDown < CLICK_THRESHOLD_MS && !hasMouseMoved && !isSelectingTimeRange)
                {
                    var selectedSlot = selectionStartSlot ?? timeSlot;
                    ResetTimeRangeSelection();
                    SelectTimeSlotForRoom(room, selectedSlot.Hour, selectedSlot.Minute);
                    return;
                }

                if (isSelectingTimeRange)
                {
                    CompleteTimeRangeSelectionForRoom(room, timeSlot);
                }
                else
                {
                    ResetTimeRangeSelection();
                }
            }
        }

        private void CompleteTimeRangeSelectionForRoom(string room, TimeSlotInfo? endSlot = null)
        {
            if (!isSelectingTimeRange || selectionStartSlot == null)
            {
                ResetTimeRangeSelection();
                return;
            }

            if (endSlot != null)
            {
                selectionEndSlot = endSlot;
            }

            if (selectionEndSlot == null)
            {
                selectionEndSlot = selectionStartSlot;
            }

            var slotDuration = GetTimeScaleMinutes();

            var startSlot = selectionStartSlot;
            var finalEndSlot = selectionEndSlot;

            var startTime = ViewModel.SelectedDate.AddHours(startSlot.Hour).AddMinutes(startSlot.Minute);
            var endTime = ViewModel.SelectedDate.AddHours(finalEndSlot.Hour).AddMinutes(finalEndSlot.Minute).AddMinutes(slotDuration);

            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }

            if ((endTime - startTime).TotalMinutes >= slotDuration)
            {
                editingAppointment = new Appointment
                {
                    Room = room,
                    Start = startTime,
                    End = endTime
                };
                appointmentCreateSubmitted = false;
                showModal = true;
            }

            ResetTimeRangeSelection();
        }

        private void ResetDragState()
        {
            dragStartStaff = null;
            dragStartHour = null;
            dragStartMinute = null;
            draggedAppointment = null;
            dragTargetStaff = null;
            dragTargetHour = null;
            dragTargetMinute = null;
            dragTargetRoom = null;
            dragOffsetMinutes = 0;
            isAppointmentDragging = false;
            draggingAppointmentId = null;
            draggingAppointmentDurationMinutes = 0;
            isDeleteDropOver = false;
            wasDragging = false;
            CloseDateSelector();
            StateHasChanged();
        }

        private bool ShouldShowDragPreviewInSlot(TimeSlotInfo timeSlot, Staff? staff = null, string? room = null)
        {
            if (!isAppointmentDragging || !dragTargetHour.HasValue || !dragTargetMinute.HasValue || draggingAppointmentDurationMinutes <= 0)
                return false;

            // Check if this slot matches the target staff/room
            if (staff != null && (dragTargetStaff == null ||
                                  !string.Equals(dragTargetStaff.Id, staff.Id, StringComparison.OrdinalIgnoreCase)))
                return false;
            if (room != null && dragTargetRoom != room)
                return false;

            var slotDuration = GetTimeScaleMinutes();
            var targetSlotStart = ViewModel.SelectedDate.AddHours(dragTargetHour.Value).AddMinutes(dragTargetMinute.Value);
            var previewStart = targetSlotStart.AddMinutes(dragOffsetMinutes);
            var previewEnd = previewStart.AddMinutes(draggingAppointmentDurationMinutes);

            var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
            var slotEnd = slotStart.AddMinutes(slotDuration);

            // Check if the preview overlaps with this slot
            return previewStart < slotEnd && previewEnd > slotStart;
        }

        private (double topPercent, double heightPercent) GetDragPreviewPositionInSlot(TimeSlotInfo timeSlot)
        {
            if (!isAppointmentDragging || !dragTargetHour.HasValue || !dragTargetMinute.HasValue || draggingAppointmentDurationMinutes <= 0)
                return (0, 0);

            var slotDuration = GetTimeScaleMinutes();
            var targetSlotStart = ViewModel.SelectedDate.AddHours(dragTargetHour.Value).AddMinutes(dragTargetMinute.Value);
            var previewStart = targetSlotStart.AddMinutes(dragOffsetMinutes);
            var previewEnd = previewStart.AddMinutes(draggingAppointmentDurationMinutes);

            var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
            var slotEnd = slotStart.AddMinutes(slotDuration);

            // Calculate how much of the preview is in this slot
            var previewStartInSlot = Math.Max(0, (previewStart - slotStart).TotalMinutes);
            var previewEndInSlot = Math.Min(slotDuration, (previewEnd - slotStart).TotalMinutes);
            var previewHeightInSlot = previewEndInSlot - previewStartInSlot;

            var topPercent = (previewStartInSlot / slotDuration) * 100.0;
            var heightPercent = (previewHeightInSlot / slotDuration) * 100.0;

            return (topPercent, heightPercent);
        }

        private void AddAppointment()
        {
            var defaultStartMinutes = GetScheduleStartMinutes();
            var defaultDuration = DefaultAppointmentDurationMinutes;
            var defaultStart = ViewModel.SelectedDate.Date.AddMinutes(defaultStartMinutes);
            var mobileStaff = isMobileScheduler
                ? GetVisibleStaff().FirstOrDefault(staff =>
                    string.Equals(staff.Id.Trim(), mobileSelectedEmployeeId.Trim(), StringComparison.OrdinalIgnoreCase))
                : null;

            editingAppointment = new Appointment
            {
                EmployeeId = mobileStaff?.Id ?? string.Empty,
                StaffName = mobileStaff?.Name ?? string.Empty,
                Room = isMobileScheduler && currentGroupBy == GroupByOption.Room ? mobileSelectedRoom : string.Empty,
                Start = defaultStart,
                End = defaultStart.AddMinutes(defaultDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
        }

        private void EditAppointment(Appointment appt)
        {
            editingAppointment = new Appointment
            {
                Id = appt.Id,
                AppointmentId = appt.AppointmentId,
                BookingId = appt.BookingId,
                CustomerId = appt.CustomerId,
                CustomerName = appt.CustomerName,
                RequestedBy = appt.RequestedBy,
                Phone = appt.Phone,
                EmployeeId = appt.EmployeeId,
                StaffName = appt.StaffName,
                SalesPersonCode = appt.SalesPersonCode,
                ServiceItem = appt.ServiceItem,
                ServiceInventoryId = appt.ServiceInventoryId,
                Start = appt.Start,
                End = appt.End,
                Remarks = appt.Remarks,
                Location = appt.Location,
                Room = appt.Room,
                BranchId = appt.BranchId,
                IsAllDay = appt.IsAllDay,
                IsRecurring = appt.IsRecurring,
                RecurrencePattern = appt.RecurrencePattern,
                BusyStatus = appt.BusyStatus,
                StatusName = appt.StatusName,
                Label = appt.Label,
                PaymentStatus = appt.PaymentStatus,
                AppointmentType = appt.AppointmentType,
                Subject = appt.Subject,
                Tags = appt.Tags.ToList()
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
            Feedback.Info(
                string.IsNullOrWhiteSpace(appt.CustomerName)
                    ? "Appointment opened for editing."
                    : $"Editing appointment for {appt.CustomerName}.",
                "Edit appointment",
                2400);
        }


        private void OpenAppointment(Appointment appointment)
        {
            if (isMobileScheduler)
            {
                OpenMobileAppointmentActions(appointment);
                return;
            }

            EditAppointment(appointment);
        }

        private void OpenMobileAppointmentActions(Appointment appointment)
        {
            mobileActionAppointment = appointment;
            showMobileDeleteConfirmation = false;
            showMobileAppointmentSheet = true;
        }

        private void CloseMobileAppointmentActions()
        {
            if (isMobileAppointmentDeleting)
            {
                return;
            }

            showMobileAppointmentSheet = false;
            showMobileDeleteConfirmation = false;
            mobileActionAppointment = null;
        }

        private void EditMobileAppointment()
        {
            var appointment = mobileActionAppointment;
            CloseMobileAppointmentActions();
            if (appointment is not null)
            {
                EditAppointment(appointment);
            }
        }

        private void RequestEditingAppointmentDelete()
        {
            if (editingAppointment.Id == 0 || isAppointmentSaving)
            {
                return;
            }

            mobileActionAppointment = editingAppointment;
            showMobileAppointmentSheet = false;
            showMobileDeleteConfirmation = true;
        }
        private void RequestMobileAppointmentDelete()
        {
            if (mobileActionAppointment is null)
            {
                return;
            }

            if (!IsCancelledAppointment(mobileActionAppointment))
            {
                appointmentError = "Cancel the appointment before deleting it.";
                Feedback.Warning(appointmentError, "Appointment not deleted");
                return;
            }

            showMobileDeleteConfirmation = true;
        }

        private void CancelMobileAppointmentDelete()
        {
            if (!isMobileAppointmentDeleting)
            {
                showMobileDeleteConfirmation = false;
            }
        }

        private async Task ConfirmMobileAppointmentDelete()
        {
            var appointment = mobileActionAppointment;
            if (appointment is null || isMobileAppointmentDeleting)
            {
                return;
            }

            isMobileAppointmentDeleting = true;
            appointmentError = null;

            try
            {
                var result = await ViewModel.DeleteAppointmentAsync(appointment);
                if (!result.Success)
                {
                    appointmentError = result.ErrorMessage ?? "Unable to delete the appointment.";
                    Feedback.Error(appointmentError, "Appointment not deleted");
                    showMobileDeleteConfirmation = false;
                    return;
                }

                NormalizeAppointmentDisplayValues();
                showMobileAppointmentSheet = false;
                showMobileDeleteConfirmation = false;
                mobileActionAppointment = null;
                showModal = false;
                Feedback.Success("Appointment deleted successfully.", "Appointment deleted");
            }
            catch (Exception exception)
            {
                appointmentError = exception.Message;
                Feedback.Error(appointmentError, "Appointment not deleted");
                showMobileDeleteConfirmation = false;
            }
            finally
            {
                isMobileAppointmentDeleting = false;
            }
        }
        private void OpenAppointmentFromHistory(Appointment appointment)
        {
            CloseSettingsSidebar();
            EditAppointment(appointment);
        }

        private void SelectTimeSlot(Staff staff, int hour, int minute)
        {
            if (wasDragging || isSelectingTimeRange)
            {
                wasDragging = false;
                return;
            }

            CloseDateSelector();
            selectedStaff = staff;
            var slotDuration = DefaultAppointmentDurationMinutes;
            editingAppointment = new Appointment
            {
                EmployeeId = staff.Id,
                StaffName = staff.Name,
                Start = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute),
                End = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute).AddMinutes(slotDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
        }

        private void SelectTimeSlotForRoom(string room, int hour, int minute)
        {
            if (wasDragging)
            {
                wasDragging = false;
                return;
            }

            CloseDateSelector();
            var slotDuration = DefaultAppointmentDurationMinutes;
            editingAppointment = new Appointment
            {
                Room = room,
                Start = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute),
                End = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute).AddMinutes(slotDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
        }

        private void OnMouseDown(MouseEventArgs e, Staff staff, TimeSlotInfo timeSlot)
        {
            if (e.Button == 0 && !wasDragging && draggedAppointment == null)
            {
                var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
                var slotEnd = slotStart.AddMinutes(GetTimeScaleMinutes());
                var hasAppointments = ViewModel.Appointments
                    .Any(a => IsAppointmentForStaff(a, staff) &&
                            a.Start.Date == ViewModel.SelectedDate.Date &&
                            a.Start >= slotStart && a.Start < slotEnd);

                if (!hasAppointments)
                {
                    mouseDownTime = DateTime.Now;
                    hasMouseMoved = false;
                    isSelectingTimeRange = false;
                    selectionStaff = staff;
                    selectionStartSlot = timeSlot;
                    selectionEndSlot = timeSlot;
                    CloseDateSelector();
                }
            }
        }

        private void OnMouseMove(MouseEventArgs e, Staff staff, TimeSlotInfo timeSlot)
        {
            if (mouseDownTime.HasValue && selectionStaff?.Id == staff.Id && draggedAppointment == null)
            {
                if (!hasMouseMoved)
                {
                    hasMouseMoved = true;
                }

                var timeSinceDown = (DateTime.Now - mouseDownTime.Value).TotalMilliseconds;
                if (timeSinceDown > CLICK_THRESHOLD_MS || hasMouseMoved)
                {
                    if (!isSelectingTimeRange)
                    {
                        isSelectingTimeRange = true;
                    }
                    selectionEndSlot = timeSlot;
                    StateHasChanged();
                }
            }
        }

        private void OnMouseUp(MouseEventArgs e, Staff staff, TimeSlotInfo timeSlot)
        {
            if (mouseDownTime.HasValue && selectionStaff?.Id == staff.Id)
            {
                var timeSinceDown = (DateTime.Now - mouseDownTime.Value).TotalMilliseconds;

                if (timeSinceDown < CLICK_THRESHOLD_MS && !hasMouseMoved && !isSelectingTimeRange)
                {
                    var selectedSlot = selectionStartSlot ?? timeSlot;
                    ResetTimeRangeSelection();
                    SelectTimeSlot(staff, selectedSlot.Hour, selectedSlot.Minute);
                    return;
                }

                if (isSelectingTimeRange)
                {
                    CompleteTimeRangeSelection(staff, timeSlot);
                }
                else
                {
                    ResetTimeRangeSelection();
                }
            }
        }

        private void OnSchedulerMouseMove(MouseEventArgs e)
        {

        }

        private void OnSchedulerMouseUp(MouseEventArgs e)
        {
            if (isSelectingTimeRange && selectionStartSlot != null)
            {
                if (selectionStaff != null)
                {
                    CompleteTimeRangeSelection(selectionStaff, selectionEndSlot);
                }
                else if (selectionRoom != null)
                {
                    CompleteTimeRangeSelectionForRoom(selectionRoom, selectionEndSlot);
                }
                else if (currentGroupBy == GroupByOption.None)
                {
                    CompleteTimeRangeSelectionNone(selectionEndSlot);
                }
            }
            else
            {
                ResetTimeRangeSelection();
            }
        }

        private void CompleteTimeRangeSelection(Staff staff, TimeSlotInfo? endSlot = null)
        {
            if (!isSelectingTimeRange || selectionStartSlot == null)
            {
                ResetTimeRangeSelection();
                return;
            }

            if (endSlot != null)
            {
                selectionEndSlot = endSlot;
            }

            if (selectionEndSlot == null)
            {
                selectionEndSlot = selectionStartSlot;
            }

            var slotDuration = GetTimeScaleMinutes();

            var startSlot = selectionStartSlot;
            var finalEndSlot = selectionEndSlot;

            var startTime = ViewModel.SelectedDate.AddHours(startSlot.Hour).AddMinutes(startSlot.Minute);
            var endTime = ViewModel.SelectedDate.AddHours(finalEndSlot.Hour).AddMinutes(finalEndSlot.Minute).AddMinutes(slotDuration);

            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }

            if ((endTime - startTime).TotalMinutes >= slotDuration)
            {
                editingAppointment = new Appointment
                {
                    EmployeeId = staff.Id,
                    StaffName = staff.Name,
                    Start = startTime,
                    End = endTime
                };
                appointmentCreateSubmitted = false;
                showModal = true;
            }

            ResetTimeRangeSelection();
        }

        private void ResetTimeRangeSelection()
        {
            isSelectingTimeRange = false;
            selectionStaff = null;
            selectionRoom = null;
            selectionStartSlot = null;
            selectionEndSlot = null;
            mouseDownTime = null;
            hasMouseMoved = false;
            StateHasChanged();
        }

        private bool IsSlotInSelectionRange(Staff staff, TimeSlotInfo timeSlot)
        {
            if (!isSelectingTimeRange || selectionStaff?.Id != staff.Id || selectionStartSlot == null || selectionEndSlot == null)
                return false;

            var slotTime = timeSlot.Hour * 60 + timeSlot.Minute;
            var startTime = selectionStartSlot.Hour * 60 + selectionStartSlot.Minute;
            var endTime = selectionEndSlot.Hour * 60 + selectionEndSlot.Minute;

            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }

            return slotTime >= startTime && slotTime <= endTime;
        }

        private bool IsSlotInSelectionRangeForRoom(string room, TimeSlotInfo timeSlot)
        {
            if (!isSelectingTimeRange || selectionRoom != room || selectionStartSlot == null || selectionEndSlot == null)
                return false;

            var slotTime = timeSlot.Hour * 60 + timeSlot.Minute;
            var startTime = selectionStartSlot.Hour * 60 + selectionStartSlot.Minute;
            var endTime = selectionEndSlot.Hour * 60 + selectionEndSlot.Minute;

            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }

            return slotTime >= startTime && slotTime <= endTime;
        }

        private bool IsSlotInSelectionRangeNone(TimeSlotInfo timeSlot)
        {
            if (!isSelectingTimeRange || currentGroupBy != GroupByOption.None || selectionStartSlot == null || selectionEndSlot == null)
                return false;

            var slotTime = timeSlot.Hour * 60 + timeSlot.Minute;
            var startTime = selectionStartSlot.Hour * 60 + selectionStartSlot.Minute;
            var endTime = selectionEndSlot.Hour * 60 + selectionEndSlot.Minute;

            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }

            return slotTime >= startTime && slotTime <= endTime;
        }

        private TimeSlotInfo? GetAppointmentStartSlot(Appointment appt)
        {
            var slotDuration = GetTimeScaleMinutes();
            foreach (var slot in TimeSlots)
            {
                var slotStart = ViewModel.SelectedDate.AddHours(slot.Hour).AddMinutes(slot.Minute);
                var slotEnd = slotStart.AddMinutes(slotDuration);

                if (appt.Start >= slotStart && appt.Start < slotEnd)
                {
                    return slot;
                }
            }
            return null;
        }

        private double GetAppointmentTopPosition(Appointment appt, TimeSlotInfo timeSlot)
        {
            var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
            var minutesPerSlot = GetTimeScaleMinutes();
            const double slotPixelHeight = 50.0; // Base slot height in CSS

            var minutesIntoSlot = (appt.Start - slotStart).TotalMinutes;
            var pixelsFromTop = (minutesIntoSlot / minutesPerSlot) * slotPixelHeight;

            return pixelsFromTop;
        }

        private double GetAppointmentTotalHeight(Appointment appt)
        {
            var totalDuration = appt.IsAllDay
                ? WorkingHoursEndMinutes - WorkingHoursStartMinutes
                : (appt.End - appt.Start).TotalMinutes;
            var minutesPerSlot = GetTimeScaleMinutes();
            const double slotPixelHeight = 50.0; // Base slot height in CSS

            var heightInPixels = (totalDuration / minutesPerSlot) * slotPixelHeight;

            return Math.Max(heightInPixels, 30); // Minimum height safeguard
        }

        private string GetAppointmentTopPositionPercent(Appointment appt, TimeSlotInfo timeSlot)
        {
            var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
            var minutesPerSlot = GetTimeScaleMinutes();

            var minutesIntoSlot = (appt.Start - slotStart).TotalMinutes;
            var percentFromTop = (minutesIntoSlot / minutesPerSlot) * 100.0;

            return $"{Math.Max(0, percentFromTop)}%";
        }

        private string GetAppointmentTotalHeightPercent(Appointment appt)
        {
            var totalDuration = appt.IsAllDay
                ? WorkingHoursEndMinutes - WorkingHoursStartMinutes
                : (appt.End - appt.Start).TotalMinutes;
            var minutesPerSlot = GetTimeScaleMinutes();

            var slotCount = totalDuration / minutesPerSlot;
            var heightPercent = slotCount * 100.0;
            var minHeightPercent = 60.0; // 30px minimum = 60% of 50px slot

            // Each rendered slot has a 1px bottom border. Percentage heights use the
            // slot padding box, so a full-day block otherwise loses one pixel per row.
            return appt.IsAllDay
                ? $"calc({heightPercent}% + {slotCount}px)"
                : $"{Math.Max(minHeightPercent, heightPercent)}%";
        }

        private static string GetAppointmentDisplayTitle(Appointment appointment)
        {
            if (!string.IsNullOrWhiteSpace(appointment.CustomerName))
                return appointment.CustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(appointment.Subject))
                return appointment.Subject.Trim();
            if (!string.IsNullOrWhiteSpace(appointment.RequestedBy))
                return appointment.RequestedBy.Trim();

            return "Unassigned customer";
        }

        private static string GetAppointmentDisplayService(Appointment appointment)
        {
            var service = !string.IsNullOrWhiteSpace(appointment.ServiceItem)
                ? appointment.ServiceItem.Trim()
                : !string.IsNullOrWhiteSpace(appointment.Subject) &&
                  !string.Equals(appointment.Subject.Trim(), GetAppointmentDisplayTitle(appointment), StringComparison.OrdinalIgnoreCase)
                    ? appointment.Subject.Trim()
                    : "General appointment";

            return appointment.IsRecurring
                ? $"{service} | Repeats {GetRecurrenceDisplay(appointment.RecurrencePattern)}"
                : service;
        }

        private static string GetAppointmentTimeRange(Appointment appointment) => appointment.IsAllDay
            ? "All day"
            : $"{appointment.Start:h:mm tt} - {appointment.End:h:mm tt}";

        private static string GetRecurrenceDisplay(string? pattern) => pattern switch
        {
            "Daily" => "daily",
            "Monthly" => "monthly",
            _ => "weekly"
        };

        private static string GetAppointmentStatusCssClass(Appointment appointment)
        {
            return appointment.Label switch
            {
                0 => "appointment-status-none",
                1 => "appointment-status-confirmed",
                2 => "appointment-status-completed",
                3 => "appointment-status-pending-payment",
                4 => "appointment-status-paid",
                5 => "appointment-status-no-show",
                6 => "appointment-status-cancelled",
                _ => GetAppointmentStatusCssClassByName(appointment.StatusName)
            };
        }

        private static string GetAppointmentStatusCssClassByName(string? statusName)
        {
            var status = statusName?.Trim().ToLowerInvariant() ?? string.Empty;
            if (status.Contains("pending") && status.Contains("payment")) return "appointment-status-pending-payment";
            if (status.Contains("no show")) return "appointment-status-no-show";
            if (status.Contains("cancel")) return "appointment-status-cancelled";
            if (status.Contains("complete")) return "appointment-status-completed";
            if (status.Contains("confirm")) return "appointment-status-confirmed";
            if (status.Contains("paid")) return "appointment-status-paid";
            return "appointment-status-none";
        }

        private string GetAppointmentStatusDisplayName(Appointment appointment)
        {
            var apiName = appointmentStatuses.FirstOrDefault(status => status.Value == appointment.Label)?.Name;
            if (!string.IsNullOrWhiteSpace(apiName))
            {
                return apiName;
            }

            if (!string.IsNullOrWhiteSpace(appointment.StatusName))
            {
                return appointment.StatusName;
            }

            return appointment.Label switch
            {
                0 => "No Status",
                1 => "Confirmed",
                2 => "Completed",
                3 => "Pending Payment",
                4 => "Paid",
                5 => "No Show",
                6 => "Cancelled",
                _ => "Unknown"
            };
        }

        private bool IsCancelledAppointment(Appointment appointment)
        {
            var cancelledStatus = appointmentStatuses.FirstOrDefault(status =>
                status.Name.Contains("cancel", StringComparison.OrdinalIgnoreCase));
            return cancelledStatus is not null && appointment.Label == cancelledStatus.Value;
        }
        private void OnEditingAppointmentStatusChanged()
        {
            editingAppointment.StatusName = appointmentStatuses
                .FirstOrDefault(status => status.Value == editingAppointment.Label)?.Name
                ?? GetAppointmentStatusDisplayName(editingAppointment);
        }
        private bool ShouldShowAppointmentInSlot(Appointment appt, TimeSlotInfo timeSlot)
        {
            var startSlot = GetAppointmentStartSlot(appt);
            if (startSlot == null)
                return false;

            return startSlot.Hour == timeSlot.Hour && startSlot.Minute == timeSlot.Minute;
        }

        private static Dictionary<Appointment, int> CalculateTimelineLanes(IEnumerable<Appointment> appointments)
        {
            var laneEnds = new List<DateTime>();
            var assignments = new Dictionary<Appointment, int>();

            foreach (var appointment in appointments
                .Where(item => item.End > item.Start)
                .OrderBy(item => item.Start)
                .ThenBy(item => item.End))
            {
                var lane = laneEnds.FindIndex(end => end <= appointment.Start);
                if (lane < 0)
                {
                    lane = laneEnds.Count;
                    laneEnds.Add(appointment.End);
                }
                else
                {
                    laneEnds[lane] = appointment.End;
                }

                assignments[appointment] = lane;
            }

            return assignments;
        }
        private class AppointmentLayout
        {
            public Appointment Appointment { get; set; } = null!;
            public double Left { get; set; }
            public double Width { get; set; }
            public int ColumnIndex { get; set; }
            public int TotalColumns { get; set; }
        }

        private List<AppointmentLayout> CalculateAppointmentLayouts(List<Appointment> appointments)
        {
            var sortedAppointments = appointments
                .Where(appointment => appointment.End > appointment.Start)
                .OrderBy(appointment => appointment.Start)
                .ThenBy(appointment => appointment.End)
                .ToList();

            if (sortedAppointments.Count == 0)
                return new List<AppointmentLayout>();

            var overlapGroups = new List<List<Appointment>>();
            List<Appointment>? currentGroup = null;
            var currentGroupEnd = DateTime.MinValue;

            foreach (var appointment in sortedAppointments)
            {
                if (currentGroup is null || appointment.Start >= currentGroupEnd)
                {
                    currentGroup = new List<Appointment>();
                    overlapGroups.Add(currentGroup);
                    currentGroupEnd = appointment.End;
                }
                else if (appointment.End > currentGroupEnd)
                {
                    currentGroupEnd = appointment.End;
                }

                currentGroup.Add(appointment);
            }

            var layouts = new List<AppointmentLayout>();
            const double outerPadding = 1.0;
            const double laneGap = 0.6;

            foreach (var group in overlapGroups)
            {
                var laneEnds = new List<DateTime>();
                var laneAssignments = new Dictionary<Appointment, int>();

                foreach (var appointment in group)
                {
                    var lane = laneEnds.FindIndex(end => end <= appointment.Start);
                    if (lane < 0)
                    {
                        lane = laneEnds.Count;
                        laneEnds.Add(appointment.End);
                    }
                    else
                    {
                        laneEnds[lane] = appointment.End;
                    }

                    laneAssignments[appointment] = lane;
                }

                var laneCount = Math.Max(laneEnds.Count, 1);
                var availableWidth = 100.0 - (outerPadding * 2) - (laneGap * (laneCount - 1));
                var laneWidth = availableWidth / laneCount;

                foreach (var appointment in group)
                {
                    var lane = laneAssignments[appointment];
                    layouts.Add(new AppointmentLayout
                    {
                        Appointment = appointment,
                        Left = outerPadding + (lane * (laneWidth + laneGap)),
                        Width = laneWidth,
                        ColumnIndex = lane,
                        TotalColumns = laneCount
                    });
                }
            }

            return layouts.OrderBy(layout => layout.Appointment.Start).ToList();
        }

        private async Task SaveAppointment()
        {
            var isCreate = editingAppointment.Id == 0;
            if (isAppointmentSaving || (isCreate && appointmentCreateSubmitted)) return;

            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            PrepareAppointmentReferences(editingAppointment);
            ViewModel.SelectedDate = editingAppointment.Start.Date;
            if (isMobileScheduler && !string.IsNullOrWhiteSpace(editingAppointment.EmployeeId))
            {
                mobileSelectedEmployeeId = editingAppointment.EmployeeId;
            }
            if (isMobileScheduler && !string.IsNullOrWhiteSpace(editingAppointment.Room))
            {
                mobileSelectedRoom = editingAppointment.Room;
            }
            isAppointmentSaving = true;
            if (isCreate) appointmentCreateSubmitted = true;
            appointmentError = null;
            try
            {
                var result = isCreate
                    ? await ViewModel.AddAppointmentAsync(editingAppointment)
                    : await ViewModel.UpdateAppointmentAsync(editingAppointment);

                if (!result.Success)
                {
                    if (isCreate) appointmentCreateSubmitted = false;
                    appointmentError = result.ErrorMessage ?? "Unable to save the appointment.";
                    Feedback.Error(
                        appointmentError,
                        isCreate ? "Appointment not created" : "Appointment not updated");
                    return;
                }

                NormalizeAppointmentDisplayValues();
                Feedback.Success(
                    isCreate ? "Appointment created successfully." : "Appointment updated successfully.",
                    isCreate ? "Appointment created" : "Appointment updated");
                CloseModal();
            }
            catch (Exception exception)
            {
                if (isCreate) appointmentCreateSubmitted = false;
                appointmentError = $"Unable to save the appointment: {exception.Message}";
                Feedback.Error(
                    appointmentError,
                    isCreate ? "Appointment not created" : "Appointment not updated");
            }
            finally
            {
                isAppointmentSaving = false;
            }
        }


        private async Task<bool> PersistMovedAppointmentAsync(Appointment appointment)
        {
            PrepareAppointmentReferences(appointment);
            var result = await ViewModel.UpdateAppointmentAsync(appointment);
            if (!result.Success)
            {
                appointmentError = result.ErrorMessage ?? "Unable to move the appointment.";
                Feedback.Error(appointmentError, "Appointment not moved");
                return false;
            }

            appointmentError = null;
            NormalizeAppointmentDisplayValues();
            Feedback.Success(
                $"Appointment moved to {appointment.Start:dd/MM/yyyy hh:mm tt}.",
                "Appointment moved",
                3200);
            await InvokeAsync(StateHasChanged);
            return true;
        }
        private async Task CancelAppointment()
        {
            if (isAppointmentSaving || editingAppointment.Id == 0) return;

            isAppointmentSaving = true;
            appointmentError = null;
            PrepareAppointmentReferences(editingAppointment);
            var result = await ViewModel.CancelAppointmentAsync(editingAppointment);
            isAppointmentSaving = false;

            if (!result.Success)
            {
                appointmentError = result.ErrorMessage ?? "Unable to cancel the appointment.";
                Feedback.Error(appointmentError, "Appointment not cancelled");
                return;
            }

            Feedback.Success("Appointment cancelled successfully.", "Appointment cancelled");
            CloseModal();
            await ReloadAppointmentsAsync();
        }

        private void CloseModal()
        {
            showModal = false;
            CloseDateSelector();
        }

        private void NavigateToCaseNotes()
        {
            CloseModal();
            NavigationManager.NavigateTo("/case-notes");
        }

        private void SelectTimeSlotNone(int hour, int minute)
        {
            if (wasDragging || isSelectingTimeRange)
            {
                wasDragging = false;
                return;
            }

            CloseDateSelector();
            var slotDuration = DefaultAppointmentDurationMinutes;
            editingAppointment = new Appointment
            {
                Start = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute),
                End = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute).AddMinutes(slotDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
        }

        private void OnMouseDownNone(MouseEventArgs e, TimeSlotInfo timeSlot)
        {
            if (e.Button == 0 && !wasDragging && draggedAppointment == null)
            {
                var slotStart = ViewModel.SelectedDate.AddHours(timeSlot.Hour).AddMinutes(timeSlot.Minute);
                var slotEnd = slotStart.AddMinutes(GetTimeScaleMinutes());
                var hasAppointments = ViewModel.Appointments
                    .Any(a => a.Start.Date == ViewModel.SelectedDate.Date &&
                            a.Start >= slotStart && a.Start < slotEnd);

                if (!hasAppointments)
                {
                    mouseDownTime = DateTime.Now;
                    hasMouseMoved = false;
                    isSelectingTimeRange = false;
                    selectionRoom = null;
                    selectionStaff = null;
                    selectionStartSlot = timeSlot;
                    selectionEndSlot = timeSlot;
                    CloseDateSelector();
                }
            }
        }

        private void OnMouseMoveNone(MouseEventArgs e, TimeSlotInfo timeSlot)
        {
            if (mouseDownTime.HasValue && currentGroupBy == GroupByOption.None && draggedAppointment == null)
            {
                if (!hasMouseMoved)
                {
                    hasMouseMoved = true;
                }

                var timeSinceDown = (DateTime.Now - mouseDownTime.Value).TotalMilliseconds;
                if (timeSinceDown > CLICK_THRESHOLD_MS || hasMouseMoved)
                {
                    if (!isSelectingTimeRange)
                    {
                        isSelectingTimeRange = true;
                    }
                    selectionEndSlot = timeSlot;
                    StateHasChanged();
                }
            }
        }

        private void OnMouseUpNone(MouseEventArgs e, TimeSlotInfo timeSlot)
        {
            if (mouseDownTime.HasValue && currentGroupBy == GroupByOption.None)
            {
                var timeSinceDown = (DateTime.Now - mouseDownTime.Value).TotalMilliseconds;

                if (timeSinceDown < CLICK_THRESHOLD_MS && !hasMouseMoved && !isSelectingTimeRange)
                {
                    var selectedSlot = selectionStartSlot ?? timeSlot;
                    ResetTimeRangeSelection();
                    SelectTimeSlotNone(selectedSlot.Hour, selectedSlot.Minute);
                    return;
                }

                if (isSelectingTimeRange)
                {
                    CompleteTimeRangeSelectionNone(timeSlot);
                }
                else
                {
                    ResetTimeRangeSelection();
                }
            }
        }

        private void CompleteTimeRangeSelectionNone(TimeSlotInfo? endSlot = null)
        {
            if (!isSelectingTimeRange || selectionStartSlot == null || currentGroupBy != GroupByOption.None)
            {
                ResetTimeRangeSelection();
                return;
            }

            // Use provided endSlot or keep current selectionEndSlot
            if (endSlot != null)
            {
                selectionEndSlot = endSlot;
            }

            if (selectionEndSlot == null)
            {
                // If no end slot, use start slot (single slot)
                selectionEndSlot = selectionStartSlot;
            }

            var slotDuration = GetTimeScaleMinutes();

            // Calculate the time range
            var startSlot = selectionStartSlot;
            var finalEndSlot = selectionEndSlot;

            // Determine which is earlier (start) and which is later (end)
            var startTime = ViewModel.SelectedDate.AddHours(startSlot.Hour).AddMinutes(startSlot.Minute);
            var endTime = ViewModel.SelectedDate.AddHours(finalEndSlot.Hour).AddMinutes(finalEndSlot.Minute).AddMinutes(slotDuration);

            if (startTime > endTime)
            {
                // Swap if user dragged backwards
                (startTime, endTime) = (endTime, startTime);
            }

            // Only open modal if there's a meaningful time range (at least one slot)
            if ((endTime - startTime).TotalMinutes >= slotDuration)
            {
                // Open modal with selected time range
                editingAppointment = new Appointment
                {
                    Start = startTime,
                    End = endTime
                };
                appointmentCreateSubmitted = false;
                showModal = true;
            }

            // Reset selection state
            ResetTimeRangeSelection();
        }

        private void OnDragStartNone(DragEventArgs e, Appointment appointment, int hour, int minute)
        {
            wasDragging = true;
            draggedAppointment = appointment;
            isAppointmentDragging = true;
            draggingAppointmentId = appointment.Id;
            draggingAppointmentDurationMinutes = (int)(appointment.End - appointment.Start).TotalMinutes;
            dragStartHour = hour;
            dragStartMinute = minute;
            var slotStart = ViewModel.SelectedDate.AddHours(hour).AddMinutes(minute);
            dragOffsetMinutes = (int)(appointment.Start - slotStart).TotalMinutes;
            e.DataTransfer.EffectAllowed = "move";
            CloseDateSelector();
            ResetTimeRangeSelection();
        }

        private void OnDragEnterNone(DragEventArgs e, int hour, int minute)
        {
            if (!isAppointmentDragging)
            {
                return;
            }
            wasDragging = true;
            dragTargetRoom = null;
            dragTargetStaff = null;
            dragTargetHour = hour;
            dragTargetMinute = minute;
            CloseDateSelector();
            StateHasChanged();
        }

        private void OnDragLeaveNone(DragEventArgs e, TimeSlotInfo timeSlot)
        {
            if (!isAppointmentDragging)
            {
                return;
            }
            if (dragTargetHour != timeSlot.Hour ||
                dragTargetMinute != timeSlot.Minute)
            {
                return;
            }
            dragTargetHour = null;
            dragTargetMinute = null;
            StateHasChanged();
        }

        private async Task OnDropNone(DragEventArgs e, int targetHour, int targetMinute)
        {
            var appointment = draggedAppointment;
            if (appointment != null)
            {
                var previousStart = appointment.Start;
                var previousEnd = appointment.End;
                var newStart = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute).AddMinutes(dragOffsetMinutes);
                var duration = appointment.End - appointment.Start;

                appointment.Start = newStart;
                appointment.End = newStart.Add(duration);
                ConstrainAppointmentToWorkingHours(appointment, preserveDuration: true);
                ResetDragState();

                if (!await PersistMovedAppointmentAsync(appointment))
                {
                    appointment.Start = previousStart;
                    appointment.End = previousEnd;
                    await InvokeAsync(StateHasChanged);
                }

                return;
            }

            var slotDuration = GetTimeScaleMinutes();
            editingAppointment = new Appointment
            {
                Start = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute),
                End = ViewModel.SelectedDate.AddHours(targetHour).AddMinutes(targetMinute).AddMinutes(slotDuration)
            };
            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
            appointmentCreateSubmitted = false;
            showModal = true;
            ResetDragState();
        }
        public void Dispose()
        {
            AppointmentService.OnChange -= OnAppointmentServiceChanged;
        }

        private void OnAppointmentServiceChanged()
        {
            _ = InvokeAsync(StateHasChanged);
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    isMobileScheduler = await JS.InvokeAsync<bool>("eval", "window.matchMedia('(max-width: 576px)').matches");
                }
                catch
                {
                    isMobileScheduler = false;
                }

                if (isMobileScheduler)
                {
                    EnsureMobileEmployeeSelection();
                    mobileSelectedRoom = GetVisibleRooms().FirstOrDefault() ?? string.Empty;
                    StateHasChanged();
                }
            }

            if (!showPickerSupportChecked)
            {
                showPickerSupportChecked = true;
                bool detectedSupport = false;
                try
                {
                    detectedSupport = await JS.InvokeAsync<bool>("eval", "('showPicker' in HTMLInputElement.prototype)");
                }
                catch
                {
                    detectedSupport = false;
                }

                if (supportsShowPicker != detectedSupport)
                {
                    supportsShowPicker = detectedSupport;
                    StateHasChanged();
                }
                else
                {
                    supportsShowPicker = detectedSupport;
                }
            }

            if (focusDateInputPending)
            {
                focusDateInputPending = false;
                await dateInputRef.FocusAsync();

                if (supportsShowPicker)
                {
                    try
                    {
                        await JS.InvokeVoidAsync("eval", "(() => { const el = document.getElementById('inlineDatePicker'); if (!el) return; if (typeof el.showPicker === 'function') { el.showPicker(); } })();");
                    }
                    catch
                    {

                    }
                }
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        private void OnAllDayChanged()
        {
            if (editingAppointment == null) return;

            if (editingAppointment.IsAllDay)
            {
                editingAppointment.Start = editingAppointment.Start.Date.AddHours(9);
                editingAppointment.End = editingAppointment.Start.Date.AddHours(18);
                return;
            }

            editingAppointment.Start = editingAppointment.Start.Date.AddHours(9);
            editingAppointment.End = editingAppointment.Start.AddMinutes(DefaultAppointmentDurationMinutes);
        }

        private void OnRecurringChanged()
        {
            if (editingAppointment == null) return;

            editingAppointment.RecurrencePattern = editingAppointment.IsRecurring
                ? string.IsNullOrWhiteSpace(editingAppointment.RecurrencePattern) ? "Weekly" : editingAppointment.RecurrencePattern
                : string.Empty;
        }

        private static void ConstrainAppointmentToWorkingHours(Appointment appointment, bool preserveDuration)
        {
            var opening = appointment.Start.Date.AddMinutes(WorkingHoursStartMinutes);
            var closing = appointment.Start.Date.AddMinutes(WorkingHoursEndMinutes);

            if (appointment.IsAllDay)
            {
                appointment.Start = opening;
                appointment.End = closing;
                return;
            }

            var duration = appointment.End - appointment.Start;
            if (duration <= TimeSpan.Zero)
            {
                duration = TimeSpan.FromMinutes(DefaultAppointmentDurationMinutes);
            }

            var workingDayDuration = closing - opening;
            if (duration > workingDayDuration)
            {
                duration = workingDayDuration;
            }

            if (preserveDuration)
            {
                var start = appointment.Start < opening ? opening : appointment.Start;
                if (start.Add(duration) > closing)
                {
                    start = closing - duration;
                }

                appointment.Start = start;
                appointment.End = start.Add(duration);
                return;
            }

            if (appointment.Start < opening)
            {
                appointment.Start = opening;
            }
            else if (appointment.Start >= closing)
            {
                appointment.Start = closing.AddMinutes(-DefaultAppointmentDurationMinutes);
            }

            appointment.End = appointment.End > closing ? closing : appointment.End;
            if (appointment.End <= appointment.Start)
            {
                appointment.End = appointment.Start.Add(duration);
                if (appointment.End > closing)
                {
                    appointment.End = closing;
                }
            }
        }
        private void EnsureEndAfterStart()
        {
            if (editingAppointment == null)
            {
                return;
            }

            if (editingAppointment.End <= editingAppointment.Start)
            {
                editingAppointment.End = editingAppointment.Start.AddMinutes(DefaultAppointmentDurationMinutes);
            }

            ConstrainAppointmentToWorkingHours(editingAppointment, preserveDuration: false);
        }
    }
}




