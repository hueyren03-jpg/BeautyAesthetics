using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class CaseNoteReasonManagerViewModel : INotifyPropertyChanged
{
    // Status messaging
    private string _statusMessage = string.Empty;
    private bool _isSuccess;

    // Form state
    private string _newReasonKey = string.Empty;
    private string _newReasonValue = string.Empty;
    private string _editingReasonKey = string.Empty;
    private string _editReasonKey = string.Empty;
    private string _editReasonValue = string.Empty;
    
    // UI state
    private bool _isAddingNew;
    private bool _showDeleteConfirmation;
    private string _reasonToDelete = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ==================== PROPERTIES ====================

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public string NewReasonKey
    {
        get => _newReasonKey;
        set => SetProperty(ref _newReasonKey, value);
    }

    public string NewReasonValue
    {
        get => _newReasonValue;
        set => SetProperty(ref _newReasonValue, value);
    }

    public string EditingReasonKey
    {
        get => _editingReasonKey;
        set => SetProperty(ref _editingReasonKey, value);
    }

    public string EditReasonKey
    {
        get => _editReasonKey;
        set => SetProperty(ref _editReasonKey, value);
    }

    public string EditReasonValue
    {
        get => _editReasonValue;
        set => SetProperty(ref _editReasonValue, value);
    }

    public bool IsAddingNew
    {
        get => _isAddingNew;
        set => SetProperty(ref _isAddingNew, value);
    }

    public bool ShowDeleteConfirmation
    {
        get => _showDeleteConfirmation;
        set => SetProperty(ref _showDeleteConfirmation, value);
    }

    public string ReasonToDelete
    {
        get => _reasonToDelete;
        set => SetProperty(ref _reasonToDelete, value);
    }

    // ==================== VALIDATION ====================

    public bool CanAddReason()
    {
        return !string.IsNullOrWhiteSpace(NewReasonKey) && 
               !string.IsNullOrWhiteSpace(NewReasonValue);
    }

    public bool CanUpdateReason()
    {
        return !string.IsNullOrWhiteSpace(EditReasonKey) && 
               !string.IsNullOrWhiteSpace(EditReasonValue);
    }

    public string GetAddValidationError()
    {
        if (string.IsNullOrWhiteSpace(NewReasonKey))
            return "Reason name is required";
        
        if (string.IsNullOrWhiteSpace(NewReasonValue))
            return "Reason description is required";
        
        return string.Empty;
    }

    public string GetEditValidationError()
    {
        if (string.IsNullOrWhiteSpace(EditReasonKey))
            return "Reason name is required";
        
        if (string.IsNullOrWhiteSpace(EditReasonValue))
            return "Reason description is required";
        
        return string.Empty;
    }

    // ==================== UI ACTIONS ====================

    public void StartAddingNew()
    {
        IsAddingNew = true;
        NewReasonKey = string.Empty;
        NewReasonValue = string.Empty;
        ClearStatus();
    }

    public void CancelAddingNew()
    {
        IsAddingNew = false;
        NewReasonKey = string.Empty;
        NewReasonValue = string.Empty;
    }

    public void StartEditing(string key, string value)
    {
        EditingReasonKey = key;
        EditReasonKey = key;
        EditReasonValue = value;
        ClearStatus();
    }

    public void CancelEditing()
    {
        EditingReasonKey = string.Empty;
        EditReasonKey = string.Empty;
        EditReasonValue = string.Empty;
    }

    public void ShowDeleteDialog(string key)
    {
        ReasonToDelete = key;
        ShowDeleteConfirmation = true;
    }

    public void CancelDelete()
    {
        ReasonToDelete = string.Empty;
        ShowDeleteConfirmation = false;
    }

    public void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
    }

    public void SetSuccess(string message)
    {
        StatusMessage = message;
        IsSuccess = true;
    }

    public void SetError(string message)
    {
        StatusMessage = message;
        IsSuccess = false;
    }

    // ==================== HELPER METHODS ====================

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class ReasonItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

