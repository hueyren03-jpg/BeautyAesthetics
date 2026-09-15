using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class CaseNotePrescriptionViewModel : INotifyPropertyChanged
{
    // Initialize Drugs from shared list
    public CaseNotePrescriptionViewModel()
    {
        // Sync with shared drugs list - convert to this ViewModel's Drug type
        Drugs = new List<Drug>(CaseNotePrescriptionBuilderViewModel.SharedDrugs.Select(d => 
            new Drug(d.Id, d.Name, d.DrugCode, d.Category, d.Indication, d.Restrictions, d.Dosage)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Models
    public record Drug(int Id, string Name, string DrugCode, string Category, string Indication, string Restrictions, string Dosage);
    public record SelectedDrug(int Id, Drug Drug, decimal Quantity, string Frequency, string Duration, string Reason, string Notes);
    public record Prescription(int Id, string PrescriptionNumber, DateTime Date, string Type, List<SelectedDrug> Drugs, string Status);

    // Drug Library - Shared with CaseNotePrescriptionBuilderViewModel
    public List<Drug> Drugs { get; private set; } = new();
    
    public List<string> AvailableCategories =>
        CaseNotePrescriptionBuilderViewModel.SharedCategories.OrderBy(c => c).ToList();

    // State Properties
    private string _searchTerm = string.Empty;
    private string _selectedCategory = string.Empty;
    private Drug? _selectedDrugToAdd;

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm != value)
            {
                _searchTerm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDrugs)); // ← Update filtered list
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory != value)
            {
                _selectedCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDrugs)); // ← Update filtered list
            }
        }
    }

    public Drug? SelectedDrugToAdd
    {
        get => _selectedDrugToAdd;
        set
        {
            if (_selectedDrugToAdd != value)
            {
                _selectedDrugToAdd = value;
                OnPropertyChanged();
            }
        }
    }

    public List<SelectedDrug> SelectedDrugs { get; set; } = new();
    public List<Prescription> Prescriptions { get; set; } = new();
    
    private decimal _newDrugQuantity = 1.00m;
    private string _newDrugFrequency = string.Empty;
    private string _newDrugDuration = string.Empty;
    private string _newDrugReason = string.Empty;
    private string _newDrugNotes = string.Empty;

    public decimal NewDrugQuantity
    {
        get => _newDrugQuantity;
        set
        {
            if (_newDrugQuantity != value)
            {
                _newDrugQuantity = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewDrugFrequency
    {
        get => _newDrugFrequency;
        set
        {
            if (_newDrugFrequency != value)
            {
                _newDrugFrequency = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewDrugDuration
    {
        get => _newDrugDuration;
        set
        {
            if (_newDrugDuration != value)
            {
                _newDrugDuration = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewDrugReason
    {
        get => _newDrugReason;
        set
        {
            if (_newDrugReason != value)
            {
                _newDrugReason = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewDrugNotes
    {
        get => _newDrugNotes;
        set
        {
            if (_newDrugNotes != value)
            {
                _newDrugNotes = value;
                OnPropertyChanged();
            }
        }
    }

    // Dropdown Options
    public List<string> FrequencyOptions { get; } = new()
    {
        "ONCE DAILY",
        "TWO TIMES DAILY",
        "THREE TIMES DAILY",
        "FOUR TIMES DAILY",
        "FIVE TIMES DAILY",
        "WHEN NEEDED",
        "EVERY 4 HOURS",
        "EVERY 6 HOURS",
        "EVERY 8 HOURS",
        "EVERY 12 HOURS",
        "BEFORE MEALS",
        "AFTER MEALS",
        "AT BEDTIME"
    };

    public List<string> DurationOptions { get; } = new()
    {
        "1 DAY",
        "3 DAYS",
        "5 DAYS",
        "7 DAYS",
        "10 DAYS",
        "14 DAYS",
        "1 WEEK",
        "2 WEEKS",
        "3 WEEKS",
        "1 MONTH",
        "2 MONTHS",
        "3 MONTHS",
        "ONGOING"
    };

    public List<string> ReasonOptions { get; } = new()
    {
        "PAIN RELIEF",
        "FEVER",
        "HEADACHE",
        "INFECTION",
        "INFLAMMATION",
        "ALLERGY",
        "HYPERTENSION",
        "DIABETES",
        "ANXIETY",
        "DEPRESSION",
        "INSOMNIA",
        "NAUSEA",
        "COUGH",
        "COLD",
        "SKIN CONDITION",
        "DIGESTIVE ISSUES",
        "OTHER"
    };

    // Computed Properties
    public IEnumerable<Drug> FilteredDrugs =>
        Drugs.Where(d => 
            (string.IsNullOrWhiteSpace(SearchTerm) ||
             d.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
             d.DrugCode.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
             d.Category.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(SelectedCategory) ||
             d.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)));

    // Methods
    public void SelectDrugToAdd(Drug drug)
    {
        SelectedDrugToAdd = drug;
        
        // Reset form fields when selecting a new drug
        NewDrugQuantity = 1.00m;
        NewDrugFrequency = string.Empty;
        NewDrugDuration = string.Empty;
        NewDrugReason = string.Empty;
        NewDrugNotes = string.Empty;
    }

    public void AddSelectedDrug()
    {
        if (SelectedDrugToAdd == null) return;

        var selectedDrug = new SelectedDrug(
            SelectedDrugs.Count + 1,
            SelectedDrugToAdd,
            NewDrugQuantity,
            NewDrugFrequency,
            NewDrugDuration,
            NewDrugReason,
            NewDrugNotes
        );

        SelectedDrugs.Add(selectedDrug);
        
        // Reset form and clear selection
        SelectedDrugToAdd = null;
        NewDrugQuantity = 1.00m;
        NewDrugFrequency = string.Empty;
        NewDrugDuration = string.Empty;
        NewDrugReason = string.Empty;
        NewDrugNotes = string.Empty;
        
        // Trigger UI update
        OnPropertyChanged(nameof(SelectedDrugs));
    }

    public void RemoveSelectedDrug(SelectedDrug drug)
    {
        SelectedDrugs.Remove(drug);
        
        // Re-number remaining drugs
        for (int i = 0; i < SelectedDrugs.Count; i++)
        {
            var updatedDrug = SelectedDrugs[i] with { Id = i + 1 };
            SelectedDrugs[i] = updatedDrug;
        }
        
        OnPropertyChanged(nameof(SelectedDrugs)); // ← Trigger UI update
    }

    public void ClearSelection()
    {
        SelectedDrugs.Clear();
        SelectedDrugToAdd = null;
        OnPropertyChanged(nameof(SelectedDrugs));
    }

    public string SubmitPrescription()
    {
        if (!SelectedDrugs.Any())
        {
            return "Error: No drugs selected";
        }

        // Generate prescription number
        var prescriptionNumber = $"RX-{DateTime.Now:yyyyMMdd}-{Prescriptions.Count + 1:000}";
        
        // Create new prescription
        var prescription = new Prescription(
            Prescriptions.Count + 1,
            prescriptionNumber,
            DateTime.Now,
            "Standard",
            new List<SelectedDrug>(SelectedDrugs),
            "Submitted"
        );
        
        Prescriptions.Add(prescription);
        
        // Clear current selection
        SelectedDrugs.Clear();
        SelectedDrugToAdd = null;
        
        OnPropertyChanged(nameof(SelectedDrugs));
        OnPropertyChanged(nameof(Prescriptions));
        
        Console.WriteLine($"✓ Prescription {prescriptionNumber} submitted successfully");
        return prescriptionNumber;
    }

    public void UpdatePrescription(int id, DateTime date, string type, string status, List<SelectedDrug> drugs)
    {
        var prescription = Prescriptions.FirstOrDefault(p => p.Id == id);
        if (prescription == null) return;

        var updatedPrescription = new Prescription(
            prescription.Id,
            prescription.PrescriptionNumber,
            date,
            type,
            drugs,
            status
        );

        var index = Prescriptions.IndexOf(prescription);
        Prescriptions[index] = updatedPrescription;
        
        OnPropertyChanged(nameof(Prescriptions));
    }

    public void RemovePrescription(int id)
    {
        var prescription = Prescriptions.FirstOrDefault(p => p.Id == id);
        if (prescription == null) return;

        Prescriptions.Remove(prescription);
        
        // Re-number remaining prescriptions
        for (int i = 0; i < Prescriptions.Count; i++)
        {
            var updatedPrescription = Prescriptions[i] with { Id = i + 1 };
            Prescriptions[i] = updatedPrescription;
        }
        
        OnPropertyChanged(nameof(Prescriptions));
    }

    public void CreatePrescription(string prescriptionNumber, DateTime date, string type, string status, List<SelectedDrug> drugs)
    {
        if (!drugs.Any())
        {
            return;
        }

        // Create new prescription
        var prescription = new Prescription(
            Prescriptions.Count + 1,
            prescriptionNumber,
            date,
            type,
            new List<SelectedDrug>(drugs),
            status
        );

        Prescriptions.Add(prescription);
        
        OnPropertyChanged(nameof(Prescriptions));
        
        Console.WriteLine($"✓ Prescription {prescriptionNumber} created successfully");
    }
}
