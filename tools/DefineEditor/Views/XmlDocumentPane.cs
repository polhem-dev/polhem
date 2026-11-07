using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The XML of a define document, with highlighting, line numbers and find (Cmd/Ctrl+F). Opening it puts the caret on
/// the element of the node the tree had selected. What is typed is read into the document after a pause in typing;
/// XML that cannot be read is underlined and listed below the text, and the document keeps its last valid state.
/// </summary>
public sealed partial class XmlDocumentPane : UserControl
{
    // Long enough not to read the XML at every key, short enough that the tree is current when the person looks up.
    private static readonly TimeSpan s_applyDelay = TimeSpan.FromMilliseconds(500);

    private readonly TextEditor _editor;
    private readonly IssueUnderline _underline = new();
    private readonly StackPanel _issueList;
    private readonly Border _issuePanel;
    private readonly DispatcherTimer _applyTimer;
    private ObjectTreeDocumentViewModelBase? _document;

    // Set while the pane itself puts text in the editor, which is not an edit to read back.
    private bool _isShowingText;

    public XmlDocumentPane()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            FontFamily = new FontFamily("ui-monospace, Menlo, Consolas, monospace"),
            FontSize = 13,
            Padding = new Thickness(6, 4),
            Background = Brushes.Transparent,
            WordWrap = false,
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
        _editor.TextArea.TextView.BackgroundRenderers.Add(_underline);
        _editor.TextChanged += OnEditorTextChanged;
        InstallCompletion();

        _applyTimer = new DispatcherTimer { Interval = s_applyDelay };
        // A half-typed attribute (`Kind=""` while its values are still being offered) is not read back until the
        // completion list closes, so it is not flagged as a problem mid-way.
        _applyTimer.Tick += (_, _) =>
        {
            if (_completionWindow is null) FlushEdit();
        };

