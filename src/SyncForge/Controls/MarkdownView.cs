using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace SyncForge.Controls;

/// <summary>
/// Stellt einen Markdown-Ausschnitt in Windows-11-Typografie dar. Unterstützt bewusst nur, was Hilfe und
/// Rechtstexte nutzen: # / ## / ### Überschriften, Absätze, "- " und "1. " Listen, "> " Hinweise (als InfoBar),
/// **fett** und `Code`. HTML-Kommentare werden ignoriert. Text ist markier- und kopierbar.
/// </summary>
public sealed partial class MarkdownView : UserControl
{
    private readonly StackPanel _panel = new() { Spacing = 8 };

    public MarkdownView()
    {
        Content = _panel;
        IsTabStop = false;
    }

    /// <summary>Erste Überschrift (# …) nicht anzeigen, wenn die Seite den Titel selbst zeigt.</summary>
    public bool HideTitle { get; set; }

    public void SetMarkdown(string markdown)
    {
        _panel.Children.Clear();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        var firstTitleSkipped = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            _panel.Children.Add(CreateRichText(string.Join(" ", paragraph)));
            paragraph.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("<!--", StringComparison.Ordinal)) { FlushParagraph(); continue; }
            if (trimmed.Length == 0) { FlushParagraph(); continue; }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushParagraph();
                if (HideTitle && !firstTitleSkipped) { firstTitleSkipped = true; continue; }
                _panel.Children.Add(Heading(trimmed[2..], "TitleTextBlockStyle", new Thickness(0, 0, 0, 4)));
            }
            else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph();
                _panel.Children.Add(Heading(trimmed[3..], "SubtitleTextBlockStyle", new Thickness(0, 16, 0, 0)));
            }
            else if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushParagraph();
                _panel.Children.Add(Heading(trimmed[4..], "BodyStrongTextBlockStyle", new Thickness(0, 8, 0, 0)));
            }
            else if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushParagraph();
                var note = new List<string> { trimmed[2..] };
                while (i + 1 < lines.Length && lines[i + 1].TrimStart().StartsWith("> ", StringComparison.Ordinal))
                    note.Add(lines[++i].TrimStart()[2..]);
                _panel.Children.Add(Note(string.Join(" ", note)));
            }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph();
                _panel.Children.Add(ListItem("•", trimmed[2..]));
            }
            else if (NumberedItem().Match(trimmed) is { Success: true } m)
            {
                FlushParagraph();
                _panel.Children.Add(ListItem(m.Groups[1].Value + ".", m.Groups[2].Value));
            }
            else
            {
                paragraph.Add(trimmed);
            }
        }
        FlushParagraph();
    }

    private static TextBlock Heading(string text, string style, Thickness margin) => new()
    {
        Text = StripInline(text),
        Style = (Style)Application.Current.Resources[style],
        TextWrapping = TextWrapping.Wrap,
        Margin = margin,
        IsTextSelectionEnabled = true
    };

    private static UIElement ListItem(string marker, string text)
    {
        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(4, 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bullet = new TextBlock { Text = marker, HorizontalAlignment = HorizontalAlignment.Right };
        var content = CreateRichText(text);
        Grid.SetColumn(content, 1);
        grid.Children.Add(bullet);
        grid.Children.Add(content);
        return grid;
    }

    private static UIElement Note(string text)
    {
        var plain = StripInline(text);
        var isDraft = plain.StartsWith("ENTWURF", StringComparison.OrdinalIgnoreCase) || plain.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase)
                      || plain.StartsWith("Wichtig", StringComparison.OrdinalIgnoreCase) || plain.StartsWith("Important", StringComparison.OrdinalIgnoreCase);
        return new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = isDraft ? InfoBarSeverity.Warning : InfoBarSeverity.Informational,
            Message = plain,
            Margin = new Thickness(0, 4, 0, 4)
        };
    }

    private static RichTextBlock CreateRichText(string text)
    {
        var block = new RichTextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, LineHeight = 22 };
        var paragraph = new Paragraph();
        foreach (Match token in InlineToken().Matches(text))
        {
            if (token.Groups["bold"].Success)
                paragraph.Inlines.Add(new Run { Text = token.Groups["bold"].Value, FontWeight = FontWeights.SemiBold });
            else if (token.Groups["code"].Success)
                paragraph.Inlines.Add(new Run { Text = token.Groups["code"].Value, FontFamily = new FontFamily("Cascadia Mono, Consolas") });
            else
                paragraph.Inlines.Add(new Run { Text = token.Value });
        }
        block.Blocks.Add(paragraph);
        return block;
    }

    /// <summary>Text ohne Markdown-Auszeichnung (für Überschriften, Hinweise und die Suche).</summary>
    public static string StripInline(string text) => text.Replace("**", string.Empty).Replace("`", string.Empty);

    [GeneratedRegex(@"\*\*(?<bold>.+?)\*\*|`(?<code>[^`]+)`|[^*`]+|[*`]")]
    private static partial Regex InlineToken();

    [GeneratedRegex(@"^(\d+)\.\s+(.*)$")]
    private static partial Regex NumberedItem();
}
