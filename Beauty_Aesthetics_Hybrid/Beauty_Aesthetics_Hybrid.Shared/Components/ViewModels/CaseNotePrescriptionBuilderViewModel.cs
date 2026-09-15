using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClosedXML.Excel;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class CaseNotePrescriptionBuilderViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Models
    public record Drug(int Id, string Name, string DrugCode, string Category, string Indication, string Restrictions, string Dosage);
    public record SelectedDrug(int Id, Drug Drug, decimal Quantity, string Frequency, string Duration, string Reason, string Notes);
    public record Prescription(int Id, string PrescriptionNumber, DateTime Date, string Type, List<SelectedDrug> Drugs, string Status);

    // Category Management - Shared across instances
    public static List<string> SharedCategories = new()
    {
        "Facial Treatment",
        "Injectables",
        "Laser Treatment",
        "Skincare Product",
        "Body Contouring",
        "Hair Removal"
    };

    // Treatment Library - Shared with CaseNotePrescriptionViewModel
    public static List<Drug> SharedDrugs = new()
    {
        new Drug(1, "Hydrating Facial Treatment", "FACIAL-HYD-001", "Facial Treatment", 
            "Deep hydrating facial for dry or dehydrated skin to improve texture and radiance.", 
            "Avoid strong exfoliants 24 hours before treatment. Not recommended for broken or infected skin.", 
            "Single in-clinic session; repeat every 4 weeks as needed."),
        new Drug(2, "Chemical Peel 20% AHA", "PEEL-AHA20-001", "Facial Treatment", 
            "Superficial chemical peel to improve skin tone, fine lines, and mild acne scars.", 
            "Not suitable for very sensitive skin, active infection, or recent sunburn.", 
            "In-clinic application every 4–6 weeks, according to practitioner assessment."),
        new Drug(3, "Botulinum Toxin Injection 50U", "INJ-BTX-050", "Injectables", 
            "Temporary reduction of dynamic wrinkles (e.g., frown lines, crow's feet).", 
            "Not recommended during pregnancy, breastfeeding, or for patients with neuromuscular disorders.", 
            "Administered in-clinic every 3–4 months as indicated."),
        new Drug(4, "Hyaluronic Acid Filler 1ml", "FILL-HA-1ML", "Injectables", 
            "Dermal filler for volume restoration and contouring of facial features.", 
            "Use with caution in patients with bleeding disorders or active skin infection at injection site.", 
            "In-clinic procedure; maintenance every 9–18 months depending on area and product."),
        new Drug(5, "Laser Skin Rejuvenation (Full Face)", "LASER-REJ-FF", "Laser Treatment", 
            "Non-ablative laser treatment to improve overall skin tone, texture, and mild pigmentation.", 
            "Avoid on tanned skin and in patients with photosensitivity. Strict sun protection required post-treatment.", 
            "In-clinic session every 4 weeks; usually 3–6 sessions per course."),
        new Drug(6, "Laser Hair Removal (Underarms)", "LHR-UA-001", "Hair Removal", 
            "Permanent hair reduction of axillary region.", 
            "Not recommended on recently waxed or epilated areas; avoid sun exposure before and after treatment.", 
            "In-clinic session every 4–6 weeks; typically 6–8 sessions per area."),
        new Drug(7, "Body Contouring RF Treatment (Abdomen)", "BODY-RF-ABD", "Body Contouring", 
            "Radiofrequency body contouring to improve skin laxity and mild localized fat appearance.", 
            "Not suitable for pregnancy, pacemaker, or metal implants in the treatment area.", 
            "In-clinic session every 2–4 weeks; 4–8 sessions per course as assessed."),
        new Drug(8, "Medical-Grade Retinol Serum 0.5%", "SKIN-RET-005", "Skincare Product", 
            "Topical serum to improve fine lines, texture, and pigmentation.", 
            "Introduce gradually; avoid in pregnancy and with other strong exfoliants. Daily SPF required.", 
            "Apply a pea-sized amount at night, 2–3 times per week, increasing as tolerated."),
        new Drug(9, "Brightening Vitamin C Serum 15%", "SKIN-VC15-001", "Skincare Product", 
            "Antioxidant serum to support brighter, more even skin tone.", 
            "May cause mild tingling on sensitive skin. Discontinue if significant irritation occurs.", 
            "Apply once daily in the morning before moisturizer and sunscreen."),
        new Drug(10, "Oil-Control Acne Cleanser", "SKIN-CL-ACNE", "Skincare Product", 
            "Foaming cleanser for oily or acne-prone skin to reduce excess sebum and impurities.", 
            "Avoid eye area and overuse in very dry or sensitive skin.", 
            "Use twice daily as part of home skincare routine."),
        new Drug(11, "Post-Procedure Recovery Cream", "SKIN-REC-CRM", "Skincare Product", 
            "Soothing cream to support barrier recovery after aesthetic procedures (e.g., peels, lasers).", 
            "Formulated for post-procedure use; check ingredients for specific allergies.", 
            "Apply 2–3 times daily or as directed until skin comfort and hydration are restored."),
    };

    // Cached Excel template bytes to avoid regenerating on every download
    private static byte[]? _cachedExcelTemplateBytes;

    public List<Drug> Drugs { get; private set; } = new(SharedDrugs);
    public List<Prescription> Prescriptions { get; set; } = new();

    // State Properties
    private string _searchTerm = string.Empty;
    private string _selectedCategory = string.Empty;
    private Drug? _selectedDrug = null;
    private bool _isEditingDrug = false;

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm != value)
            {
                _searchTerm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredDrugs));
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
                OnPropertyChanged(nameof(FilteredDrugs));
            }
        }
    }

    public Drug? SelectedDrugForEdit
    {
        get => _selectedDrug;
        set
        {
            if (_selectedDrug != value)
            {
                _selectedDrug = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsEditingDrug
    {
        get => _isEditingDrug;
        set
        {
            if (_isEditingDrug != value)
            {
                _isEditingDrug = value;
                OnPropertyChanged();
            }
        }
    }

    // Form Fields for Drug CRUD
    private string _drugName = string.Empty;
    private string _drugCode = string.Empty;
    private string _drugCategory = string.Empty;
    private string _drugIndication = string.Empty;
    private string _drugRestriction = string.Empty;
    private string _drugDosage = string.Empty;

    public string DrugName
    {
        get => _drugName;
        set
        {
            if (_drugName != value)
            {
                _drugName = value;
                OnPropertyChanged();
            }
        }
    }

    public string DrugCode
    {
        get => _drugCode;
        set
        {
            if (_drugCode != value)
            {
                _drugCode = value;
                OnPropertyChanged();
            }
        }
    }

    public string DrugCategory
    {
        get => _drugCategory;
        set
        {
            if (_drugCategory != value)
            {
                _drugCategory = value;
                OnPropertyChanged();
            }
        }
    }

    public string DrugIndication
    {
        get => _drugIndication;
        set
        {
            if (_drugIndication != value)
            {
                _drugIndication = value;
                OnPropertyChanged();
            }
        }
    }

    public string DrugRestriction
    {
        get => _drugRestriction;
        set
        {
            if (_drugRestriction != value)
            {
                _drugRestriction = value;
                OnPropertyChanged();
            }
        }
    }

    public string DrugDosage
    {
        get => _drugDosage;
        set
        {
            if (_drugDosage != value)
            {
                _drugDosage = value;
                OnPropertyChanged();
            }
        }
    }

    // Computed Properties
    public IEnumerable<Drug> FilteredDrugs =>
        Drugs.Where(d => 
            (string.IsNullOrWhiteSpace(SearchTerm) ||
             d.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
             d.DrugCode.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrEmpty(d.Category) && d.Category.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(SelectedCategory) ||
             (!string.IsNullOrEmpty(d.Category) && d.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))));

    public List<string> AvailableCategories =>
        SharedCategories.OrderBy(c => c).ToList();
    
    // Category Management Methods
    public void AddCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new InvalidOperationException("Category name cannot be empty");
        }

        var trimmedCategory = category.Trim();
        
        if (SharedCategories.Any(c => c.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Category '{trimmedCategory}' already exists");
        }

        SharedCategories.Add(trimmedCategory);
        OnPropertyChanged(nameof(AvailableCategories));
    }

    public void UpdateCategory(string oldCategory, string newCategory)
    {
        if (string.IsNullOrWhiteSpace(newCategory))
        {
            throw new InvalidOperationException("Category name cannot be empty");
        }

        var trimmedNewCategory = newCategory.Trim();
        var trimmedOldCategory = oldCategory.Trim();

        if (trimmedOldCategory.Equals(trimmedNewCategory, StringComparison.OrdinalIgnoreCase))
        {
            return; // No change needed
        }

        if (SharedCategories.Any(c => c != trimmedOldCategory && c.Equals(trimmedNewCategory, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Category '{trimmedNewCategory}' already exists");
        }

        var index = SharedCategories.FindIndex(c => c.Equals(trimmedOldCategory, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            SharedCategories[index] = trimmedNewCategory;
            
            // Update all drugs that use this category
            for (int i = 0; i < SharedDrugs.Count; i++)
            {
                if (SharedDrugs[i].Category.Equals(trimmedOldCategory, StringComparison.OrdinalIgnoreCase))
                {
                    var updatedDrug = SharedDrugs[i] with { Category = trimmedNewCategory };
                    SharedDrugs[i] = updatedDrug;
                }
            }
            
            // Sync local Drugs list
            Drugs = new List<Drug>(SharedDrugs);
        }

        OnPropertyChanged(nameof(AvailableCategories));
        OnPropertyChanged(nameof(Drugs));
        OnPropertyChanged(nameof(FilteredDrugs));
    }

    public void DeleteCategory(string category)
    {
        var trimmedCategory = category.Trim();
        
        // Check if any drugs use this category
        if (SharedDrugs.Any(d => d.Category.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Cannot delete category '{trimmedCategory}' because it is used by one or more drugs. Please remove or reassign the category from all drugs first.");
        }

        SharedCategories.RemoveAll(c => c.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(AvailableCategories));
    }

    // Methods
    public void StartNewDrug()
    {
        _isEditingDrug = false;
        _selectedDrug = null;
        _drugName = string.Empty;
        _drugCode = string.Empty;
        _drugCategory = string.Empty;
        _drugIndication = string.Empty;
        _drugRestriction = string.Empty;
        _drugDosage = string.Empty;
        OnPropertyChanged(nameof(SelectedDrugForEdit));
        OnPropertyChanged(nameof(IsEditingDrug));
    }

    public void SelectDrugForEdit(Drug drug)
    {
        _selectedDrug = drug;
        _isEditingDrug = true;
        _drugName = drug.Name;
        _drugCode = drug.DrugCode;
        _drugCategory = drug.Category;
        _drugIndication = drug.Indication;
        _drugRestriction = drug.Restrictions;
        _drugDosage = drug.Dosage;
        OnPropertyChanged(nameof(SelectedDrugForEdit));
        OnPropertyChanged(nameof(IsEditingDrug));
    }

    public void CreateDrug()
    {
        if (string.IsNullOrWhiteSpace(_drugName) || string.IsNullOrWhiteSpace(_drugCode))
        {
            throw new InvalidOperationException("Drug Name and Drug Code are required");
        }

        // Check for duplicate drug code
        if (SharedDrugs.Any(d => d.DrugCode.Equals(_drugCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A drug with code '{_drugCode}' already exists. Please change the drug code.");
        }

        // Check for duplicate drug name
        if (SharedDrugs.Any(d => d.Name.Equals(_drugName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A drug with name '{_drugName}' already exists. Please change the drug name.");
        }

        // Auto-add new category if it doesn't exist
        if (!string.IsNullOrWhiteSpace(_drugCategory))
        {
            var trimmedCategory = _drugCategory.Trim();
            if (!SharedCategories.Any(c => c.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                SharedCategories.Add(trimmedCategory);
            }
        }

        // Use SharedDrugs to ensure unique ID across all instances
        var newId = SharedDrugs.Any() ? SharedDrugs.Max(d => d.Id) + 1 : 1;
        var newDrug = new Drug(
            newId,
            _drugName,
            _drugCode,
            _drugCategory,
            _drugIndication,
            _drugRestriction,
            _drugDosage
        );

        // Add to shared list first
        SharedDrugs.Add(newDrug);
        
        // Sync local Drugs list with SharedDrugs to keep them in sync
        Drugs = new List<Drug>(SharedDrugs);
        
        OnPropertyChanged(nameof(Drugs));
        OnPropertyChanged(nameof(FilteredDrugs));
        OnPropertyChanged(nameof(AvailableCategories));
        
        StartNewDrug();
    }

    public void UpdateDrug()
    {
        if (_selectedDrug == null)
        {
            throw new InvalidOperationException("No drug selected for editing");
        }

        if (string.IsNullOrWhiteSpace(_drugName) || string.IsNullOrWhiteSpace(_drugCode))
        {
            throw new InvalidOperationException("Drug Name and Drug Code are required");
        }

        // Check for duplicate drug code (excluding current drug)
        if (SharedDrugs.Any(d => d.Id != _selectedDrug.Id && d.DrugCode.Equals(_drugCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A drug with code '{_drugCode}' already exists. Please change the drug code.");
        }

        // Check for duplicate drug name (excluding current drug)
        if (SharedDrugs.Any(d => d.Id != _selectedDrug.Id && d.Name.Equals(_drugName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A drug with name '{_drugName}' already exists. Please change the drug name.");
        }

        // Auto-add new category if it doesn't exist
        if (!string.IsNullOrWhiteSpace(_drugCategory))
        {
            var trimmedCategory = _drugCategory.Trim();
            if (!SharedCategories.Any(c => c.Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                SharedCategories.Add(trimmedCategory);
            }
        }

        var updatedDrug = new Drug(
            _selectedDrug.Id,
            _drugName,
            _drugCode,
            _drugCategory,
            _drugIndication,
            _drugRestriction,
            _drugDosage
        );

        // Update shared list first
        var sharedIndex = SharedDrugs.FindIndex(d => d.Id == _selectedDrug.Id);
        if (sharedIndex >= 0)
        {
            SharedDrugs[sharedIndex] = updatedDrug;
            
            // Sync local Drugs list with SharedDrugs to keep them in sync
            Drugs = new List<Drug>(SharedDrugs);
        }

        OnPropertyChanged(nameof(Drugs));
        OnPropertyChanged(nameof(FilteredDrugs));
        OnPropertyChanged(nameof(AvailableCategories));
        
        StartNewDrug();
    }

    public void DeleteDrug(int drugId)
    {
        var drug = SharedDrugs.FirstOrDefault(d => d.Id == drugId);
        if (drug == null) return;

        // Remove from shared list first
        SharedDrugs.Remove(drug);
        
        // Sync local Drugs list with SharedDrugs to keep them in sync
        Drugs = new List<Drug>(SharedDrugs);
        
        OnPropertyChanged(nameof(Drugs));
        OnPropertyChanged(nameof(FilteredDrugs));
        OnPropertyChanged(nameof(AvailableCategories));
        
        if (_selectedDrug?.Id == drugId)
        {
            StartNewDrug();
        }
    }

    // Prescription Management Methods
    public void CreatePrescription(string prescriptionNumber, DateTime date, string type, string status, List<SelectedDrug> drugs)
    {
        if (!drugs.Any())
        {
            throw new InvalidOperationException("At least one drug is required");
        }

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

        var index = Prescriptions.FindIndex(p => p.Id == id);
        if (index >= 0)
        {
            Prescriptions[index] = updatedPrescription;
            OnPropertyChanged(nameof(Prescriptions));
        }
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

    // Excel Export/Import Methods
    public byte[] GenerateExcelTemplate()
    {
        // Return cached template if already generated
        if (_cachedExcelTemplateBytes != null && _cachedExcelTemplateBytes.Length > 0)
        {
            return _cachedExcelTemplateBytes;
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Prescription Template");

        // Set column headers
        worksheet.Cell(1, 1).Value = "Drug Name";
        worksheet.Cell(1, 2).Value = "Drug Code";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "Indication";
        worksheet.Cell(1, 5).Value = "Restriction";
        worksheet.Cell(1, 6).Value = "Dosage";

        // Style the header row
        var headerRange = worksheet.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 129, 189);
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Add instructions in a separate row
        worksheet.Cell(2, 1).Value = "Fill in the prescription details starting from row 4. Drug Name and Drug Code are required. The first 3 rows are fixed and cannot be edited.";
        var instructionRange = worksheet.Range(2, 1, 2, 6);
        instructionRange.Merge();
        instructionRange.Style.Font.Italic = true;
        instructionRange.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 241);
        instructionRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        // Add sample data row (optional)
        worksheet.Cell(3, 1).Value = "Example Facial Treatment";
        worksheet.Cell(3, 2).Value = "FACIAL-EX-001";
        worksheet.Cell(3, 3).Value = "Facial Treatment";
        worksheet.Cell(3, 4).Value = "Hydrating facial treatment to improve skin texture and radiance";
        worksheet.Cell(3, 5).Value = "Avoid strong exfoliants 24 hours before and after treatment";
        worksheet.Cell(3, 6).Value = "Single in-clinic session; repeat every 4 weeks as needed";

        // Style sample row
        var sampleRange = worksheet.Range(3, 1, 3, 6);
        sampleRange.Style.Font.Italic = true;
        sampleRange.Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242);

        // Auto-fit columns (for the first three rows)
        worksheet.Columns(1, 6).AdjustToContents(1, 3);

        // Set minimum column widths
        for (int col = 1; col <= 6; col++)
        {
            var column = worksheet.Column(col);
            if (column.Width < 15)
            {
                column.Width = 15;
            }
        }

        // Add borders to header + instruction + sample rows
        var borderRange = worksheet.Range(1, 1, 3, 6);
        borderRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        borderRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        borderRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        borderRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Protect header, instruction, and example rows (1-3) and allow editing from row 4 downward
        // Lock rows 1-3
        var lockedRange = worksheet.Range(1, 1, 3, 6);
        lockedRange.Style.Protection.Locked = true;

        // Unlock data entry area (rows 4-1000 for columns 1-6)
        var dataRange = worksheet.Range(4, 1, 1000, 6);
        dataRange.Style.Protection.Locked = false;

        // Enable worksheet protection so locking takes effect
        worksheet.Protect();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        _cachedExcelTemplateBytes = stream.ToArray();
        return _cachedExcelTemplateBytes;
    }

    public List<Drug> ImportDrugsFromExcel(Stream excelStream)
    {
        var importedDrugs = new List<Drug>();
        var errors = new List<string>();

        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            throw new InvalidOperationException("The Excel file does not contain any worksheets.");
        }

        // Validate that the uploaded file matches the expected prescription template
        // Optional: check sheet name first
        if (!string.Equals(worksheet.Name, "Prescription Template", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid Excel template. Please use the 'Prescription Template' file downloaded from this system.");
        }

        // Validate header row (row 1)
        string[] expectedHeaders = new[]
        {
            "Drug Name",
            "Drug Code",
            "Category",
            "Indication",
            "Restriction",
            "Dosage"
        };

        for (int col = 1; col <= expectedHeaders.Length; col++)
        {
            var headerValue = worksheet.Cell(1, col).GetString().Trim();
            if (!string.Equals(headerValue, expectedHeaders[col - 1], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid Excel template format. Please use the latest 'Prescription Template' exported from this system.");
            }
        }

        // Find the starting row (skip header and instruction rows)
        int startRow = 4; // Start from row 4 (after header, instruction, and sample)
        int endRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (endRow < startRow)
        {
            throw new InvalidOperationException("The Excel file does not contain any data rows.");
        }

        // Optimize: Pre-build HashSets for O(1) lookup instead of O(n) with Any()
        var existingDrugCodes = new HashSet<string>(SharedDrugs.Select(d => d.DrugCode), StringComparer.OrdinalIgnoreCase);
        var existingDrugNames = new HashSet<string>(SharedDrugs.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
        var importedDrugCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedDrugNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingCategories = new HashSet<string>(SharedCategories, StringComparer.OrdinalIgnoreCase);
        var newCategories = new List<string>();

        // Optimize: Calculate base ID once
        var baseId = SharedDrugs.Count > 0 ? SharedDrugs.Max(d => d.Id) + 1 : 1;

        for (int row = startRow; row <= endRow; row++)
        {
            var drugName = worksheet.Cell(row, 1).GetString().Trim();
            var drugCode = worksheet.Cell(row, 2).GetString().Trim();

            // Skip completely empty rows quickly
            if (string.IsNullOrWhiteSpace(drugName) && string.IsNullOrWhiteSpace(drugCode))
                continue;

            var category = worksheet.Cell(row, 3).GetString().Trim();
            var indication = worksheet.Cell(row, 4).GetString().Trim();
            var restriction = worksheet.Cell(row, 5).GetString().Trim();
            var dosage = worksheet.Cell(row, 6).GetString().Trim();

            // Normalize null/empty to empty string for optional fields
            category ??= string.Empty;
            indication ??= string.Empty;
            restriction ??= string.Empty;
            dosage ??= string.Empty;

            // Collect all validation errors for this row before continuing
            var rowErrors = new List<string>();
            bool hasErrors = false;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(drugName))
            {
                rowErrors.Add($"Row {row}: Drug Name is required.");
                hasErrors = true;
            }

            if (string.IsNullOrWhiteSpace(drugCode))
            {
                rowErrors.Add($"Row {row}: Drug Code is required.");
                hasErrors = true;
            }

            // Only check for duplicates if both name and code are provided
            if (!string.IsNullOrWhiteSpace(drugName) && !string.IsNullOrWhiteSpace(drugCode))
            {
                // Check for duplicates in existing system (check both at the same time)
                bool codeExists = existingDrugCodes.Contains(drugCode);
                bool nameExists = existingDrugNames.Contains(drugName);

                if (codeExists)
                {
                    rowErrors.Add($"Row {row}: Drug code '{drugCode}' already exists in the system.");
                    hasErrors = true;
                }

                if (nameExists)
                {
                    rowErrors.Add($"Row {row}: Drug name '{drugName}' already exists in the system.");
                    hasErrors = true;
                }

                // Check for duplicates within the import file (check both at the same time)
                bool codeDuplicateInImport = importedDrugCodes.Contains(drugCode);
                bool nameDuplicateInImport = importedDrugNames.Contains(drugName);

                if (codeDuplicateInImport)
                {
                    rowErrors.Add($"Row {row}: Duplicate drug code '{drugCode}' found in the import file.");
                    hasErrors = true;
                }

                if (nameDuplicateInImport)
                {
                    rowErrors.Add($"Row {row}: Duplicate drug name '{drugName}' found in the import file.");
                    hasErrors = true;
                }
            }

            // If any errors found for this row, add them all and skip to next row
            if (hasErrors)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            // Auto-add new category if it doesn't exist
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!existingCategories.Contains(category))
                {
                    existingCategories.Add(category);
                    newCategories.Add(category);
                }
            }

            // Optimize: Use pre-calculated base ID + count
            var newId = baseId + importedDrugs.Count;

            // At this point, drugName and drugCode are guaranteed to be non-null and non-empty
            // (we've validated them above and skipped if they were empty)
            var newDrug = new Drug(
                newId,
                drugName!,
                drugCode!,
                category,
                indication,
                restriction,
                dosage
            );

            importedDrugs.Add(newDrug);
            importedDrugCodes.Add(drugCode!);
            importedDrugNames.Add(drugName!);
        }

        if (errors.Any())
        {
            var errorMessage = "The following errors occurred during import:\n" + string.Join("\n", errors);
            throw new InvalidOperationException(errorMessage);
        }

        if (!importedDrugs.Any())
        {
            throw new InvalidOperationException("No valid drug data found in the Excel file.");
        }

        // Optimize: Batch add new categories
        if (newCategories.Count > 0)
        {
            SharedCategories.AddRange(newCategories);
        }

        // Optimize: Batch add imported drugs to the shared list
        SharedDrugs.AddRange(importedDrugs);

        // Optimize: Sync local Drugs list (single allocation)
        Drugs = new List<Drug>(SharedDrugs);

        // Optimize: Batch notify property changes
        OnPropertyChanged(nameof(Drugs));
        OnPropertyChanged(nameof(FilteredDrugs));
        OnPropertyChanged(nameof(AvailableCategories));

        return importedDrugs;
    }
}
