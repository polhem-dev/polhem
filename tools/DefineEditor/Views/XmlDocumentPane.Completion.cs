using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.Views;

// Completion in the XML view, as in a XAML editor: `<` offers the elements the definition allows there, a space in a
// start tag its attributes, the quotes of an enum or boolean attribute its values, Ctrl+Space whatever fits the caret,
// and `>` closing a start tag puts its end tag after the caret. What is offered comes from XmlCompletion.
public sealed partial class XmlDocumentPane
{
    private CompletionWindow? _completionWindow;

    private void InstallCompletion()
    {
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.AddHandler(KeyDownEvent, OnCompletionKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        switch (e.Text)
        {
            case "<" or " " or "\"" or "'" or "/":
                ShowCompletion(automatic: true);
                break;
            case ">":
                CloseStartTag();
                break;
        }
    }

    private void OnCompletionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            ShowCompletion(automatic: false);
            e.Handled = true;
        }
    }

    // An automatic list opens only when the caret is where something starts; Ctrl+Space also opens in the middle of a
    // name, filtered by what was typed of it.
    private void ShowCompletion(bool automatic)
    {
        if (_document is null) return;
        _completionWindow?.Close();

        var offset = _editor.CaretOffset;
        var suggestions = XmlCompletion.At(_editor.Text, offset, _document.XmlRootType, DescribeAttribute, L("XmlCompletion_DefaultValue"));
        if (suggestions.Items.Count == 0 || (automatic && suggestions.ReplaceFrom != offset)) return;

        var window = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = suggestions.ReplaceFrom,
            CloseWhenCaretAtBeginning = automatic,
        };
        foreach (var item in suggestions.Items)
            window.CompletionList.CompletionData.Add(new XmlCompletionData(item, this));
        // Enter takes the selected item, and with nothing typed yet nothing would be selected.
        if (offset > suggestions.ReplaceFrom)
            window.CompletionList.SelectItem(_editor.Document.GetText(suggestions.ReplaceFrom, offset - suggestions.ReplaceFrom));
        else
            window.CompletionList.SelectedItem = window.CompletionList.CompletionData[0];
        window.Closed += (_, _) =>
        {
            _completionWindow = null;
            RestartApplyTimer();
        };
        _completionWindow = window;
        window.Show();
    }

    private void CloseStartTag()
    {
        var offset = _editor.CaretOffset;
        if (XmlCompletion.EndTagAfterClose(_editor.Text, offset) is not { } endTag) return;
        _editor.Document.Insert(offset, endTag);
        _editor.CaretOffset = offset;
    }

    private static string DescribeAttribute(DefineXmlAttributeInfo attribute)
    {
        var parts = new List<string> { attribute.TypeName };
        if (attribute.IsRequired) parts.Add(L("XmlCompletion_Required"));
        if (!string.IsNullOrEmpty(attribute.DefaultValue)) parts.Add(L("XmlCompletion_Default", attribute.DefaultValue));
        return string.Join(" · ", parts.Where(p => p.Length > 0));
    }

    private static string L(string key) => LocalizationService.Current[key];

    private static string L(string key, string arg) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, LocalizationService.Current[key], arg);

    /// <summary>A suggestion in AvaloniaEdit's completion list.</summary>
    private sealed class XmlCompletionData(XmlSuggestion suggestion, XmlDocumentPane pane) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => suggestion.Label;

        public object Content => suggestion.Label;

        public object? Description => suggestion.Detail.Length > 0 ? suggestion.Detail : null;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var document = textArea.Document;
            var insert = suggestion.InsertText;
            var caretBack = suggestion.CaretOffset;

            // A value typed after an opening quote with no closing one yet gets its closing quote.
            if (suggestion.Kind == XmlSuggestionKind.Value)
            {
                var end = completionSegment.EndOffset;
                var next = end < document.TextLength ? document.GetCharAt(end) : '\0';
                if (next is not ('"' or '\'')) insert += "\"";
                else caretBack = -1;
            }

            // The segment is anchored: replacing an empty one moves its start past the inserted text, so the start
            // is read before.
            var start = completionSegment.Offset;
            document.Replace(completionSegment, insert);
            textArea.Caret.Offset = start + insert.Length - caretBack;

            if (suggestion.OffersValues)
                Dispatcher.UIThread.Post(() => pane.ShowCompletion(automatic: true), DispatcherPriority.Background);
        }
    }
}
