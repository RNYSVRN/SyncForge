using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SyncForge.Models;
using Windows.UI;

namespace SyncForge.Services;

/// <summary>Wendet System/Hell/Dunkel auf Fensterinhalt, Titelleiste und Fensterschaltflächen an.</summary>
public sealed class ThemeService
{
    private Window? _window;

    public void Initialize(Window window, AppTheme theme)
    {
        _window = window;
        if (window.Content is FrameworkElement root)
        {
            // Folgt auch Systemwechseln (Hell/Dunkel), wenn "Windows-Standard" gewählt ist.
            root.ActualThemeChanged += (s, _) => ApplyCaptionButtonColors(s.ActualTheme);
        }
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
