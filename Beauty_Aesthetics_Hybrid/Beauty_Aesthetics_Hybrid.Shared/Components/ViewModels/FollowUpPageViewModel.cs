using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class FollowUpPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Models
    public record Patient(int Id, string Name, DateOnly Birthday, string Phone);
    public record FollowUp(int Id, int PatientId, DateTime Date, string Content, string Preview, string PatientName, string? ImageUrl = null, bool IsPinned = false);

    // Properties
    private Patient? _selectedPatient;
    private bool _isPatientInfoCollapsed = false;
    private string _searchQuery = "";
    private string _dateFilter = "all";
    private DateTime? _customStartDate = null;
    private DateTime? _customEndDate = null;
    private int? _currentlyViewingFollowUpId = null;

    public List<Patient> Patients { get; } = new();
    public List<FollowUp> FollowUpHistory { get; } = new();

    public int? CurrentlyViewingFollowUpId
    {
        get => _currentlyViewingFollowUpId;
        set
        {
            if (_currentlyViewingFollowUpId != value)
            {
                _currentlyViewingFollowUpId = value;
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
                OnPropertyChanged(nameof(FilteredFollowUpHistory));
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

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredFollowUpHistory));
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
                OnPropertyChanged(nameof(FilteredFollowUpHistory));
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
                OnPropertyChanged(nameof(FilteredFollowUpHistory));
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
                OnPropertyChanged(nameof(FilteredFollowUpHistory));
            }
        }
    }

    public bool IsCustomDateRange => DateFilter == "custom";

    // Computed Properties
    public IEnumerable<FollowUp> FilteredFollowUpHistory
    {
        get
        {
            var query = SelectedPatient == null
                ? FollowUpHistory.AsEnumerable()
                : FollowUpHistory.Where(n => n.PatientId == SelectedPatient.Id);

            // Apply search filter
            query = ApplySearch(query);

            // Apply date filter
            query = ApplyDateFilter(query);

            // Sort by pinned status and date
            return query.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.Date);
        }
    }

    // Constructor with mock data
    public FollowUpPageViewModel()
    {
        // Initialize mock patients with Malaysian phone numbers
        Patients.Add(new Patient(1, "Siti Aminah binti Abdullah", new DateOnly(1985, 3, 15), "013-846-3738"));
        Patients.Add(new Patient(2, "Muhammad Hafiz bin Rahman", new DateOnly(1992, 7, 22), "012-345-6789"));
        Patients.Add(new Patient(3, "Tan Wei Ling", new DateOnly(1978, 11, 8), "016-789-4321"));
        Patients.Add(new Patient(4, "Kumar Raj", new DateOnly(1990, 5, 20), "017-234-5678"));
        Patients.Add(new Patient(5, "Emily Wong", new DateOnly(1988, 9, 12), "019-876-5432"));

        // Initialize mock follow-up history
        FollowUpHistory.Add(new FollowUp(
            1,
            1,
            DateTime.Now.AddDays(-2),
            "<p><strong>Follow-up call:</strong> Patient reports skin condition improving. No adverse reactions. Advised to continue with skincare routine. Next appointment scheduled in 2 weeks.</p>",
            "Follow-up call: Patient reports skin condition improving. No adverse reactions...",
            "Siti Aminah binti Abdullah"
        ));

        FollowUpHistory.Add(new FollowUp(
            2,
            2,
            DateTime.Now.AddDays(-1),
            "<p><strong>Follow-up appointment:</strong> Patient completed 4th laser session. Progress is excellent. Minimal discomfort reported. Scheduled next session in 3 weeks.</p>",
            "Follow-up appointment: Patient completed 4th laser session. Progress is excellent...",
            "Muhammad Hafiz bin Rahman"
        ));

        FollowUpHistory.Add(new FollowUp(
            3,
            3,
            DateTime.Now.AddDays(-5),
            "<p><strong>Post-treatment follow-up:</strong> Botox treatment results are visible. Patient satisfied with forehead lines reduction. No side effects reported.</p>",
            "Post-treatment follow-up: Botox treatment results are visible. Patient satisfied...",
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

    public void TogglePatientInfo()
    {
        IsPatientInfoCollapsed = !IsPatientInfoCollapsed;
    }

    public void SaveFollowUp(string content)
    {
        if (SelectedPatient == null || string.IsNullOrWhiteSpace(content))
            return;

        // Optimize: Process preview and image extraction
        var preview = GeneratePreview(content);
        var imageUrl = ExtractFirstImage(content);

        // Optimize: Use Count for better performance than Any + Max
        var nextId = FollowUpHistory.Count > 0 ? FollowUpHistory.Max(n => n.Id) + 1 : 1;

        var newFollowUp = new FollowUp(
            nextId,
            SelectedPatient.Id,
            DateTime.Now,
            content,
            preview,
            SelectedPatient.Name,
            imageUrl
        );

        FollowUpHistory.Add(newFollowUp);
        
        // Optimize: Only notify once instead of triggering multiple property changes
        OnPropertyChanged(nameof(FilteredFollowUpHistory));
    }

    public void UpdateFollowUp(int id, string content)
    {
        // Optimize: Find index directly instead of finding followUp then index
        var index = FollowUpHistory.FindIndex(n => n.Id == id);
        if (index >= 0 && SelectedPatient != null)
        {
            var followUp = FollowUpHistory[index];
            
            // Optimize: Process preview and image extraction
            var preview = GeneratePreview(content);
            var imageUrl = ExtractFirstImage(content);

            // Create updated follow-up with same ID but new content
            var updatedFollowUp = new FollowUp(
                followUp.Id,
                followUp.PatientId,
                DateTime.Now, // Update timestamp
                content,
                preview,
                followUp.PatientName,
                imageUrl
            );

            // Direct index assignment is faster than IndexOf + assignment
            FollowUpHistory[index] = updatedFollowUp;
            OnPropertyChanged(nameof(FilteredFollowUpHistory));
        }
    }

    public void DeleteFollowUp(int id)
    {
        var followUp = FollowUpHistory.FirstOrDefault(n => n.Id == id);
        if (followUp != null)
        {
            FollowUpHistory.Remove(followUp);
            OnPropertyChanged(nameof(FilteredFollowUpHistory));
        }
    }

    public void TogglePin(int id)
    {
        var followUp = FollowUpHistory.FirstOrDefault(n => n.Id == id);
        if (followUp != null)
        {
            // Create a new follow-up with toggled pin status
            var updatedFollowUp = new FollowUp(
                followUp.Id,
                followUp.PatientId,
                followUp.Date,
                followUp.Content,
                followUp.Preview,
                followUp.PatientName,
                followUp.ImageUrl,
                !followUp.IsPinned // Toggle the pin status
            );

            // Replace old follow-up with updated one
            var index = FollowUpHistory.IndexOf(followUp);
            FollowUpHistory[index] = updatedFollowUp;
            OnPropertyChanged(nameof(FilteredFollowUpHistory));
        }
    }

    private IEnumerable<FollowUp> ApplySearch(IEnumerable<FollowUp> followUps)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return followUps;

        var query = SearchQuery.ToLower().Trim();
        return followUps.Where(f =>
            f.PatientName.ToLower().Contains(query) ||
            f.Preview.ToLower().Contains(query) ||
            f.Content.ToLower().Contains(query) ||
            f.Date.ToString("dd/MM/yyyy").Contains(query) ||
            f.Date.ToString("MMM dd, yyyy").ToLower().Contains(query) ||
            f.Date.ToString("MMMM").ToLower().Contains(query) ||
            f.Date.Year.ToString().Contains(query));
    }

    private IEnumerable<FollowUp> ApplyDateFilter(IEnumerable<FollowUp> followUps)
    {
        var now = DateTime.Now;

        return DateFilter switch
        {
            "today" => followUps.Where(f => f.Date.Date == now.Date),
            "yesterday" => followUps.Where(f => f.Date.Date == now.AddDays(-1).Date),
            "last7days" => followUps.Where(f => f.Date.Date >= now.AddDays(-7).Date),
            "last30days" => followUps.Where(f => f.Date.Date >= now.AddDays(-30).Date),
            "thisMonth" => followUps.Where(f => f.Date.Year == now.Year && f.Date.Month == now.Month),
            "lastMonth" => followUps.Where(f =>
            {
                var lastMonth = now.AddMonths(-1);
                return f.Date.Year == lastMonth.Year && f.Date.Month == lastMonth.Month;
            }),
            "custom" => ApplyCustomDateRange(followUps),
            _ => followUps // "all" or default
        };
    }

    private IEnumerable<FollowUp> ApplyCustomDateRange(IEnumerable<FollowUp> followUps)
    {
        if (CustomStartDate.HasValue && CustomEndDate.HasValue)
        {
            var startDate = CustomStartDate.Value.Date;
            var endDate = CustomEndDate.Value.Date.AddDays(1).AddTicks(-1); // End of day
            return followUps.Where(f => f.Date >= startDate && f.Date <= endDate);
        }
        else if (CustomStartDate.HasValue)
        {
            return followUps.Where(f => f.Date.Date >= CustomStartDate.Value.Date);
        }
        else if (CustomEndDate.HasValue)
        {
            var endDate = CustomEndDate.Value.Date.AddDays(1).AddTicks(-1);
            return followUps.Where(f => f.Date <= endDate);
        }

        return followUps;
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
