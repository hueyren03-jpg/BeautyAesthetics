using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class CaseNoteTemplateViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Model
    public record Template(int Id, string Name, string Category, string Description, DateTime CreatedDate);

    // Sample Templates
    public List<Template> Templates { get; } = new()
    {
        new Template(1, "Initial Consultation", "Consultation", "Standard initial patient consultation template", DateTime.Now.AddDays(-30)),
        new Template(2, "Botox Treatment", "Treatment", "Botox injection treatment notes", DateTime.Now.AddDays(-25)),
        new Template(3, "Dermal Filler", "Treatment", "Dermal filler procedure template", DateTime.Now.AddDays(-20)),
        new Template(4, "Skin Assessment", "Assessment", "Comprehensive skin condition assessment", DateTime.Now.AddDays(-15)),
        new Template(5, "Follow-up Visit", "Follow-up", "Standard follow-up appointment notes", DateTime.Now.AddDays(-10)),
        new Template(6, "Laser Treatment", "Treatment", "Laser skin treatment procedure", DateTime.Now.AddDays(-8)),
        new Template(7, "Chemical Peel", "Treatment", "Chemical peel treatment notes", DateTime.Now.AddDays(-5)),
        new Template(8, "Microneedling", "Treatment", "Microneedling procedure template", DateTime.Now.AddDays(-3)),
        new Template(9, "Post-Treatment Review", "Follow-up", "Post-treatment assessment and review", DateTime.Now.AddDays(-2)),
        new Template(10, "Consultation - Advanced", "Consultation", "Advanced consultation for complex cases", DateTime.Now.AddDays(-1)),
    };

    // State Properties
    private string _searchTerm = string.Empty;
    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm != value)
            {
                _searchTerm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredTemplates));
            }
        }
    }

    private Template? _selectedTemplate;
    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (_selectedTemplate != value)
            {
                _selectedTemplate = value;
                OnPropertyChanged();
            }
        }
    }

    public HashSet<string> SelectedCategories { get; } = new();
    public bool ShowAllCategories { get; set; } = true;

    // Computed Properties
    public IEnumerable<Template> FilteredTemplates
    {
        get
        {
            var filtered = Templates.AsEnumerable();

            // Filter by search term
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filtered = filtered.Where(t =>
                    t.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    t.Category.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by category
            if (!ShowAllCategories && SelectedCategories.Any())
            {
                filtered = filtered.Where(t => SelectedCategories.Contains(t.Category));
            }

            return filtered;
        }
    }

    public IEnumerable<string> Categories =>
        Templates.Select(t => t.Category).Distinct().OrderBy(c => c);

    // Methods
    public void SelectTemplate(Template template)
    {
        SelectedTemplate = template;
        OnPropertyChanged(nameof(SelectedTemplate));
    }

    public void ClearSelection()
    {
        SelectedTemplate = null;
        OnPropertyChanged(nameof(SelectedTemplate));
    }

    public void ToggleAllCategories(bool isChecked)
    {
        ShowAllCategories = isChecked;
        if (isChecked)
        {
            SelectedCategories.Clear();
        }
        OnPropertyChanged(nameof(ShowAllCategories));
        OnPropertyChanged(nameof(FilteredTemplates));
    }

    public void ToggleCategory(string category, bool isChecked)
    {
        if (isChecked)
        {
            SelectedCategories.Add(category);
            ShowAllCategories = false;
        }
        else
        {
            SelectedCategories.Remove(category);
            if (!SelectedCategories.Any())
            {
                ShowAllCategories = true;
            }
        }
        OnPropertyChanged(nameof(ShowAllCategories));
        OnPropertyChanged(nameof(FilteredTemplates));
    }

    public void UseTemplate(Template template)
    {
        SelectedTemplate = template;
        // Navigate to case note creation with this template
        Console.WriteLine($"Using template: {template.Name}");
    }

    public void EditTemplate(Template template)
    {
        SelectedTemplate = template;
        // Navigate to template editor
        Console.WriteLine($"Editing template: {template.Name}");
    }

    public void CreateNewTemplate()
    {
        // Navigate to template creator
        Console.WriteLine("Creating new template");
    }
}
