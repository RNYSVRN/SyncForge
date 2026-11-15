using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SyncForge.Models;
using Windows.UI;

namespace SyncForge.Services;

/// <summary>Wendet System/Hell/Dunkel auf Fensterinhalt, Titelleiste und Fensterschaltflächen an.</summary>
public sealed class ThemeService
{
    private Window? _window;
    private readonly Windows.UI.ViewManagement.AccessibilitySettings _accessibility = new();
    // Das Ereignis HighContrastChanged gibt es in Desktop-Apps nicht; ColorValuesChanged meldet auch Kontrastwechsel.
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

    public void Initialize(Window window, AppTheme theme)
    {
        _window = window;
        if (window.Content is FrameworkElement root)
        {
            // Folgt auch Systemwechseln (Hell/Dunkel), wenn "Windows-Standard" gewählt ist.
            root.ActualThemeChanged += (s, _) => ApplyCaptionButtonColors(s.ActualTheme);
        }
        // Kontrastdesign ein/aus: Fensterschaltflächen wieder Windows überlassen bzw. eigene Farben setzen.
        _uiSettings.ColorValuesChanged += (_, _) => window.DispatcherQueue.TryEnqueue(() =>
        {
            if (window.Content is FrameworkElement r) ApplyCaptionButtonColors(r.ActualTheme);
        });
        Apply(theme);
    }

    public void Apply(AppTheme theme)
    {
        App.OnThemeChanged(theme);
        if (_window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            ApplyCaptionButtonColors(root.ActualTheme);
        }

        if (_window?.AppWindow is { } appWindow)
        {
            appWindow.TitleBar.PreferredTheme = theme switch
            {
                AppTheme.Light => TitleBarTheme.Light,
                AppTheme.Dark => TitleBarTheme.Dark,
                _ => TitleBarTheme.UseDefaultAppMode
            };
        }
    }

    /// <summary>
    /// Farben für Minimieren/Maximieren (Schließen färbt Windows beim Hover selbst rot),
    /// entsprechend den Windows-11-Fluent-Werten (Subtle Fill / Text Fill).
    /// </summary>
    private void ApplyCaptionButtonColors(ElementTheme actualTheme)
    {
        if (_window?.AppWindow is not { } appWindow) return;
        var tb = appWindow.TitleBar;

        // Kontrastdesign: Windows bestimmt die Farben (eigene Werte wären dort schlecht oder gar nicht sichtbar).
        if (_accessibility.HighContrast)
        {
            tb.ButtonBackgroundColor = tb.ButtonInactiveBackgroundColor = null;
            tb.ButtonForegroundColor = tb.ButtonInactiveForegroundColor = null;
            tb.ButtonHoverBackgroundColor = tb.ButtonHoverForegroundColor = null;
            tb.ButtonPressedBackgroundColor = tb.ButtonPressedForegroundColor = null;
            return;
        }

        var dark = actualTheme == ElementTheme.Dark;
        var transparent = Color.FromArgb(0, 0, 0, 0);

        tb.ButtonBackgroundColor = transparent;
        tb.ButtonInactiveBackgroundColor = transparent;

        if (dark)
        {
            tb.ButtonForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            tb.ButtonHoverBackgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
            tb.ButtonHoverForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            tb.ButtonPressedBackgroundColor = Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF);
            tb.ButtonPressedForegroundColor = Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF);
            tb.ButtonInactiveForegroundColor = Color.FromArgb(0x5D, 0xFF, 0xFF, 0xFF);
        }
        else
        {
            tb.ButtonForegroundColor = Color.FromArgb(0xE4, 0x00, 0x00, 0x00);
            tb.ButtonHoverBackgroundColor = Color.FromArgb(0x09, 0x00, 0x00, 0x00);
            tb.ButtonHoverForegroundColor = Color.FromArgb(0xE4, 0x00, 0x00, 0x00);
            tb.ButtonPressedBackgroundColor = Color.FromArgb(0x06, 0x00, 0x00, 0x00);
            tb.ButtonPressedForegroundColor = Color.FromArgb(0x9E, 0x00, 0x00, 0x00);
            tb.ButtonInactiveForegroundColor = Color.FromArgb(0x5C, 0x00, 0x00, 0x00);
        }
    }
}
