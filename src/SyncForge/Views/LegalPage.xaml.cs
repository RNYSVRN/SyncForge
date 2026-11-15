using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.Views;

/// <summary>Anzeige einer Drittanbieter-Komponente.</summary>
public sealed class ComponentItem(ThirdPartyComponent component)
{
    /// <summary>Lange Lizenztexte (z. B. Windows App SDK mit Drittanbieter-Hinweisen) nur anreißen; vollständig per Button.</summary>
    private const int PreviewLength = 12000;

    public ThirdPartyComponent Component { get; } = component;
    public string Name => Component.Name;
    public string License => Component.License;
    public string Url => Component.Url;
    public Uri? Uri => Uri.TryCreate(Component.Url, UriKind.Absolute, out var u) ? u : null;
    public string Detail => string.Join(" · ", new[] { Component.Version is null ? null : Format.Text("Legal_Version", Component.Version), Component.Authors }
        .Where(s => !string.IsNullOrEmpty(s)));
    public string PackagesText => Component.Packages.Count == 0 ? string.Empty : Format.Text("Legal_Packages", string.Join(", ", Component.Packages));
    public string OpenText => Localization.Get("Legal_OpenFullText");

    public string Preview
    {
        get
        {
            var path = ContentService.LicenseFilePath(Component);
            if (!File.Exists(path)) return Localization.Get("Legal_LicenseMissing");
            var text = File.ReadAllText(path);
            return text.Length <= PreviewLength ? text : text[..PreviewLength] + Environment.NewLine + Environment.NewLine + Localization.Get("Legal_Truncated");
        }
    }

    public override string ToString() => $"{Name}, {License}";
}

/// <summary>Lizenzen &amp; Rechtliches: Nutzungsbedingungen, Datenschutz und Drittanbieter-Lizenzen.</summary>
public sealed partial class LegalPage : Page
{
    private bool _thirdPartyLoaded;

    public LegalPage()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
        VersionText.Text = Format.Text("Legal_Version", version);
        EulaView.SetMarkdown(ContentService.LoadLegal("eula"));
        PrivacyView.SetMarkdown(ContentService.LoadLegal("privacy"));
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string tab && Tabs.Items.FirstOrDefault(i => (string)i.Tag == tab) is { } item) Tabs.SelectedItem = item;
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var tab = sender.SelectedItem?.Tag as string;
        AppPanel.Visibility = tab == "App" ? Visibility.Visible : Visibility.Collapsed;
        PrivacyView.Visibility = tab == "Privacy" ? Visibility.Visible : Visibility.Collapsed;
        ThirdPartyPanel.Visibility = tab == "ThirdParty" ? Visibility.Visible : Visibility.Collapsed;
        if (tab == "ThirdParty" && !_thirdPartyLoaded)
        {
            ComponentList.ItemsSource = ContentService.LoadThirdPartyComponents().Select(c => new ComponentItem(c)).ToList();
            _thirdPartyLoaded = true;
        }
        Scroller.ChangeView(null, 0, null, disableAnimation: true);
    }

    private void OpenLicense_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ComponentItem item && File.Exists(ContentService.LicenseFilePath(item.Component)))
        {
            Process.Start(new ProcessStartInfo(ContentService.LicenseFilePath(item.Component)) { UseShellExecute = true });
        }
    }
}
