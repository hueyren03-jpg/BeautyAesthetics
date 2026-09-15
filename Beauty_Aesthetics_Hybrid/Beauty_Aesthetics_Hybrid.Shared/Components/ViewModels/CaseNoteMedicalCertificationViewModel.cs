using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class CaseNoteMedicalCertificationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Active Tab
    private string _activeTab = "day-off";
    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != value)
            {
                _activeTab = value;
                OnPropertyChanged();
            }
        }
    }

    // Patient Name
    private string _patientName = string.Empty;
    public string PatientName
    {
        get => _patientName;
        set
        {
            if (_patientName != value)
            {
                _patientName = value;
                OnPropertyChanged();
            }
        }
    }

    // Day-off Slip Properties
    private DateTime _dayOffStartDate = DateTime.Today;
    private DateTime _dayOffEndDate = DateTime.Today;
    private int _durationDays = 1;
    private bool _includeHalfDay = false;
    private string _dayOffReason = string.Empty;

    public DateTime DayOffStartDate
    {
        get => _dayOffStartDate;
        set
        {
            if (_dayOffStartDate != value)
            {
                _dayOffStartDate = value;
                OnPropertyChanged();
                CalculateDurationDays();
            }
        }
    }

    public DateTime DayOffEndDate
    {
        get => _dayOffEndDate;
        set
        {
            if (_dayOffEndDate != value)
            {
                _dayOffEndDate = value;
                OnPropertyChanged();
                CalculateDurationDays();
            }
        }
    }

    public int DurationDays
    {
        get => _durationDays;
        set
        {
            if (_durationDays != value)
            {
                _durationDays = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IncludeHalfDay
    {
        get => _includeHalfDay;
        set
        {
            if (_includeHalfDay != value)
            {
                _includeHalfDay = value;
                OnPropertyChanged();
            }
        }
    }

    public string DayOffReason
    {
        get => _dayOffReason;
        set
        {
            if (_dayOffReason != value)
            {
                _dayOffReason = value;
                OnPropertyChanged();
            }
        }
    }

    // Custom Reasons - loaded dynamically from localStorage
    // Note: This is now managed through JavaScript reason-storage.js

    // Time-off Slip Properties
    private DateTime _timeOffStartDate = DateTime.Today;
    private DateTime _timeOffEndDate = DateTime.Today;
    private TimeOnly _timeOffStartTime = new TimeOnly(9, 0, 0);
    private TimeOnly _timeOffEndTime = new TimeOnly(17, 0, 0);
    private decimal _durationHours = 8;
    private string _timeOffReason = string.Empty;

    public DateTime TimeOffStartDate
    {
        get => _timeOffStartDate;
        set
        {
            if (_timeOffStartDate != value)
            {
                _timeOffStartDate = value;
                _timeOffEndDate = value; // Auto-sync end date to same as start date
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeOffEndDate));
                CalculateDurationHours();
            }
        }
    }

    public DateTime TimeOffEndDate
    {
        get => _timeOffEndDate;
        set
        {
            if (_timeOffEndDate != value)
            {
                _timeOffEndDate = value;
                OnPropertyChanged();
                CalculateDurationHours();
            }
        }
    }

    public TimeOnly TimeOffStartTime
    {
        get => _timeOffStartTime;
        set
        {
            if (_timeOffStartTime != value)
            {
                _timeOffStartTime = value;
                OnPropertyChanged();
                CalculateDurationHours();
            }
        }
    }

    public TimeOnly TimeOffEndTime
    {
        get => _timeOffEndTime;
        set
        {
            if (_timeOffEndTime != value)
            {
                _timeOffEndTime = value;
                OnPropertyChanged();
                CalculateDurationHours();
            }
        }
    }

    public decimal DurationHours
    {
        get => _durationHours;
        private set
        {
            if (_durationHours != value)
            {
                _durationHours = value;
                OnPropertyChanged();
            }
        }
    }

    public string TimeOffReason
    {
        get => _timeOffReason;
        set
        {
            if (_timeOffReason != value)
            {
                _timeOffReason = value;
                OnPropertyChanged();
            }
        }
    }

    // Methods
    private void CalculateDurationDays()
    {
        if (DayOffEndDate >= DayOffStartDate)
        {
            DurationDays = (DayOffEndDate - DayOffStartDate).Days + 1;
        }
    }

    private void CalculateDurationHours()
    {
        // Calculate duration on the same day
        var startMinutes = TimeOffStartTime.Hour * 60 + TimeOffStartTime.Minute;
        var endMinutes = TimeOffEndTime.Hour * 60 + TimeOffEndTime.Minute;
        
        if (endMinutes > startMinutes)
        {
            var totalMinutes = endMinutes - startMinutes;
            DurationHours = Math.Round((decimal)totalMinutes / 60, 2);
        }
        else
        {
            DurationHours = 0;
        }
    }

    public void SetTab(string tab)
    {
        ActiveTab = tab;
    }

    public void SetDayOffPredefinedReason(string reasonValue)
    {
        DayOffReason = reasonValue;
    }

    public void SetTimeOffPredefinedReason(string reasonValue)
    {
        TimeOffReason = reasonValue;
    }

    public bool CanSubmitDayOff()
    {
        return !string.IsNullOrWhiteSpace(PatientName) &&
               !string.IsNullOrWhiteSpace(DayOffReason) && 
               DayOffEndDate >= DayOffStartDate;
    }

    public bool CanSubmitTimeOff()
    {
        return !string.IsNullOrWhiteSpace(PatientName) &&
               !string.IsNullOrWhiteSpace(TimeOffReason) && 
               DurationHours > 0;
    }

    public void SubmitDayOff()
    {
        if (!CanSubmitDayOff())
        {
            StatusMessage = DayOffValidationError;
            IsSuccess = false;
            return;
        }

        // Implementation for submitting day-off slip
        var certificateNumber = $"DC-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        Console.WriteLine($"Day-off submitted: {DayOffStartDate:yyyy-MM-dd} to {DayOffEndDate:yyyy-MM-dd}, Duration: {DurationDays}{(IncludeHalfDay ? ".5" : "")} days, Reason: {DayOffReason}");
        
        StatusMessage = $"✓ Day-off certificate {certificateNumber} submitted successfully!";
        IsSuccess = true;
        
        // Reset form after submission
        ResetDayOff();
    }

    public void SubmitTimeOff()
    {
        if (!CanSubmitTimeOff())
        {
            StatusMessage = TimeOffValidationError;
            IsSuccess = false;
            return;
        }

        // Implementation for submitting time-off slip
        var certificateNumber = $"TC-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        Console.WriteLine($"Time-off submitted: {TimeOffStartDate:yyyy-MM-dd} {TimeOffStartTime} to {TimeOffEndDate:yyyy-MM-dd} {TimeOffEndTime}, Duration: {DurationHours} hours, Reason: {TimeOffReason}");
        
        StatusMessage = $"✓ Time-off certificate {certificateNumber} submitted successfully!";
        IsSuccess = true;
        
        // Reset form after submission
        ResetTimeOff();
    }

    public void ResetDayOff()
    {
        PatientName = string.Empty;
        DayOffStartDate = DateTime.Today;
        DayOffEndDate = DateTime.Today;
        DurationDays = 1;
        IncludeHalfDay = false;
        DayOffReason = string.Empty;
    }

    public void ResetTimeOff()
    {
        PatientName = string.Empty;
        TimeOffStartDate = DateTime.Today;
        TimeOffEndDate = DateTime.Today;
        TimeOffStartTime = new TimeOnly(9, 0, 0);
        TimeOffEndTime = new TimeOnly(17, 0, 0);
        TimeOffReason = string.Empty;
        CalculateDurationHours(); // ← Fix: Recalculate duration
    }

    // Status message properties
    private string _statusMessage = string.Empty;
    private bool _isSuccess = false;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set
        {
            if (_isSuccess != value)
            {
                _isSuccess = value;
                OnPropertyChanged();
            }
        }
    }

    // Validation error messages
    public string DayOffValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PatientName))
                return "Please enter patient name";
            if (DayOffEndDate < DayOffStartDate)
                return "End date cannot be before start date";
            if (string.IsNullOrWhiteSpace(DayOffReason))
                return "Please enter a reason";
            return string.Empty;
        }
    }

    public string TimeOffValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PatientName))
                return "Please enter patient name";
            if (DurationHours <= 0)
                return "End time must be after start time";
            if (string.IsNullOrWhiteSpace(TimeOffReason))
                return "Please enter a reason";
            return string.Empty;
        }
    }
}
