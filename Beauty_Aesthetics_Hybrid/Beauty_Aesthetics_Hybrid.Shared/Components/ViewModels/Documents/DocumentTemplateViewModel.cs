#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class DocumentTemplateViewModel : INotifyPropertyChanged
{
    private readonly IJSRuntime? _jsRuntime;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    // Constructor for dependency injection
    public DocumentTemplateViewModel()
    {
        _jsRuntime = null;
    }

    public DocumentTemplateViewModel(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Field Types
    public enum FieldType
    {
        Text,
        TextArea,
        Number,
        Date,
        DateTime,
        Time,
        Dropdown,
        Radio,
        Checkbox,
        Email,
        Phone
    }

    // Template Field Model
    public record TemplateField(
        int Id,
        string Label,
        FieldType Type,
        bool IsRequired,
        int Order,
        string? Placeholder = null,
        string? DefaultValue = null,
        List<string>? Options = null, // For dropdown, radio
        string? ValidationRule = null,
        string? HelpText = null
    );

    // Custom Template Model
    public record CustomTemplate(
        int Id,
        string Name,
        string Category,
        string Description,
        List<TemplateField> Fields,
        DateTime CreatedDate,
        DateTime LastModified,
        string CreatedBy,
        bool IsSystemTemplate = false
    );

    // Storage for custom templates
    private List<CustomTemplate> _customTemplates = new();
    
    public List<CustomTemplate> CustomTemplates
    {
        get => _customTemplates;
        set
        {
            _customTemplates = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AllTemplates));
        }
    }

    // Combine system templates with custom templates
    public List<CustomTemplate> AllTemplates
    {
        get
        {
            var systemTemplates = GetSystemTemplates();
            return systemTemplates.Concat(_customTemplates).ToList();
        }
    }

    // Get system templates (predefined)
    private List<CustomTemplate> GetSystemTemplates()
    {
        return new List<CustomTemplate>
        {
            new CustomTemplate(
                1,
                "Initial Consultation",
                "Consultation",
                "Standard initial consultation template",
                new List<TemplateField>
                {
                    new(1, "Patient Name", FieldType.Text, true, 1, "Enter patient full name"),
                    new(2, "Patient ID", FieldType.Text, false, 2, "Patient ID (optional)"),
                    new(3, "Date", FieldType.Date, true, 3),
                    new(4, "Chief Complaint", FieldType.TextArea, true, 4, "Patient's main concern..."),
                    new(5, "Medical History", FieldType.TextArea, false, 5, "Past medical history..."),
                    new(6, "Allergies", FieldType.Text, false, 6, "Known allergies"),
                    new(7, "Physical Examination", FieldType.TextArea, false, 7, "Examination findings..."),
                    new(8, "Assessment", FieldType.TextArea, false, 8, "Clinical assessment..."),
                    new(9, "Treatment Plan", FieldType.TextArea, false, 9, "Recommended plan...")
                },
                DateTime.Now.AddDays(-30),
                DateTime.Now.AddDays(-30),
                "System",
                true
            )
        };
    }

    // State for template builder
    private CustomTemplate? _templateBeingEdited = null;
    private List<TemplateField> _templateFields = new();
    private string _templateName = "";
    private string _templateCategory = "Custom";
    private string _templateDescription = "";

    public CustomTemplate? TemplateBeingEdited
    {
        get => _templateBeingEdited;
        set
        {
            _templateBeingEdited = value;
            OnPropertyChanged();
        }
    }

    public List<TemplateField> TemplateFields
    {
        get => _templateFields;
        set
        {
            _templateFields = value;
            OnPropertyChanged();
        }
    }

    public string TemplateName
    {
        get => _templateName;
        set
        {
            _templateName = value;
            OnPropertyChanged();
        }
    }

    public string TemplateCategory
    {
        get => _templateCategory;
        set
        {
            _templateCategory = value;
            OnPropertyChanged();
        }
    }

    public string TemplateDescription
    {
        get => _templateDescription;
        set
        {
            _templateDescription = value;
            OnPropertyChanged();
        }
    }

    // Methods
    public void StartNewTemplate()
    {
        _templateBeingEdited = null;
        _templateFields = new List<TemplateField>();
        _templateName = "";
        _templateCategory = _documentTypes.Any() ? _documentTypes.First() : "Custom";
        _templateDescription = "";
        
        // Batch property change notification for better performance
        OnPropertyChanged(nameof(TemplateBeingEdited));
        OnPropertyChanged(nameof(TemplateFields));
        OnPropertyChanged(nameof(TemplateName));
        OnPropertyChanged(nameof(TemplateCategory));
        OnPropertyChanged(nameof(TemplateDescription));
    }

    public void EditTemplate(CustomTemplate template)
    {
        _templateBeingEdited = template;
        _templateFields = new List<TemplateField>(template.Fields);
        _templateName = template.Name;
        _templateCategory = template.Category;
        _templateDescription = template.Description;
        OnPropertyChanged(nameof(TemplateFields));
        OnPropertyChanged(nameof(TemplateName));
        OnPropertyChanged(nameof(TemplateCategory));
        OnPropertyChanged(nameof(TemplateDescription));
    }

    public void AddField(TemplateField field)
    {
        _templateFields.Add(field);
        OnPropertyChanged(nameof(TemplateFields));
        OnPropertyChanged();
    }

    public void RemoveField(int fieldId)
    {
        _templateFields.RemoveAll(f => f.Id == fieldId);
        OnPropertyChanged(nameof(TemplateFields));
    }

    public void UpdateFieldOrder(int fieldId, int newOrder)
    {
        if (newOrder < 0 || newOrder >= _templateFields.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newOrder), "Order must be within valid range");
        }

        var field = _templateFields.FirstOrDefault(f => f.Id == fieldId);
        if (field != null)
        {
            var index = _templateFields.IndexOf(field);
            if (index >= 0)
            {
                _templateFields.RemoveAt(index);
                _templateFields.Insert(newOrder, field);
                // Reorder all fields
                for (int i = 0; i < _templateFields.Count; i++)
                {
                    var f = _templateFields[i];
                    _templateFields[i] = f with { Order = i + 1 };
                }
                OnPropertyChanged(nameof(TemplateFields));
            }
        }
    }

    public void UpdateField(int fieldId, TemplateField updatedField)
    {
        var index = _templateFields.FindIndex(f => f.Id == fieldId);
        if (index >= 0)
        {
            _templateFields[index] = updatedField;
            OnPropertyChanged(nameof(TemplateFields));
        }
    }

    public CustomTemplate SaveTemplate()
    {
        if (string.IsNullOrWhiteSpace(_templateName) || !_templateFields.Any())
        {
            throw new InvalidOperationException("Template name and at least one field are required");
        }

        // Check for duplicate template names (case-insensitive, excluding the template being edited)
        var trimmedTemplateName = _templateName.Trim();
        var isDuplicate = AllTemplates.Any(t => 
            t.Id != _templateBeingEdited?.Id && 
            t.Name.Equals(trimmedTemplateName, StringComparison.OrdinalIgnoreCase));
        
        if (isDuplicate)
        {
            throw new InvalidOperationException($"A template with the name '{trimmedTemplateName}' already exists. Please choose a different name.");
        }

        // Generate next ID dynamically to avoid conflicts
        var existingIds = AllTemplates.Select(t => t.Id).ToHashSet();
        var nextId = 1;
        while (existingIds.Contains(nextId))
        {
            nextId++;
        }

        var template = new CustomTemplate(
            _templateBeingEdited?.Id ?? nextId,
            _templateName,
            _templateCategory,
            _templateDescription,
            _templateFields.OrderBy(f => f.Order).ToList(),
            _templateBeingEdited?.CreatedDate ?? DateTime.Now,
            DateTime.Now,
            "Current User",
            false
        );

        if (_templateBeingEdited != null)
        {
            // Update existing
            var index = _customTemplates.FindIndex(t => t.Id == _templateBeingEdited.Id);
            if (index >= 0)
            {
                _customTemplates[index] = template;
            }
        }
        else
        {
            // Add new
            _customTemplates.Add(template);
        }

        OnPropertyChanged(nameof(CustomTemplates));
        OnPropertyChanged(nameof(AllTemplates));
        
        // Clear editing state after saving
        _templateBeingEdited = null;
        OnPropertyChanged(nameof(TemplateBeingEdited));
        
        return template;
    }

    public void DeleteTemplate(int templateId)
    {
        _customTemplates.RemoveAll(t => t.Id == templateId);
        OnPropertyChanged(nameof(CustomTemplates));
        OnPropertyChanged(nameof(AllTemplates));
    }

    public CustomTemplate? GetTemplate(int templateId)
    {
        return AllTemplates.FirstOrDefault(t => t.Id == templateId);
    }

    // Document Types Management (shared across components)
    private List<string> _documentTypes = new()
    {
        "Treatment Note",
        "Consent Form",
        "Prescription",
        "Other"
    };

    public List<string> DocumentTypes
    {
        get => _documentTypes;
        set
        {
            _documentTypes = value;
            OnPropertyChanged();
        }
    }

    public void AddDocumentType(string typeName)
    {
        if (!string.IsNullOrWhiteSpace(typeName) && !_documentTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase))
        {
            _documentTypes.Add(typeName.Trim());
            OnPropertyChanged(nameof(DocumentTypes));
        }
    }

    public void UpdateDocumentType(string oldType, string newType)
    {
        var index = _documentTypes.FindIndex(t => t.Equals(oldType, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && !string.IsNullOrWhiteSpace(newType))
        {
            var trimmedNewType = newType.Trim();
            
            // Check for duplicates (excluding the current one being updated)
            var isDuplicate = _documentTypes.Where((t, i) => i != index)
                                          .Any(t => t.Equals(trimmedNewType, StringComparison.OrdinalIgnoreCase));
            
            if (!isDuplicate)
            {
                _documentTypes[index] = trimmedNewType;
                OnPropertyChanged(nameof(DocumentTypes));
            }
            else
            {
                throw new InvalidOperationException($"Document type '{trimmedNewType}' already exists");
            }
        }
    }

    public void RemoveDocumentType(string typeName)
    {
        _documentTypes.RemoveAll(t => t.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(DocumentTypes));
    }

    // ===== LOCALSTORAGE PERSISTENCE METHODS =====
    
    /// <summary>
    /// Save custom templates to browser localStorage
    /// </summary>
    public async Task SaveTemplatesToStorageAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            var json = JsonSerializer.Serialize(_customTemplates, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
            await _jsRuntime.InvokeVoidAsync("templateStorage.saveTemplates", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving templates to localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Load custom templates from browser localStorage
    /// </summary>
    public async Task LoadTemplatesFromStorageAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("templateStorage.loadTemplates");
            
            if (!string.IsNullOrEmpty(json))
            {
                var templates = JsonSerializer.Deserialize<List<CustomTemplate>>(json);
                if (templates != null)
                {
                    _customTemplates = templates;
                    OnPropertyChanged(nameof(CustomTemplates));
                    OnPropertyChanged(nameof(AllTemplates));
                    Console.WriteLine($"Loaded {templates.Count} custom templates from localStorage");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading templates from localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Save document types to browser localStorage
    /// </summary>
    public async Task SaveDocumentTypesToStorageAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            var json = JsonSerializer.Serialize(_documentTypes, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
            await _jsRuntime.InvokeVoidAsync("templateStorage.saveDocumentTypes", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving document types to localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Load document types from browser localStorage
    /// </summary>
    public async Task LoadDocumentTypesFromStorageAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("templateStorage.loadDocumentTypes");
            
            if (!string.IsNullOrEmpty(json))
            {
                var types = JsonSerializer.Deserialize<List<string>>(json);
                if (types != null && types.Any())
                {
                    _documentTypes = types;
                    OnPropertyChanged(nameof(DocumentTypes));
                    Console.WriteLine($"Loaded {types.Count} document types from localStorage");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading document types from localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all data from localStorage
    /// </summary>
    public async Task ClearStorageAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("templateStorage.clearAll");
            Console.WriteLine("Cleared all template data from localStorage");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing localStorage: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize - Load data from localStorage on startup
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadDocumentTypesFromStorageAsync();
        await LoadTemplatesFromStorageAsync();
    }
}

