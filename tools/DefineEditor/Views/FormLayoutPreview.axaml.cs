using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Polhem.DefineEditor.BrowserPreview;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The live preview of the FormLayout being edited: the framework's <see cref="Polhem.UI.Avalonia.Controls.FormLayoutPresenter"/>
/// renders the layout alone, with no data and no FormSchema, so the preview shows exactly what the layout defines.
/// </summary>
public partial class FormLayoutPreview : UserControl
{
    // Edits arrive in bursts (typing in the property grid writes on every commit), so the preview waits for a pause
    // instead of rebuilding the whole form on each one.
    private static readonly TimeSpan RenderDelay = TimeSpan.FromMilliseconds(300);

    // The phone form gets a fixed width, the logical width of a common phone in portrait (iPhone 12 to 16), so it
    // keeps its size whatever the width of the pane. It is below the framework's compact threshold.
    private const double CompactWidth = 390;

    private readonly DispatcherTimer _renderTimer;
    private FormLayoutDocumentViewModel? _document;

    public FormLayoutPreview()
    {
        InitializeComponent();
        _renderTimer = new DispatcherTimer { Interval = RenderDelay };
        _renderTimer.Tick += (_, _) =>
        {
            _renderTimer.Stop();
            Render();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_document is not null)
        {
            _document.DocumentChanged -= OnDocumentChanged;
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }
        _document = DataContext as FormLayoutDocumentViewModel;
        if (_document is not null)
        {
            _document.DocumentChanged += OnDocumentChanged;
            _document.PropertyChanged += OnDocumentPropertyChanged;
        }
        Render();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _renderTimer.Stop();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FormLayoutDocumentViewModel.IsPreviewVisible)
            or nameof(FormLayoutDocumentViewModel.PreviewFormMode)
            or nameof(FormLayoutDocumentViewModel.IsPreviewCompact)
            or nameof(FormLayoutDocumentViewModel.ShowPreviewFieldNames))
        {
            Render();
        }
    }

    private void Render()
    {
        _renderTimer.Stop();
        PublishToBrowser();
        if (_document is not { IsPreviewVisible: true } document)
        {
            LayoutPresenter.Layout = null;
            return;
        }

        var compact = document.IsPreviewCompact;
        LayoutPresenter.Width = compact ? CompactWidth : double.NaN;
        LayoutPresenter.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        // A pane narrower than the phone scrolls sideways instead of squeezing the phone form.
        PreviewScroller.HorizontalScrollBarVisibility = compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        try
        {
            FormScope.SetFormMode(LayoutPresenter, document.PreviewFormMode);
            LayoutPresenter.IsCompact = compact;
            LayoutPresenter.Layout = document.Root;
            // The layout is edited in place, so the reference above rarely changes; render explicitly.
            LayoutPresenter.Refresh();
            if (document.ShowPreviewFieldNames)
                ShowFieldNames();
            ErrorText.IsVisible = false;
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex);
        }
        catch (ArgumentException ex)
        {
            ShowError(ex);
        }
    }

    // Every render builds new editors and grids, so turning the names off needs no clean-up. An editor that already
    // has a placeholder (the date-time input pattern) keeps it; a detail grid, which has no rows to hold one, shows
    // each field name under its column caption.
    private void ShowFieldNames()
    {
        foreach (var grid in LayoutPresenter.GetLogicalDescendants().OfType<GridControl>())
            ShowColumnFieldNames(grid);

        foreach (var editor in LayoutPresenter.GetLogicalDescendants().OfType<IFieldEditor>())
        {
            switch (editor)
            {
                case TextBox textBox when string.IsNullOrEmpty(textBox.PlaceholderText):
                    textBox.PlaceholderText = editor.FieldName;
                    break;
                case ComboBox comboBox when string.IsNullOrEmpty(comboBox.PlaceholderText):
                    comboBox.PlaceholderText = editor.FieldName;
                    break;
            }
        }
    }

    private static void ShowColumnFieldNames(GridControl grid)
    {
        if (grid.Layout?.Columns is not { } layoutColumns) return;
        // NOTE: the grid builds one column per visible layout column, in layout order.
        var visible = layoutColumns.Where(c => c.Visible).ToList();
        var columns = grid.InnerGrid.Columns;
        for (var i = 0; i < Math.Min(columns.Count, visible.Count); i++)
        {
            var caption = columns[i].Header as Control ?? new TextBlock { Text = columns[i].Header?.ToString() };
            var fieldName = new TextBlock { Text = visible[i].FieldName, FontSize = 11 };
            fieldName.Classes.Add("muted");
            columns[i].Header = new StackPanel { Children = { caption, fieldName } };
        }
    }

    // The browser follows the document whether or not this pane draws it, but only once a browser preview was opened.
    private void PublishToBrowser()
    {
        if (_document is null || !BrowserPreviewHost.IsSharedCreated || !BrowserPreviewHost.Shared.IsRunning) return;
        var hub = BrowserPreviewHost.Shared.Hub;
        hub.Publish(hub.GetDocumentId(_document.DocumentKey), _document.CreateBrowserPreviewSnapshot());
    }

    private void ShowError(Exception ex)
    {
        LayoutPresenter.Layout = null;
        ErrorText.Text = string.Format(LocalizationService.Current.Culture, LocalizationService.Current["Preview_RenderFailed"], ex.Message);
        ErrorText.IsVisible = true;
    }
}
