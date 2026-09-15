using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class DocumentViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ===== MODELS =====
    public record Document(
        int Id,
        string Title,
        string Type,
        DateTime CreatedDate,
        DateTime LastUpdated,
        string UploadedBy,
        string? FilePath = null,
        string? Content = null,
        int? TemplateId = null
    );

    // ===== STATE PROPERTIES =====
    private List<Document> _documents = new();
    private string _searchQuery = string.Empty;
    private bool _showAllTypes = true;
    private HashSet<string> _selectedTypes = new();
    private string _sortBy = "CreatedDate"; // "CreatedDate" or "LastUpdated"
    private bool _sortAscending = false; // false = descending (newest first), true = ascending (oldest first)
    private int _currentPage = 1;
    private const int PageSize = 10;
    private string _dateFilterField = "CreatedDate"; // "CreatedDate" or "LastUpdated"
    private string _dateFilterPreset = "All"; // All, ThisWeek, ThisMonth, Custom
    private DateTime? _customDateFrom;
    private DateTime? _customDateTo;

    // Document list
    public List<Document> Documents
    {
        get => _documents;
        set
        {
            if (_documents != value)
            {
                _documents = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
            }
        }
    }

    // Search query
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                // Reset to first page when search changes
                CurrentPage = 1;
            }
        }
    }

    // Show all types filter
    public bool ShowAllTypes
    {
        get => _showAllTypes;
        set
        {
            if (_showAllTypes != value)
            {
                _showAllTypes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                // Reset to first page when filter changes
                CurrentPage = 1;
            }
        }
    }

    // Date filter field (CreatedDate or LastUpdated)
    public string DateFilterField
    {
        get => _dateFilterField;
        set
        {
            if (_dateFilterField != value)
            {
                _dateFilterField = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                CurrentPage = 1;
            }
        }
    }

    // Date filter preset (All, ThisWeek, ThisMonth, Custom)
    public string DateFilterPreset
    {
        get => _dateFilterPreset;
        set
        {
            if (_dateFilterPreset != value)
            {
                _dateFilterPreset = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                CurrentPage = 1;
            }
        }
    }

    // Custom date range (used when DateFilterPreset == "Custom")
    public DateTime? CustomDateFrom
    {
        get => _customDateFrom;
        set
        {
            if (_customDateFrom != value)
            {
                _customDateFrom = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                CurrentPage = 1;
            }
        }
    }

    public DateTime? CustomDateTo
    {
        get => _customDateTo;
        set
        {
            if (_customDateTo != value)
            {
                _customDateTo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                CurrentPage = 1;
            }
        }
    }

    // Selected types filter
    public HashSet<string> SelectedTypes
    {
        get => _selectedTypes;
        set
        {
            if (_selectedTypes != value)
            {
                _selectedTypes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(TotalDocuments));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PaginatedDocuments));
                // Reset to first page when filter changes
                CurrentPage = 1;
            }
        }
    }

    // Sort by column
    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (_sortBy != value)
            {
                _sortBy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(PaginatedDocuments));
                // Reset to first page when sort changes
                CurrentPage = 1;
            }
        }
    }

    // Sort direction
    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (_sortAscending != value)
            {
                _sortAscending = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDocuments));
                OnPropertyChanged(nameof(PaginatedDocuments));
                // Reset to first page when sort changes
                CurrentPage = 1;
            }
        }
    }

    // Current page
    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (value >= 1 && value <= TotalPages)
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PaginatedDocuments));
                }
            }
        }
    }

    // Page size (constant)
    public int ItemsPerPage => PageSize;

    // ===== COMPUTED PROPERTIES =====
    public IEnumerable<Document> FilteredDocuments
    {
        get
        {
            var filtered = Documents.AsEnumerable();

            // Search filter - optimized for performance
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.Trim().ToLowerInvariant();
                filtered = filtered.Where(d => 
                    d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    d.Type.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    d.UploadedBy.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            // Type filter
            if (!ShowAllTypes && SelectedTypes.Any())
            {
                filtered = filtered.Where(d => SelectedTypes.Contains(d.Type));
            }

            // Date filter
            if (DateFilterPreset != "All")
            {
                DateTime? rangeStart = null;
                DateTime? rangeEnd = null;
                var now = DateTime.Now;

                if (DateFilterPreset == "ThisWeek")
                {
                    // Start from Monday of the current week
                    var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    rangeStart = now.Date.AddDays(-diff);
                    rangeEnd = now.Date.AddDays(1).AddTicks(-1);
                }
                else if (DateFilterPreset == "ThisMonth")
                {
                    rangeStart = new DateTime(now.Year, now.Month, 1);
                    rangeEnd = rangeStart.Value.AddMonths(1).AddTicks(-1);
                }
                else if (DateFilterPreset == "Custom")
                {
                    if (CustomDateFrom.HasValue)
                    {
                        rangeStart = CustomDateFrom.Value.Date;
                    }
                    if (CustomDateTo.HasValue)
                    {
                        rangeEnd = CustomDateTo.Value.Date.AddDays(1).AddTicks(-1);
                    }
                }

                if (rangeStart.HasValue || rangeEnd.HasValue)
                {
                    filtered = filtered.Where(d =>
                    {
                        var date = DateFilterField == "LastUpdated" ? d.LastUpdated : d.CreatedDate;
                        var inStart = !rangeStart.HasValue || date >= rangeStart.Value;
                        var inEnd = !rangeEnd.HasValue || date <= rangeEnd.Value;
                        return inStart && inEnd;
                    });
                }
            }

            // Sort by selected column
            if (SortBy == "CreatedDate")
            {
                return SortAscending 
                    ? filtered.OrderBy(d => d.CreatedDate)
                    : filtered.OrderByDescending(d => d.CreatedDate);
            }
            else // LastUpdated
            {
                return SortAscending 
                    ? filtered.OrderBy(d => d.LastUpdated)
                    : filtered.OrderByDescending(d => d.LastUpdated);
            }
        }
    }

    public int TotalDocuments => FilteredDocuments.Count();
    
    public int TotalPages => TotalDocuments == 0 ? 1 : (int)Math.Ceiling(TotalDocuments / (double)PageSize);
    
    public IEnumerable<Document> PaginatedDocuments
    {
        get
        {
            var filtered = FilteredDocuments.ToList();
            var total = filtered.Count;
            if (total == 0) return Enumerable.Empty<Document>();
            
            // Ensure current page is valid
            var totalPages = TotalPages;
            if (_currentPage > totalPages && totalPages > 0)
            {
                _currentPage = totalPages;
                OnPropertyChanged(nameof(CurrentPage)); // Notify UI of page change
            }
            
            var skip = (_currentPage - 1) * PageSize;
            return filtered.Skip(skip).Take(PageSize);
        }
    }

    // ===== METHODS =====
    
    // Toggle all types filter
    public void ToggleAllTypes(bool isChecked)
    {
        ShowAllTypes = isChecked;
        if (isChecked)
        {
            // Clear individual selections when showing all types
            SelectedTypes = new HashSet<string>();
        }
    }

    // Toggle individual type
    public void ToggleType(string type, bool isSelected)
    {
        var newSelectedTypes = new HashSet<string>(SelectedTypes);
        if (isSelected)
        {
            newSelectedTypes.Add(type);
            ShowAllTypes = false;
        }
        else
        {
            newSelectedTypes.Remove(type);
            ShowAllTypes = false;
        }

        // If no specific types remain selected, revert to all types
        if (!newSelectedTypes.Any())
        {
            ShowAllTypes = true;
        }

        SelectedTypes = newSelectedTypes;
    }

    // Set sort order
    public void SetSortOrder(string sortBy)
    {
        // If clicking the same column, toggle ascending/descending
        if (SortBy == sortBy)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            // If clicking a different column, set it as the new sort column (default to descending)
            SortBy = sortBy;
            SortAscending = false;
        }
    }

    // Pagination methods
    public void GoToPage(int page)
    {
        if (page >= 1 && page <= TotalPages)
        {
            CurrentPage = page;
        }
    }

    public void GoToNextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
        }
    }

    public void GoToPreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
        }
    }

    public void ResetPagination()
    {
        CurrentPage = 1;
    }

    // CRUD Operations
    public void AddDocument(Document document)
    {
        var newDocuments = new List<Document>(Documents);
        newDocuments.Add(document);
        Documents = newDocuments;
    }

    public void UpdateDocument(int documentId, Document updatedDocument)
    {
        var newDocuments = new List<Document>(Documents);
        var index = newDocuments.FindIndex(d => d.Id == documentId);
        if (index >= 0)
        {
            newDocuments[index] = updatedDocument;
            Documents = newDocuments;
        }
    }

    public void RemoveDocument(int documentId)
    {
        var newDocuments = new List<Document>(Documents);
        newDocuments.RemoveAll(d => d.Id == documentId);
        Documents = newDocuments;
    }

    public Document? GetDocument(int documentId)
    {
        return Documents.FirstOrDefault(d => d.Id == documentId);
    }

    // Initialize with default selected types
    public void InitializeSelectedTypes(List<string> availableTypes)
    {
        SelectedTypes = new HashSet<string>(availableTypes);
    }

    // Initialize mock data (can be moved to a service later)
    public void InitializeMockData()
    {
        var mockDocuments = new List<Document>
        {
            new Document(1, "Initial Consultation Report", "Treatment Note", DateTime.Now.AddDays(-10), DateTime.Now.AddDays(-10), "Dr. Sarah Lee"),
            new Document(2, "Consent Form - Laser Treatment", "Consent Form", DateTime.Now.AddDays(-5), DateTime.Now.AddDays(-5), "Dr. Sarah Lee"),
            new Document(3, "Prescription - Acne Treatment", "Prescription", DateTime.Now.AddDays(-3), DateTime.Now.AddDays(-3), "Dr. Sarah Lee"),
            new Document(4, "Follow-up Visit - Botox Treatment", "Treatment Note", DateTime.Now.AddDays(-8), DateTime.Now.AddDays(-8), "Dr. Sarah Lee"),
            new Document(5, "Skin Assessment Report", "Treatment Note", DateTime.Now.AddDays(-7), DateTime.Now.AddDays(-7), "Dr. Sarah Lee"),
            new Document(6, "Consent Form - Dermal Filler", "Consent Form", DateTime.Now.AddDays(-6), DateTime.Now.AddDays(-6), "Dr. Sarah Lee"),
            new Document(7, "Prescription - Pain Relief", "Prescription", DateTime.Now.AddDays(-4), DateTime.Now.AddDays(-4), "Dr. Sarah Lee"),
            new Document(8, "Post-Treatment Review", "Treatment Note", DateTime.Now.AddDays(-2), DateTime.Now.AddDays(-2), "Dr. Sarah Lee"),
            new Document(9, "Pre-Treatment Consultation", "Treatment Note", DateTime.Now.AddDays(-9), DateTime.Now.AddDays(-9), "Dr. Sarah Lee"),
            new Document(10, "Consent Form - Chemical Peel", "Consent Form", DateTime.Now.AddDays(-1), DateTime.Now.AddDays(-1), "Dr. Sarah Lee"),
            new Document(11, "Prescription - Antibiotic", "Prescription", DateTime.Now, DateTime.Now, "Dr. Sarah Lee")
        };

        Documents = mockDocuments;
    }
}

