using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.Views;

// Completion in the expression editor, as in the XML view: the first letter of a name offers the fields, functions and
// types, a dot the members of what is before it, a quote after `==` a drop-down field's values, and Ctrl+Space (or Cmd+I
// on macOS) whatever fits the caret. What is offered comes from ExpressionCompletion.
public partial class ExpressionEditorDialog
{
    private CompletionWindow? _completionWindow;

    private void InstallCompletion()
    {
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.AddHandler(KeyDownEvent, OnCompletionKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow is not null) return;
        var offset = Editor.CaretOffset;
        if (e.Text is "." or "\"" || ExpressionCompletion.StartsName(Editor.Text, offset))
            ShowCompletion(automatic: true);
    }

    private void OnCompletionKeyDown(object? sender, KeyEventArgs e)
    {
        // macOS takes Ctrl+Space for switching input sources unless the person has changed it, so Cmd+I opens the list
        // there too, as it does in the XML view.
        if ((e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
            || (e.Key == Key.I && e.KeyModifiers == KeyModifiers.Meta && OperatingSystem.IsMacOS()))
        {
            ShowCompletion(automatic: false);
            e.Handled = true;
        }
    }

    private void ShowCompletion(bool automatic)
    {
        if (_viewModel is null) return;
        _completionWindow?.Close();

        var offset = Editor.CaretOffset;
        var suggestions = ExpressionCompletion.At(Editor.Text, offset, _viewModel.CompletionSources);
        if (suggestions.Items.Count == 0) return;

        var window = new CompletionWindow(Editor.TextArea)
        {
            StartOffset = suggestions.ReplaceFrom,
            CloseWhenCaretAtBeginning = automatic,
        };
        foreach (var item in suggestions.Items)
            window.CompletionList.CompletionData.Add(new ExpressionCompletionData(item));
        if (offset > suggestions.ReplaceFrom)
            window.CompletionList.SelectItem(Editor.Document.GetText(suggestions.ReplaceFrom, offset - suggestions.ReplaceFrom));
        else
            window.CompletionList.SelectedItem = window.CompletionList.CompletionData[0];
        window.Closed += (_, _) => _completionWindow = null;
        _completionWindow = window;
        window.Show();
    }

    /// <summary>A suggestion in AvaloniaEdit's completion list.</summary>
    private sealed class ExpressionCompletionData(ExpressionSuggestion suggestion) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => suggestion.Label;

        public object Content => suggestion.Label;

        public object? Description => suggestion.Detail.Length > 0 ? suggestion.Detail : null;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // The segment is anchored: replacing an empty one moves its start past the inserted text, so the start is
            // read before.
            var start = completionSegment.Offset;
            textArea.Document.Replace(completionSegment, suggestion.InsertText);
            textArea.Caret.Offset = start + suggestion.InsertText.Length;
        }
    }
}
