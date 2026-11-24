using Avalonia.Controls;
using Polhem.Chat.Avalonia;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>The AI assistant that writes a rule from plain words and explains one, with the samples the engine computed.</summary>
public partial class RuleAssistantPanel : UserControl
{
    public RuleAssistantPanel()
    {
        InitializeComponent();
        ChatLanguage.Follow(Chat, "RuleAssistant_RequestWatermark");
    }

    private void OnPromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        if (DataContext is RuleAssistantViewModel assistant)
            _ = assistant.SendAsync(e.Text);
    }

    private void OnStopRequested(object? sender, EventArgs e) => (DataContext as RuleAssistantViewModel)?.Stop();
}
