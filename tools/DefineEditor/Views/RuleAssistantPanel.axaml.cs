using Avalonia.Controls;
using Polhem.Chat;
using Polhem.Chat.Avalonia;
using Polhem.DefineEditor.ViewModels;
using Polhem.Markdown;

namespace Polhem.DefineEditor.Views;

/// <summary>The AI assistant that writes a rule from plain words and explains one, with the samples the engine computed.</summary>
public partial class RuleAssistantPanel : UserControl
{
    public RuleAssistantPanel()
    {
        InitializeComponent();
        ChatLanguage.Follow(Chat, "RuleAssistant_RequestWatermark");
    }

    /// <summary>The options of the explanation viewer in a <see cref="RuleProposalEntry"/> card, which load no images.</summary>
    /// <remarks>
    /// IMPORTANT: the explanation is the model's text. An image in it would be fetched as soon as it shows, and a prompt
    /// injection can put data in the image's address, so the viewer loads none. The conversation's `ChatView` does the
    /// same by default. Every card shares this instance, so nothing may change it after it is created.
    /// </remarks>
    public static MarkdownOptions ExplanationOptions { get; } = CreateExplanationOptions();

    private static MarkdownOptions CreateExplanationOptions()
    {
        var options = new MarkdownOptions();
        options.AllowedImageSchemes.Clear();
        return options;
    }

    private void OnPromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        if (DataContext is RuleAssistantViewModel assistant)
            _ = assistant.SendAsync(e.Text);
    }

    private void OnStopRequested(object? sender, EventArgs e) => (DataContext as RuleAssistantViewModel)?.Stop();
}
