using Microsoft.AspNetCore.Components;
using SenangRetails.Shared.Services;

namespace SenangRetails.Shared.Pages;

/// <summary>
/// Base class for all pages. Provides:
///  - LangSvc injected (use GetText() or LangSvc.GetText() for translated strings)
///  - CurrentLanguage cascading parameter (Blazor re-renders this page whenever
///    MainLayout broadcasts a language change via CascadingValue)
/// </summary>
public abstract class BasePage : ComponentBase
{
    [Inject]
    protected LanguageService LangSvc { get; set; } = default!;

    /// <summary>
    /// Receives the current language string from MainLayout's CascadingValue.
    /// When its value changes, Blazor automatically re-renders this component.
    /// </summary>
    [CascadingParameter(Name = "CurrentLanguage")]
    public string CurrentLanguage { get; set; } = "English";
}
