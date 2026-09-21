using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public class AnnotateImageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Models
    public record AnnotationImage(string Name, string Category, string ThumbnailUrl, string ImageUrl);
    
    public class Annotation
    {
        public string Type { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Color { get; set; } = "#000000";
        public int Size { get; set; } = 16;
        public string Content { get; set; } = "";
        public bool IsSelected { get; set; } = false;
    }

    // Image Library
    private List<AnnotationImage> _images = new()
    {
        new AnnotationImage("Face Anatomy - Front", "Facial", "https://placehold.co/150x150/e3f2fd/1976d2?text=Face+Front", "https://placehold.co/800x600/e3f2fd/1976d2?text=Face+Anatomy+Front"),
        new AnnotationImage("Face Anatomy - Side", "Facial", "https://placehold.co/150x150/f3e5f5/7b1fa2?text=Face+Side", "https://placehold.co/800x600/f3e5f5/7b1fa2?text=Face+Anatomy+Side"),
        new AnnotationImage("Skin Layers", "Dermatology", "https://placehold.co/150x150/e8f5e9/388e3c?text=Skin+Layers", "https://placehold.co/800x600/e8f5e9/388e3c?text=Skin+Layers+Diagram"),
        new AnnotationImage("Body Zones", "Body", "https://placehold.co/150x150/fff3e0/f57c00?text=Body+Zones", "https://placehold.co/800x600/fff3e0/f57c00?text=Body+Treatment+Zones"),
        new AnnotationImage("Hand Anatomy", "Extremities", "https://placehold.co/150x150/fce4ec/c2185b?text=Hand", "https://placehold.co/800x600/fce4ec/c2185b?text=Hand+Anatomy"),
        new AnnotationImage("Foot Anatomy", "Extremities", "https://placehold.co/150x150/e0f2f1/00796b?text=Foot", "https://placehold.co/800x600/e0f2f1/00796b?text=Foot+Anatomy"),
        new AnnotationImage("Eye Detail", "Facial", "https://placehold.co/150x150/e1f5fe/0277bd?text=Eye", "https://placehold.co/800x600/e1f5fe/0277bd?text=Eye+Detail+Anatomy"),
        new AnnotationImage("Lip Detail", "Facial", "https://placehold.co/150x150/ffeef8/ad1457?text=Lips", "https://placehold.co/800x600/ffeef8/ad1457?text=Lip+Detail+Anatomy"),
    };
    
    public List<AnnotationImage> Images => _images;

    // State Properties

    private string? _activeTool;
    private string _selectedColor = "#000000";
    private int _lineThickness = 3;

    public string SearchTerm { get; set; } = string.Empty;
    public AnnotationImage? SelectedImage { get; set; }
    
    // Before/After image properties
    private AnnotationImage? _beforeImage;
    private AnnotationImage? _afterImage;
    
    public AnnotationImage? BeforeImage
    {
        get => _beforeImage;
        set
        {
            if (_beforeImage != value)
            {
                _beforeImage = value;
                OnPropertyChanged();
            }
        }
    }
    
    public AnnotationImage? AfterImage
    {
        get => _afterImage;
        set
        {
            if (_afterImage != value)
            {
                _afterImage = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string? ActiveTool
    {
        get => _activeTool;
        set
        {
            if (_activeTool != value)
            {
                _activeTool = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int LineThickness
    {
        get => _lineThickness;
        set
        {
            if (_lineThickness != value)
            {
                _lineThickness = value;
                OnPropertyChanged();
            }
        }
    }
    

    
    private string _canvasBackground = "image";
    private int _fontSize = 20; // Increased default font size for better readability
    private bool _isBold = false;
    private bool _isItalic = false;
    private bool _isUnderline = false;
    
    public string CanvasBackground
    {
        get => _canvasBackground;
        set
        {
            if (_canvasBackground != value)
            {
                _canvasBackground = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (_fontSize != value)
            {
                _fontSize = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsBold
    {
        get => _isBold;
        set
        {
            if (_isBold != value)
            {
                _isBold = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsItalic
    {
        get => _isItalic;
        set
        {
            if (_isItalic != value)
            {
                _isItalic = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsUnderline
    {
        get => _isUnderline;
        set
        {
            if (_isUnderline != value)
            {
                _isUnderline = value;
                OnPropertyChanged();
            }
        }
    }
    public string? StatusMessage { get; set; }
    public string StatusMessageType { get; set; } = "success";
    public List<Annotation> Annotations { get; set; } = new();
    
    // Error handling properties
    private string? _errorMessage;
    private bool _hasError;
    
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError != value)
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }
    }
    
    public void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        StatusMessage = message;
        StatusMessageType = "error";
    }
    
    public void ClearError()
    {
        ErrorMessage = null;
        HasError = false;
    }

    // Computed Properties
    public IEnumerable<AnnotationImage> FilteredImages =>
        Images.Where(img => string.IsNullOrWhiteSpace(SearchTerm) ||
                           img.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                           img.Category.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));

    // Methods
    public void SelectImage(AnnotationImage image)
    {
        SelectedImage = image;
        ResetCanvas();
        StatusMessage = null;
    }

    public void SelectTool(string toolId)
    {
        // Set the active tool (don't toggle, just set it)
        ActiveTool = toolId;
    }

    public async Task SelectToolAsync(string toolId, Func<Task<bool>> canvasReadyCheck, Func<string, Task> jsUpdateCallback)
    {
        // Validate canvas is ready before tool activation
        var isReady = await canvasReadyCheck();
        
        if (!isReady)
        {
            // Canvas not ready, set tool but log warning
            Console.WriteLine($"Warning: Canvas not ready when selecting tool '{toolId}'");
            ActiveTool = toolId;
            return;
        }
        
        // Set the active tool
        ActiveTool = toolId;
        
        // Immediately propagate to JavaScript
        await jsUpdateCallback(toolId);
    }

    public void SetBackground(string background)
    {
        CanvasBackground = background;
    }

    public void UndoAction()
    {
        if (Annotations.Any())
        {
            Annotations.RemoveAt(Annotations.Count - 1);
        }
    }

    public void RedoAction()
    {
        // Redo functionality would require a separate history stack
        StatusMessage = "Redo functionality";
        StatusMessageType = "info";
    }

    public void ResetCanvas()
    {
        Annotations.Clear();
        ActiveTool = null;
        // Don't change CanvasBackground - preserve current mode (image, before-after, etc.)
        // CanvasBackground = "image"; // REMOVED - preserve current background mode
        StatusMessage = null;
        // Don't clear BeforeImage or AfterImage - preserve selected images
    }

    public void ToggleBold() => IsBold = !IsBold;
    public void ToggleItalic() => IsItalic = !IsItalic;
    public void ToggleUnderline() => IsUnderline = !IsUnderline;

    public void InsertAnnotation()
    {
        if (SelectedImage is null) return;

        StatusMessage = $"'{SelectedImage.Name}' has been inserted into case notes successfully!";
        StatusMessageType = "success";

        // Here you would implement the actual insertion logic
        // For example, saving the annotated image and adding it to case notes
    }

    public void CancelAnnotation()
    {
        ResetCanvas();
        SelectedImage = null;
        StatusMessage = "Annotation cancelled";
        StatusMessageType = "info";
    }
    
    public void AddImage(string name, string category, string thumbnailUrl, string imageUrl)
    {
        var newImage = new AnnotationImage(name, category, thumbnailUrl, imageUrl);
        _images.Insert(0, newImage); // Add to beginning of list
        OnPropertyChanged(nameof(Images));
        OnPropertyChanged(nameof(FilteredImages));
    }
    
    // Before/After image methods
    public void SetBeforeImage(AnnotationImage? image)
    {
        BeforeImage = image;
    }
    
    public void SetAfterImage(AnnotationImage? image)
    {
        AfterImage = image;
    }
    
    public void ClearBeforeAfterImages()
    {
        BeforeImage = null;
        AfterImage = null;
    }
}
