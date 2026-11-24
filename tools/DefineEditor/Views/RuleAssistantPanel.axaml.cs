using System.ComponentModel;
using Avalonia.Controls;
using Polhem.Chat.Avalonia;
using Polhem.DefineEditor.ViewModels;
using Polhem.Markdown;

namespace Polhem.DefineEditor.Views;

/// <summary>The AI assistant that writes a rule from plain words and explains one, with the samples the engine computed.</summary>
public partial class RuleAssistantPanel : UserControl
{
    private RuleAssistantViewModel? _assistant;

    public RuleAssistantPanel()
    {
        InitializeComponent();
        ChatLanguage.Follow(Chat, "RuleAssistant_RequestWatermark");

        // IMPORTANT: the explanation is the model's text. An image in it would be fetched as soon as it shows, and a
        // prompt injection can put data in the image's address, so the viewer loads none. The conversation's
        // `ChatView` does the same by default.
        var options = new MarkdownOptions();
        options.AllowedImageSchemes.Clear();
        ExplanationViewer.Options = options;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_assistant is not null) _assistant.PropertyChanged -= OnAssistantPropertyChanged;
        _assistant = DataContext as RuleAssistantViewModel;
        if (_assistant is not null) _assistant.PropertyChanged += OnAssistantPropertyChanged;
        ShareHeight();
    }

    private void OnAssistantPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RuleAssistantViewModel.HasResult)) ShareHeight();
    }

    // The result row takes no height until there is a result; then the conversation and the result share the panel.
    private void ShareHeight()
        => Rows.RowDefinitions[2].Height = _assistant?.HasResult == true ? new GridLength(1.2, GridUnitType.Star) : GridLength.Auto;

    private void OnPromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        if (DataContext is RuleAssistantViewModel assistant)
            _ = assistant.SendAsync(e.Text);
    }

    private void OnStopRequested(object? sender, EventArgs e) => (DataContext as RuleAssistantViewModel)?.Stop();
}
