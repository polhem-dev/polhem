using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>Edits one form schema expression, with the fields and functions it can use beside it.</summary>
public partial class ExpressionEditorDialog : Window
{
    private readonly ErrorUnderline _underline = new();
    private ExpressionEditorViewModel? _viewModel;

    public ExpressionEditorDialog()
    {
        InitializeComponent();
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.TextArea.TextView.BackgroundRenderers.Add(_underline);
        Editor.TextChanged += (_, _) =>
        {
            if (_viewModel is not null) _viewModel.Expression = Editor.Text;
        };
        Opened += (_, _) =>
        {
            Editor.Focus();
            Editor.CaretOffset = Editor.Document.TextLength;
        };
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/>; returns <c>true</c> when the user confirmed, after which
    /// <see cref="ExpressionEditorViewModel.Expression"/> and <see cref="ExpressionEditorViewModel.Message"/> hold the
    /// new values.
    /// </summary>
    public static Task<bool> ShowAsync(Window owner, ExpressionEditorViewModel viewModel) =>
        new ExpressionEditorDialog { DataContext = viewModel }.ShowDialog<bool>(owner);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as ExpressionEditorViewModel;
        if (_viewModel is null) return;
        Editor.Document = new TextDocument(_viewModel.Expression);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShowError();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExpressionEditorViewModel.ErrorPosition) or nameof(ExpressionEditorViewModel.Error))
            ShowError();
    }

    private void ShowError()
    {
        _underline.Position = _viewModel?.HasError == true ? _viewModel.ErrorPosition : null;
        Editor.TextArea.TextView.InvalidateLayer(_underline.Layer);
    }

    private void OnSymbolDoubleTapped(object? sender, TappedEventArgs e) => InsertSelected(sender);

    private void OnSymbolKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        InsertSelected(sender);
        e.Handled = true;
    }

    private void InsertSelected(object? sender)
    {
        if (sender is not ListBox { SelectedItem: ExpressionSymbol symbol }) return;
        var selection = Editor.TextArea.Selection;
        if (!selection.IsEmpty) selection.ReplaceSelectionWithText(string.Empty);
        Editor.Document.Insert(Editor.CaretOffset, symbol.InsertText);
        Editor.Focus();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

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
