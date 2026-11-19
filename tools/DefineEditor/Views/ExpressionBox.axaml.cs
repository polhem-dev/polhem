using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// One expression being edited: a code editor with completion and a wavy line where parsing stopped, and the error or
/// the allowlist warning below it.
/// </summary>
public partial class ExpressionBox : UserControl
{
    private readonly ErrorUnderline _underline = new();
    private ExpressionFieldViewModel? _viewModel;
    // Set while the editor is given the view model's text, so the change is not written back.
    private bool _syncing;

    public ExpressionBox()
    {
        InitializeComponent();
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.TextArea.TextView.BackgroundRenderers.Add(_underline);
        InstallCompletion();
        Editor.TextChanged += (_, _) =>
        {
            if (_viewModel is not null && !_syncing) _viewModel.Expression = Editor.Text;
        };
        Editor.TextArea.GotFocus += (_, _) => EditorFocused?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the editor gets the keyboard focus, so a dialog knows where to insert a chosen name.</summary>
    public event EventHandler? EditorFocused;

    /// <summary>Puts <paramref name="text"/> at the caret, in place of any selection, and focuses the editor.</summary>
    public void Insert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var selection = Editor.TextArea.Selection;
        if (!selection.IsEmpty) selection.ReplaceSelectionWithText(string.Empty);
        Editor.Document.Insert(Editor.CaretOffset, text);
        Editor.Focus();
    }

    /// <summary>Focuses the editor with the caret at the end of the text.</summary>
    public void FocusEnd()
    {
        Editor.Focus();
        Editor.CaretOffset = Editor.Document.TextLength;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as ExpressionFieldViewModel;
        if (_viewModel is null) return;
        _syncing = true;
        Editor.Document = new TextDocument(_viewModel.Expression);
        _syncing = false;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShowError();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExpressionFieldViewModel.ErrorPosition) or nameof(ExpressionFieldViewModel.Error))
            ShowError();
        // The text can change from outside the editor, such as when the AI assistant fills in a rule.
        if (e.PropertyName == nameof(ExpressionFieldViewModel.Expression) && _viewModel is not null
            && !string.Equals(Editor.Text, _viewModel.Expression, StringComparison.Ordinal))
        {
            _syncing = true;
            Editor.Document.Text = _viewModel.Expression;
            _syncing = false;
        }
    }

    private void ShowError()
    {
        _underline.Position = _viewModel?.HasError == true ? _viewModel.ErrorPosition : null;
        Editor.TextArea.TextView.InvalidateLayer(_underline.Layer);
    }

    /// <summary>A wavy underline under the token where the expression stopped parsing.</summary>
    private sealed class ErrorUnderline : IBackgroundRenderer
    {
        private readonly IPen _pen = new Pen(Brushes.Red, 1.2);

        public int? Position { get; set; }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Position is not { } position || textView.Document is not { } document || !textView.VisualLinesValid)
                return;
            var start = Math.Clamp(position, 0, document.TextLength);
            var end = start;
            while (end < document.TextLength && IsTokenChar(document.GetCharAt(end))) end++;
            // At the end of the text, or on an operator, mark the one character before or at the position.
            if (end == start) end = Math.Min(start + 1, document.TextLength);
            if (end == start && start > 0) start--;
            if (end == start) return;

            var segment = new TextSegment { StartOffset = start, EndOffset = end };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                drawingContext.DrawGeometry(null, _pen, WavyLine.Build(rect.BottomLeft, rect.Right));
        }

        private static bool IsTokenChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';
    }
}