        // Rebuilt for every reading of the XML. A ListBox given a new list of equal records left the rows of the old one
        // drawn over the new ones.
        _issueList = new StackPanel { Spacing = 2 };
        _issuePanel = new Border
        {
            Classes = { "surface-sunken" },
            Padding = new Thickness(8, 4),
            IsVisible = false,
            Child = new ScrollViewer { MaxHeight = 120, Content = _issueList },
        };
        DockPanel.SetDock(_issuePanel, Dock.Bottom);
        Content = new DockPanel { Children = { _issuePanel, _editor } };

        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        AttachedToVisualTree += (_, _) => ApplyTheme();
        DetachedFromVisualTree += (_, _) => FlushEdit();
        // Leaving the text for the tree beside it reads what was typed first, so the tree edits the current object.
        _editor.TextArea.LostFocus += (_, _) =>
        {
            if (_completionWindow is null) FlushEdit();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        FlushEdit();
        if (_document is not null) _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = DataContext as ObjectTreeDocumentViewModelBase;
        if (_document is null) return;

        _document.PropertyChanged += OnDocumentPropertyChanged;
        ShowText(_document.XmlText);
        ShowIssues(_document.XmlIssues);
        if (_document.IsXmlView) RevealSelectedNode(focus: !_document.IsSplitView);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_document is null) return;
        switch (e.PropertyName)
        {
            case nameof(ObjectTreeDocumentViewModelBase.XmlText):
                ShowText(_document.XmlText);
                // An edit in the tree beside the XML wrote the text again; show where the selected node now is.
                if (_document.IsSplitView && !_editor.TextArea.IsKeyboardFocusWithin) RevealSelectedNode(focus: false);
                break;
            case nameof(ObjectTreeDocumentViewModelBase.XmlIssues):
                ShowIssues(_document.XmlIssues);
                break;
            // The XML alone takes the focus; beside the tree it leaves the focus where it is, on the tree most likely.
            case nameof(ObjectTreeDocumentViewModelBase.ViewMode) when _document.IsXmlView:
                RevealSelectedNode(focus: !_document.IsSplitView);
                break;
            // Beside the tree, the XML follows the node selected there, without taking the focus from the tree. While
            // the text has the focus the selection follows the text instead, so the caret is left where it is.
            case nameof(ObjectTreeDocumentViewModelBase.SelectedTreeNode)
                when _document.IsSplitView && !_editor.TextArea.IsKeyboardFocusWithin:
                RevealSelectedNode(focus: false);
                break;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isShowingText || _document is null) return;
        _document.OnXmlTextEdited(_editor.Text);
        RestartApplyTimer();
    }

    private void RestartApplyTimer()
    {
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private void FlushEdit()
    {
        _applyTimer.Stop();
        _document?.FlushXmlEdit();
    }

    private void ShowIssues(IReadOnlyList<XmlParseIssue> issues)
    {
        _underline.Issues = issues;
        _issueList.Children.Clear();
        foreach (var issue in issues)
            _issueList.Children.Add(IssueRow(issue));
        _issuePanel.IsVisible = issues.Count > 0;
        _editor.TextArea.TextView.InvalidateLayer(_underline.Layer);
    }

    // A problem as a row that puts the caret where it is.
    private Button IssueRow(XmlParseIssue issue)
    {
        var row = new Button
        {
            Classes = { "toolbar-icon" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = ObjectTreeDocumentViewModelBase.DescribeXmlIssuePosition(issue),
                        Classes = { "muted" },
                        MinWidth = 120,
                    },
                    new TextBlock
                    {
                        Text = ObjectTreeDocumentViewModelBase.DescribeXmlIssue(issue),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
        row.Click += (_, _) => GoTo(issue.Line, issue.Column, 0);
        return row;
    }

    // Text that did not come from typing (the XML generated again, or discarded edits) keeps the caret where it was,
    // as far as the new text allows.
    private void ShowText(string text)
    {
        if (_editor.Text == text) return;
        var caret = _editor.CaretOffset;
        _isShowingText = true;
        try
        {
            _editor.Text = text;
        }
        finally
        {
            _isShowingText = false;
        }
        _editor.CaretOffset = Math.Min(caret, _editor.Document.TextLength);
    }

    // The pane may have just become visible, so its size is only known after the next layout pass.
    private void RevealSelectedNode(bool focus) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_document?.LocateSelectedNodeInXml() is { } position)
                GoTo(position.Line, position.Column, position.Length, focus);
        }, DispatcherPriority.Loaded);

    // Selects length characters from a 1-based line and column, scrolls to them and, when asked, focuses the text.
    private void GoTo(int lineNumber, int column, int length, bool focus = true)
    {
        var document = _editor.Document;
        if (lineNumber < 1 || lineNumber > document.LineCount) return;

        var line = document.GetLineByNumber(lineNumber);
        var start = Math.Clamp(line.Offset + column - 1, line.Offset, line.EndOffset);
        _editor.CaretOffset = start;
        _editor.Select(start, Math.Min(length, line.EndOffset - start));
        _editor.ScrollTo(lineNumber, column);
        if (focus) _editor.TextArea.Focus();
    }

    private void ApplyTheme()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        XmlHighlighting.Apply(dark);
        _underline.Pen = new Pen(new SolidColorBrush(Color.Parse(dark ? "#FC725A" : "#F93920")), 1.2);
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

    /// <summary>A wavy underline under each problem of the XML, from its position to the end of the name there.</summary>
    private sealed class IssueUnderline : IBackgroundRenderer
    {
        public IReadOnlyList<XmlParseIssue> Issues { get; set; } = [];

        public IPen Pen { get; set; } = new Pen(Brushes.Red, 1.2);

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Issues.Count == 0 || textView.Document is not { } document || !textView.VisualLinesValid) return;

            foreach (var issue in Issues)
            {
                if (issue.Line < 1 || issue.Line > document.LineCount) continue;
                var line = document.GetLineByNumber(issue.Line);
                var start = Math.Clamp(line.Offset + issue.Column - 1, line.Offset, line.EndOffset);
                var end = start;
                while (end < line.EndOffset && IsNameChar(document.GetCharAt(end))) end++;
                if (end == start) end = Math.Min(start + 1, line.EndOffset);
                if (end == start) continue;

                var segment = new TextSegment { StartOffset = start, EndOffset = end };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                    drawingContext.DrawGeometry(null, Pen, Wave(rect.BottomLeft, rect.Right));
            }
        }

        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is ':' or '_' or '-' or '.';

        private static StreamGeometry Wave(Point from, double right)
        {
            const double step = 2.5;
            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            context.BeginFigure(new Point(from.X, from.Y - 1), isFilled: false);
            var up = true;
            for (var x = from.X + step; x <= right + step; x += step, up = !up)
                context.LineTo(new Point(Math.Min(x, right), from.Y - (up ? 2.5 : 0)));
            context.EndFigure(isClosed: false);
            return geometry;
        }
    }
}
