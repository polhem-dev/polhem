using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The XML of a define document, read-only, with highlighting, line numbers and find (Cmd/Ctrl+F). Opening it puts
/// the caret on the element of the node the tree had selected.
/// </summary>
public sealed class XmlDocumentPane : UserControl
{
    private readonly TextEditor _editor;
    private ObjectTreeDocumentViewModelBase? _document;

    public XmlDocumentPane()
    {
        _editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = true,
            FontFamily = new FontFamily("ui-monospace, Menlo, Consolas, monospace"),
            FontSize = 13,
            Padding = new Thickness(6, 4),
            Background = Brushes.Transparent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            SyntaxHighlighting = XmlHighlighting.Definition,
        };
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Bind(TemplatedControl.ForegroundProperty,
            _editor.GetResourceObservable("SemiColorText0", value => value as IBrush ?? Brushes.Black));
        _editor.Bind(TextEditor.LineNumbersForegroundProperty,
            _editor.GetResourceObservable("SemiColorText2", value => value as IBrush ?? Brushes.Gray));
        Content = _editor;

        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        AttachedToVisualTree += (_, _) => ApplyTheme();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_document is not null) _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = DataContext as ObjectTreeDocumentViewModelBase;
        if (_document is null) return;

        _document.PropertyChanged += OnDocumentPropertyChanged;
        ShowText(_document.XmlText);
        if (_document.IsXmlView) RevealSelectedNode();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_document is null) return;
        switch (e.PropertyName)
        {
            case nameof(ObjectTreeDocumentViewModelBase.XmlText):
                ShowText(_document.XmlText);
                break;
            case nameof(ObjectTreeDocumentViewModelBase.IsXmlView) when _document.IsXmlView:
                RevealSelectedNode();
                break;
        }
    }

    // A change of the object (Undo while the XML is shown) keeps the caret where it was, as far as the new text allows.
    private void ShowText(string text)
    {
        if (_editor.Text == text) return;
        var caret = _editor.CaretOffset;
        _editor.Text = text;
        _editor.CaretOffset = Math.Min(caret, _editor.Document.TextLength);
    }

    // The pane has just become visible, so its size is only known after the next layout pass.
    private void RevealSelectedNode() =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_document?.LocateSelectedNodeInXml() is not { } position) return;
            var document = _editor.Document;
            if (position.Line > document.LineCount) return;

            var line = document.GetLineByNumber(position.Line);
            var start = Math.Min(line.Offset + position.Column - 1, line.EndOffset);
            var length = Math.Min(position.Length, line.EndOffset - start);
            _editor.CaretOffset = start;
            _editor.Select(start, length);
            _editor.ScrollTo(position.Line, position.Column);
            _editor.TextArea.Focus();
        }, DispatcherPriority.Loaded);

    private void ApplyTheme()
    {
        XmlHighlighting.Apply(ActualThemeVariant == ThemeVariant.Dark);
        _editor.TextArea.TextView.Redraw();
    }

    /// <summary>
    /// AvaloniaEdit's XML highlighting, recoloured for the workbench's light or dark theme. Its own colours are made
    /// for a white background, and the attribute values and comments turn unreadable on the dark one.
    /// </summary>
    private static class XmlHighlighting
    {
        public static IHighlightingDefinition? Definition { get; } = HighlightingManager.Instance.GetDefinition("XML");

        // The colours of VS Code's Light+ and Dark+ themes for XML.
        private static readonly Dictionary<string, (string Light, string Dark)> s_colors = new(StringComparer.Ordinal)
        {
            ["Comment"] = ("#008000", "#6A9955"),
            ["CData"] = ("#795E26", "#CE9178"),
            ["DocType"] = ("#800000", "#808080"),
            ["XmlDeclaration"] = ("#800000", "#808080"),
            ["XmlTag"] = ("#800000", "#569CD6"),
            ["AttributeName"] = ("#E50000", "#9CDCFE"),
            ["AttributeValue"] = ("#0000FF", "#CE9178"),
            ["Entity"] = ("#0000FF", "#569CD6"),
            ["BrokenEntity"] = ("#E50000", "#F44747"),
        };

        // The definition is shared by every editor, and every editor follows the one application theme, so recolouring
        // it in place is enough.
        public static void Apply(bool dark)
        {
            if (Definition is null) return;
            foreach (var color in Definition.NamedHighlightingColors)
            {
                if (!s_colors.TryGetValue(color.Name, out var pair)) continue;
                color.Foreground = new SimpleHighlightingBrush(Color.Parse(dark ? pair.Dark : pair.Light));
            }
        }
    }
}
