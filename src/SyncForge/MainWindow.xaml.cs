using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SyncForge.Services;
using SyncForge.Views;
using Windows.Graphics;

namespace SyncForge;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Pages = new()
    {
        ["Dashboard"] = typeof(DashboardPage),
        ["Profiles"] = typeof(ProfilesPage),
        ["History"] = typeof(HistoryPage),
        ["Settings"] = typeof(SettingsPage),
        ["Help"] = typeof(HelpPage),
        ["Support"] = typeof(SupportPage),
        ["Legal"] = typeof(LegalPage),
    };

    /// <summary>Unterseiten, bei denen ein Hauptpunkt der Navigation markiert bleibt.</summary>
    private static readonly Dictionary<Type, string> SubPages = new()
    {
        [typeof(ProfileEditorPage)] = "Profiles",
        [typeof(SyncRunPage)] = "Profiles",
        [typeof(RunDetailPage)] = "History",
        [typeof(VersionsPage)] = "Profiles",
    };

    private readonly SettingsService _settings;
    private bool _syncingSelection;

    public MainWindow()
    {
        InitializeComponent();

        _settings = App.Services.GetRequiredService<SettingsService>();

        Title = "SyncForge";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SyncForge.ico"));

        // Buttons in der eigenen Titelleiste müssen Klicks erhalten (Rest der Leiste = Ziehfläche).
        AppTitleBar.SizeChanged += (_, _) => UpdateTitleBarPassthrough();
        AppTitleBar.Loaded += (_, _) => UpdateTitleBarPassthrough();
        Activated += MainWindow_Activated;

        RestoreWindowSize();
        AppWindow.Closing += AppWindow_Closing;

        App.Services.GetRequiredService<ThemeService>().Initialize(this, _settings.Current.Theme);

        NavView.SelectedItem = NavView.MenuItems[0];

        // Dampf über der Kaffeetasse ("Unterstützen"), Farbe folgt dem Symbol (Hover, Auswahl, Design).
        Controls.CoffeeSteam.Attach(SupportIcon, SupportIcon, () => SupportIcon.Foreground);
    }

    public void NavigateTo(string tag)
    {
        if (tag == "Settings")
        {
            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        NavView.SelectedItem = FindNavItem(tag);
    }

    /// <summary>Öffnet eine Unterseite (z. B. Profil-Editor) mit Parameter.</summary>
    public void Navigate(Type pageType, object? parameter = null) =>
        ContentFrame.Navigate(pageType, parameter, new DrillInNavigationTransitionInfo());

    /// <summary>Zurück zur vorherigen Seite (oder zur angegebenen Hauptseite, falls es keine gibt).</summary>
    public void GoBack(string fallbackTag = "Profiles")
    {
        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
        else ContentFrame.Navigate(Pages[fallbackTag]);
    }

    /// <summary>Navigationseintrag mit diesem Tag – oben oder unten (Hilfe, Lizenzen).</summary>
    private NavigationViewItem? FindNavItem(string? tag) =>
        NavView.MenuItems.Concat(NavView.FooterMenuItems).OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingSelection) return;
        var tag = args.IsSettingsSelected ? "Settings" : (args.SelectedItem as NavigationViewItem)?.Tag as string;
        if (tag is null || !Pages.TryGetValue(tag, out var pageType) || ContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        ContentFrame.Navigate(pageType, null, args.RecommendedNavigationTransitionInfo ?? new EntranceNavigationTransitionInfo());
    }

    /// <summary>Klick auf den schon markierten Punkt (z. B. aus dem Profil-Editor zurück zur Liste).</summary>
    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.IsSettingsInvoked ? "Settings" : (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;
        if (tag is not null && Pages.TryGetValue(tag, out var pageType) && ContentFrame.CurrentSourcePageType != pageType
            && SubPages.TryGetValue(ContentFrame.CurrentSourcePageType, out var parent) && parent == tag)
        {
            ContentFrame.Navigate(pageType, null, args.RecommendedNavigationTransitionInfo);
        }
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        TitleBarBackButton.IsEnabled = ContentFrame.CanGoBack;

        // Auswahl in der Navigation an die angezeigte Seite angleichen (z. B. nach "Zurück"),
        // ohne dabei erneut zu navigieren.
        var tag = Pages.FirstOrDefault(p => p.Value == e.SourcePageType).Key
                  ?? (SubPages.TryGetValue(e.SourcePageType, out var parent) ? parent : null);
        object? item = tag == "Settings"
            ? NavView.SettingsItem
            : FindNavItem(tag);
        if (item is not null && !ReferenceEquals(NavView.SelectedItem, item))
        {
            _syncingSelection = true;
            NavView.SelectedItem = item;
            _syncingSelection = false;
        }
    }

    private void TitleBarBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
    }

    private void TitleBarPaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        var minimal = args.DisplayMode == NavigationViewDisplayMode.Minimal;
        TitleBarPaneToggleButton.Visibility = minimal ? Visibility.Visible : Visibility.Collapsed;
        if (!minimal)
        {
            // Breites Fenster: Navigation immer ausgeklappt (kein Umschalten wie in den Windows-Einstellungen).
            NavView.IsPaneOpen = true;
        }
        DispatcherQueue.TryEnqueue(UpdateTitleBarPassthrough);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Wie die Windows-11-Einstellungen: Titel bleibt unverändert, nur die Fensterschaltflächen
        // werden bei inaktivem Fenster abgeblendet (macht Windows über ButtonInactive*-Farben).
    }

    private void UpdateTitleBarPassthrough()
    {
        if (AppTitleBar.XamlRoot is null) return;
        var scale = AppTitleBar.XamlRoot.RasterizationScale;
        var rects = new List<RectInt32>();
        foreach (var element in new FrameworkElement[] { TitleBarBackButton, TitleBarPaneToggleButton })
        {
            if (element.Visibility != Visibility.Visible || element.ActualWidth == 0) continue;
            var bounds = element.TransformToVisual(null).TransformBounds(
                new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            rects.Add(new RectInt32(
                (int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale),
                (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale)));
        }
        Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(AppWindow.Id)
            .SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough, rects.ToArray());
    }

    /// <summary>
    /// Schließen: In den Infobereich (Zeitpläne und Überwachung laufen weiter) oder – wenn so eingestellt –
    /// SyncForge beenden.
    /// </summary>
    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        SaveWindowSize();
        if (App.IsExiting) return;
        args.Cancel = true;
        if (_settings.Current.MinimizeToTray) sender.Hide();
        else App.ExitApp();
    }

    private void RestoreWindowSize()
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var width = (int)(Math.Max(_settings.Current.WindowWidth, 500) * scale);
        var height = (int)(Math.Max(_settings.Current.WindowHeight, 400) * scale);

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width, height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(500 * scale);
            presenter.PreferredMinimumHeight = (int)(400 * scale);
        }
    }

    private void SaveWindowSize()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
            _settings.Current.WindowWidth = (int)(AppWindow.Size.Width / scale);
            _settings.Current.WindowHeight = (int)(AppWindow.Size.Height / scale);
            _settings.Save();
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
