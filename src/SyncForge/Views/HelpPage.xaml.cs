using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.Views;

/// <summary>Hilfe: Themen links, Inhalt rechts, Volltextsuche über alle Themen.</summary>
public sealed partial class HelpPage : Page
{
    private readonly IReadOnlyList<HelpTopic> _topics = ContentService.LoadHelpTopics();

    public HelpPage()
    {
        InitializeComponent();
        ShowTopics(_topics);
    }

    /// <summary>Optional: Thema per Id öffnen (z. B. "03-modi").</summary>
    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string id && _topics.FirstOrDefault(t => t.Id.Contains(id, StringComparison.OrdinalIgnoreCase)) is { } topic)
            TopicList.SelectedItem = topic;
    }

    private void ShowTopics(IReadOnlyList<HelpTopic> topics)
    {
        var selected = TopicList.SelectedItem as HelpTopic;
        TopicList.ItemsSource = topics;
        NoResults.Visibility = topics.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TopicList.SelectedItem = selected is not null && topics.Contains(selected) ? selected : topics.FirstOrDefault();
        if (topics.Count == 0) TopicView.SetMarkdown(string.Empty);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var query = sender.Text.Trim();
        if (query.Length == 0)
        {
            ResultText.Visibility = Visibility.Collapsed;
            ShowTopics(_topics);
            return;
        }
        var matches = _topics.Where(t => t.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        ResultText.Text = Format.Text(matches.Count == 1 ? "Help_ResultOne" : "Help_Results", Format.Number(matches.Count));
        ResultText.Visibility = Visibility.Visible;
        ShowTopics(matches);
    }

    private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicList.SelectedItem is not HelpTopic topic) return;
        TopicView.SetMarkdown(topic.Markdown);
        ContentScroller.ChangeView(null, 0, null, disableAnimation: true);
    }
}
