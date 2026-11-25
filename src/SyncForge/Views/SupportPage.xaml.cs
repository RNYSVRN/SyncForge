using Microsoft.UI.Xaml.Controls;
using SyncForge.Controls;

namespace SyncForge.Views;

/// <summary>"SyncForge unterstützen": freiwillige Unterstützung über Ko-fi, dampfende Kaffeetasse.</summary>
public sealed partial class SupportPage : Page
{
    public SupportPage()
    {
        InitializeComponent();
        // Gleicher Dampf wie beim Navigationssymbol – skaliert über die Viewbox mit.
        CoffeeSteam.Attach(CupHost, CupPath, () => CupPath.Fill);
    }
}
