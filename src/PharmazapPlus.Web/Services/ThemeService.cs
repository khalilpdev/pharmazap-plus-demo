using Microsoft.FluentUI.AspNetCore.Components;

namespace PharmazapPlus.Web.Services;

public class ThemeService
{
    public DesignThemeModes CurrentMode { get; private set; } = DesignThemeModes.Light;

    /// <summary>
    /// Current accent color as an HTML hex string (e.g. "#0078d4"), or <c>null</c> to use the default.
    /// Kept in sync with <see cref="Microsoft.FluentUI.AspNetCore.Components.FluentDesignTheme.CustomColor"/>
    /// via two-way binding in MainLayout.
    /// </summary>
    public string? CurrentAccentColor { get; private set; }

    public event Action? OnThemeChanged;

    public void SetTheme(DesignThemeModes mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        OnThemeChanged?.Invoke();
    }

    public void SetThemeFromString(string? theme)
    {
        SetTheme(theme == "Dark" ? DesignThemeModes.Dark : DesignThemeModes.Light);
    }

    /// <summary>Sets the accent color.  Pass <c>null</c> to restore the default.</summary>
    public void SetAccentColor(string? color)
    {
        if (CurrentAccentColor == color) return;
        CurrentAccentColor = color;
        OnThemeChanged?.Invoke();
    }
}
