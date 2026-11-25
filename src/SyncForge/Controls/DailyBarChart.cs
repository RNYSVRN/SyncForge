using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using SyncForge.Core.History;
using SyncForge.Helpers;

namespace SyncForge.Controls;

/// <summary>
/// Säulendiagramm "Datenmenge pro Tag" (eine Datenreihe, daher ohne Legende).
/// Dünne Säulen in Akzentfarbe, 2 px Abstand, abgerundetes Datenende; unaufdringliche Linien
/// für Grundlinie und Maximum; Tooltip je Tag mit großer Trefferfläche (ganze Spalte).
/// </summary>
public sealed class DailyBarChart : UserControl
{
    private const double ChartHeight = 140;

    private readonly Grid _bars = new() { Height = ChartHeight };
    private readonly TextBlock _maxLabel = new();
    private readonly Grid _xLabels = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly List<(Border Bar, long Value)> _barItems = [];
    private long _max;

    public DailyBarChart()
    {
        _maxLabel.Style = (Style)Application.Current.Resources["ChartLabelStyle"];

        var plot = new Grid();
        var topRule = new Rectangle { VerticalAlignment = VerticalAlignment.Top, Style = (Style)Application.Current.Resources["ChartRuleStyle"] };
        var baseline = new Rectangle { VerticalAlignment = VerticalAlignment.Bottom, Style = (Style)Application.Current.Resources["ChartRuleStyle"] };
        plot.Children.Add(topRule);
        plot.Children.Add(baseline);
        plot.Children.Add(_bars);

        var root = new StackPanel { Spacing = 4 };
        root.Children.Add(_maxLabel);
        root.Children.Add(plot);
        root.Children.Add(_xLabels);
        Content = root;

        _bars.SizeChanged += (_, _) => LayoutBars();
    }

    public void SetData(IReadOnlyList<DailyStat> days)
    {
        _bars.Children.Clear();
        _bars.ColumnDefinitions.Clear();
        _barItems.Clear();
        _xLabels.Children.Clear();

        _max = days.Count == 0 ? 0 : days.Max(d => d.Bytes);
        _maxLabel.Text = _max > 0 ? Format.Bytes(_max) : Format.Text("History_ChartEmpty");
        var barStyle = (Style)Application.Current.Resources["ChartBarStyle"];
        var columnStyle = (Style)Application.Current.Resources["ChartColumnStyle"];
        var hoverStyle = (Style)Application.Current.Resources["ChartColumnHoverStyle"];

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            _bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Trefferfläche = ganze Spalte (größer als die Säule), Hover hebt sie dezent hervor.
            var column = new Border { Style = columnStyle };
            var bar = new Border { Style = barStyle, Height = 0 };
            column.Child = bar;
            ToolTipService.SetToolTip(column, Format.Text("History_ChartTooltip",
                day.Date.ToString("D", Format.Culture), Format.Bytes(day.Bytes), Format.Number(day.Runs)));
            AutomationName(column, day);
            column.PointerEntered += (_, _) => column.Style = hoverStyle;
            column.PointerExited += (_, _) => column.Style = columnStyle;

            Grid.SetColumn(column, i);
            _bars.Children.Add(column);
            _barItems.Add((bar, day.Bytes));
        }

        if (days.Count > 0)
        {
            AddXLabel(days[0].Date, HorizontalAlignment.Left);
            if (days.Count > 2) AddXLabel(days[days.Count / 2].Date, HorizontalAlignment.Center);
            AddXLabel(days[^1].Date, HorizontalAlignment.Right);
        }
        LayoutBars();
    }

    private void LayoutBars()
    {
        foreach (var (bar, value) in _barItems)
        {
            // Werte > 0 bleiben sichtbar (mind. 2 px), 0 zeigt keine Säule.
            bar.Height = _max <= 0 || value <= 0 ? 0 : Math.Max(2, value * ChartHeight / _max);
        }
    }

    private void AddXLabel(DateOnly date, HorizontalAlignment alignment)
    {
        var label = new TextBlock
        {
            Text = date.ToString("M", Format.Culture),
            HorizontalAlignment = alignment,
            Style = (Style)Application.Current.Resources["ChartLabelStyle"]
        };
        _xLabels.Children.Add(label);
    }

    private static void AutomationName(Border column, DailyStat day) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(column,
            $"{day.Date.ToString("d", Format.Culture)}: {Format.Bytes(day.Bytes)}, {day.Runs}");
}
