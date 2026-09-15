using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class CaseNotePageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Models
    public record Patient(int Id, string Name, DateOnly Birthday, string Phone);
    public record CaseNote(int Id, int PatientId, DateTime Date, string Content, string Preview, string PatientName, string? ImageUrl = null, bool IsPinned = false);

    // Properties
    private Patient? _selectedPatient;
    private string _activeTab = "case-notes";
    private bool _isPatientInfoCollapsed = false;
    private bool _isTabsSectionCollapsed = false;
    private string _searchQuery = "";
    private string _dateFilter = "all";
    private DateTime? _customStartDate = null;
    private DateTime? _customEndDate = null;
    private int? _currentlyViewingNoteId = null;

    public List<Patient> Patients { get; } = new();
    public List<CaseNote> CaseHistory { get; } = new();

    public int? CurrentlyViewingNoteId
    {
        get => _currentlyViewingNoteId;
        set
        {
            if (_currentlyViewingNoteId != value)
            {
                _currentlyViewingNoteId = value;
                OnPropertyChanged();
            }
        }
    }

    public Patient? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            if (_selectedPatient != value)
            {
                _selectedPatient = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredCaseHistory));
                OnPropertyChanged(nameof(SelectedPatientId));
            }
        }
    }

    public int SelectedPatientId
    {
        get => SelectedPatient?.Id ?? (Patients.FirstOrDefault()?.Id ?? 0);
        set
        {
            var patient = Patients.FirstOrDefault(p => p.Id == value);
            if (patient != null)
            {
                SelectedPatient = patient;
            }
        }
    }

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

    public bool IsPatientInfoCollapsed
    {
        get => _isPatientInfoCollapsed;
        set
        {
            if (_isPatientInfoCollapsed != value)
            {
                _isPatientInfoCollapsed = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsTabsSectionCollapsed
    {
        get => _isTabsSectionCollapsed;
        set
        {
            if (_isTabsSectionCollapsed != value)
            {
                _isTabsSectionCollapsed = value;
                OnPropertyChanged();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredCaseHistory));
            }
        }
    }

    public string DateFilter
    {
        get => _dateFilter;
        set
        {
            if (_dateFilter != value)
            {
                _dateFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredCaseHistory));
                OnPropertyChanged(nameof(IsCustomDateRange));
            }
        }
    }

    public DateTime? CustomStartDate
    {
        get => _customStartDate;
        set
        {
            if (_customStartDate != value)
            {
                _customStartDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredCaseHistory));
            }
        }
    }

    public DateTime? CustomEndDate
    {
        get => _customEndDate;
        set
        {
            if (_customEndDate != value)
            {
                _customEndDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredCaseHistory));
            }
        }
    }

    public bool IsCustomDateRange => DateFilter == "custom";

    // Computed Properties
    public IEnumerable<CaseNote> FilteredCaseHistory
    {
        get
        {
            var query = SelectedPatient == null
                ? CaseHistory.AsEnumerable()
                : CaseHistory.Where(n => n.PatientId == SelectedPatient.Id);

            // Apply search filter
            query = ApplySearch(query);

            // Apply date filter
            query = ApplyDateFilter(query);

            // Sort by pinned status and date
            return query.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.Date);
        }
    }

    // Constructor with mock data
    public CaseNotePageViewModel()
    {
        // Initialize mock patients with Malaysian phone numbers
        Patients.Add(new Patient(1, "Siti Aminah binti Abdullah", new DateOnly(1985, 3, 15), "013-846-3738"));
        Patients.Add(new Patient(2, "Muhammad Hafiz bin Rahman", new DateOnly(1992, 7, 22), "012-345-6789"));
        Patients.Add(new Patient(3, "Tan Wei Ling", new DateOnly(1978, 11, 8), "016-789-4321"));
        Patients.Add(new Patient(4, "Kumar Raj", new DateOnly(1990, 5, 20), "017-234-5678"));
        Patients.Add(new Patient(5, "Emily Wong", new DateOnly(1988, 9, 12), "019-876-5432"));

        // Initialize mock case history
        CaseHistory.Add(new CaseNote(
            1,
            1,
            DateTime.Now.AddDays(-5),
            "<p>Patient reports improvement in skin condition after the treatment. Minimal redness observed. Recommended to continue with prescribed skincare routine.</p>",
            "Patient reports improvement in skin condition after the treatment...",
            "Siti Aminah binti Abdullah"
        ));

        CaseHistory.Add(new CaseNote(
            2,
            1,
            DateTime.Now.AddDays(-12),
            "<p><strong>Initial consultation</strong> for acne treatment. Patient has moderate acne on cheeks and forehead. Prescribed topical treatment and advised dietary changes.</p>",
            "Initial consultation for acne treatment. Patient has moderate acne...",
            "Siti Aminah binti Abdullah"
        ));

        CaseHistory.Add(new CaseNote(
            3,
            2,
            DateTime.Now.AddDays(-3),
            "<p>Follow-up visit for laser hair removal. Treatment progressing well. <em>Fourth session completed</em> today. Patient satisfied with results.</p>",
            "Follow-up visit for laser hair removal. Treatment progressing well...",
            "Muhammad Hafiz bin Rahman"
        ));

        CaseHistory.Add(new CaseNote(
            4,
            2,
            DateTime.Now.AddDays(-24),
            "<p>Started laser hair removal treatment course. Patient tolerated the procedure well. Scheduled for 6 sessions with 3-week intervals.</p>",
            "Started laser hair removal treatment course. Patient tolerated...",
            "Muhammad Hafiz bin Rahman"
        ));

        CaseHistory.Add(new CaseNote(
            5,
            3,
            DateTime.Now.AddDays(-7),
            "<p>Botox treatment for forehead lines. <strong>20 units administered</strong>. Patient advised to avoid lying down for 4 hours. Follow-up scheduled in 2 weeks.</p>",
            "Botox treatment for forehead lines. 20 units administered...",
            "Tan Wei Ling"
        ));

        // Set default selected patient
        SelectedPatient = Patients.FirstOrDefault();
    }

    // Methods
    public void SelectPatient(Patient patient)
    {
        SelectedPatient = patient;
    }

    public void SelectTab(string tab)
    {
        ActiveTab = tab;
    }

    public void TogglePatientInfo()
    {
        IsPatientInfoCollapsed = !IsPatientInfoCollapsed;
    }

    public void ToggleTabsSection()
    {
        IsTabsSectionCollapsed = !IsTabsSectionCollapsed;
    }

    public void SaveNote(string content)
    {
        if (SelectedPatient == null || string.IsNullOrWhiteSpace(content))
            return;

        // Optimize: Process preview and image extraction in parallel
        var preview = GeneratePreview(content);
        var imageUrl = ExtractFirstImage(content);

        // Optimize: Use Count for better performance than Any + Max
        var nextId = CaseHistory.Count > 0 ? CaseHistory.Max(n => n.Id) + 1 : 1;

        var newNote = new CaseNote(
            nextId,
            SelectedPatient.Id,
            DateTime.Now,
            content,
            preview,
            SelectedPatient.Name,
            imageUrl
        );

        CaseHistory.Add(newNote);
        
        // Optimize: Only notify once instead of triggering multiple property changes
        OnPropertyChanged(nameof(FilteredCaseHistory));
    }

    public void UpdateNote(int id, string content)
    {
        // Optimize: Find index directly instead of finding note then index
        var index = CaseHistory.FindIndex(n => n.Id == id);
        if (index >= 0 && SelectedPatient != null)
        {
            var note = CaseHistory[index];
            
            // Optimize: Process preview and image extraction
            var preview = GeneratePreview(content);
            var imageUrl = ExtractFirstImage(content);

            // Create updated note with same ID but new content
            var updatedNote = new CaseNote(
                note.Id,
                note.PatientId,
                DateTime.Now, // Update timestamp
                content,
                preview,
                note.PatientName,
                imageUrl
            );

            // Direct index assignment is faster than IndexOf + assignment
            CaseHistory[index] = updatedNote;
            OnPropertyChanged(nameof(FilteredCaseHistory));
        }
    }

    public void DeleteNote(int id)
    {
        var note = CaseHistory.FirstOrDefault(n => n.Id == id);
        if (note != null)
        {
            CaseHistory.Remove(note);
            OnPropertyChanged(nameof(FilteredCaseHistory));
        }
    }

    public void TogglePin(int id)
    {
        var note = CaseHistory.FirstOrDefault(n => n.Id == id);
        if (note != null)
        {
            // Create a new note with toggled pin status
            var updatedNote = new CaseNote(
                note.Id,
                note.PatientId,
                note.Date,
                note.Content,
                note.Preview,
                note.PatientName,
                note.ImageUrl,
                !note.IsPinned // Toggle the pin status
            );

            // Replace old note with updated one
            var index = CaseHistory.IndexOf(note);
            CaseHistory[index] = updatedNote;
            OnPropertyChanged(nameof(FilteredCaseHistory));
        }
    }

    private IEnumerable<CaseNote> ApplySearch(IEnumerable<CaseNote> notes)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return notes;

        var query = SearchQuery.ToLower().Trim();
        return notes.Where(n =>
            n.PatientName.ToLower().Contains(query) ||
            n.Preview.ToLower().Contains(query) ||
            n.Content.ToLower().Contains(query) ||
            n.Date.ToString("dd/MM/yyyy").Contains(query) ||
            n.Date.ToString("MMM dd, yyyy").ToLower().Contains(query) ||
            n.Date.ToString("MMMM").ToLower().Contains(query) ||
            n.Date.Year.ToString().Contains(query));
    }

    private IEnumerable<CaseNote> ApplyDateFilter(IEnumerable<CaseNote> notes)
    {
        var now = DateTime.Now;

        return DateFilter switch
        {
            "today" => notes.Where(n => n.Date.Date == now.Date),
            "yesterday" => notes.Where(n => n.Date.Date == now.AddDays(-1).Date),
            "last7days" => notes.Where(n => n.Date.Date >= now.AddDays(-7).Date),
            "last30days" => notes.Where(n => n.Date.Date >= now.AddDays(-30).Date),
            "thisMonth" => notes.Where(n => n.Date.Year == now.Year && n.Date.Month == now.Month),
            "lastMonth" => notes.Where(n =>
            {
                var lastMonth = now.AddMonths(-1);
                return n.Date.Year == lastMonth.Year && n.Date.Month == lastMonth.Month;
            }),
            "custom" => ApplyCustomDateRange(notes),
            _ => notes // "all" or default
        };
    }

    private IEnumerable<CaseNote> ApplyCustomDateRange(IEnumerable<CaseNote> notes)
    {
        if (CustomStartDate.HasValue && CustomEndDate.HasValue)
        {
            var startDate = CustomStartDate.Value.Date;
            var endDate = CustomEndDate.Value.Date.AddDays(1).AddTicks(-1); // End of day
            return notes.Where(n => n.Date >= startDate && n.Date <= endDate);
        }
        else if (CustomStartDate.HasValue)
        {
            return notes.Where(n => n.Date.Date >= CustomStartDate.Value.Date);
        }
        else if (CustomEndDate.HasValue)
        {
            var endDate = CustomEndDate.Value.Date.AddDays(1).AddTicks(-1);
            return notes.Where(n => n.Date <= endDate);
        }

        return notes;
    }

    public void ClearFilters()
    {
        SearchQuery = "";
        DateFilter = "all";
        CustomStartDate = null;
        CustomEndDate = null;
    }

    // Cached regex for better performance
    private static readonly System.Text.RegularExpressions.Regex HtmlTagRegex = 
        new System.Text.RegularExpressions.Regex("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex ImageSrcRegex = 
        new System.Text.RegularExpressions.Regex(@"<img[^>]+src=[""']([^""']+)[""']", 
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Use cached compiled regex for better performance
        var text = HtmlTagRegex.Replace(html, string.Empty);
        return text.Trim();
    }
    
    private string GeneratePreview(string content)
    {
        var preview = StripHtml(content);
        if (preview.Length > 100)
        {
            // Use AsSpan for better performance with substring operations
            return string.Concat(preview.AsSpan(0, 100), "...");
        }
        return preview;
    }

    private string? ExtractFirstImage(string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        // Use cached compiled regex for better performance
        var match = ImageSrcRegex.Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }
}
