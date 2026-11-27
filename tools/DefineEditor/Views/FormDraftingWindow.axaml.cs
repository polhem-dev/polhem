using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.Chat;
using Polhem.Chat.Avalonia;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Editing;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The window that drafts a form from documents: the conversation on the left, the form as it stands on the right.
/// </summary>
public partial class FormDraftingWindow : Window
{
    // The same pause and phone width as the FormLayout editor's preview, so the two show a form the same way.
    private static readonly TimeSpan RenderDelay = TimeSpan.FromMilliseconds(300);
    private const double CompactWidth = 390;

    private readonly DispatcherTimer _renderTimer;
    private FormDraftingViewModel? _viewModel;

    public FormDraftingWindow()
    {
        InitializeComponent();
        _renderTimer = new DispatcherTimer { Interval = RenderDelay };
        _renderTimer.Tick += (_, _) =>
        {
            _renderTimer.Stop();
            _viewModel?.RefreshPreview();
        };
        // Tunnelling, and with handled events too, so a field editor still takes its own click afterwards.
        PreviewSheet.AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        ChatLanguage.Follow(Chat, "Drafting_InputWatermark");
        LocalizationService.Current.CultureChanged += OnCultureChanged;
    }

    /// <summary>Opens the window over <paramref name="owner"/>.</summary>
    public static FormDraftingWindow Open(Window owner, FormDraftingViewModel viewModel)
    {
        var window = new FormDraftingWindow { DataContext = viewModel };
        window._viewModel = viewModel;
        viewModel.DraftChanged += window.OnDraftChanged;
        viewModel.PropertyChanged += window.OnViewModelPropertyChanged;
        viewModel.RefreshPreview();
        window.Show(owner);
        return window;
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderTimer.Stop();
        LocalizationService.Current.CultureChanged -= OnCultureChanged;
        if (_viewModel is not null)
        {
            _viewModel.DraftChanged -= OnDraftChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.OnClosed(e);
    }

    // WARNING: Under the Semi theme, a tab whose header grows keeps its old width, and its text is cut off, when another
    // element of the window changes size in the same layout pass: the tab strip's panel takes the new sizes but does not
    // arrange its tabs again. A change of language does both at once, since the window's heading changes too. Arranging
    // the panel again once that pass is over gives the tabs their new widths. The simple theme does not show it.
    private void OnCultureChanged(object? sender, CultureInfo culture)
        => Dispatcher.UIThread.Post(() => DraftTabs.ItemsPanelRoot?.InvalidateMeasure(), DispatcherPriority.Background);

    private void OnDraftChanged(object? sender, EventArgs e)
    {
        // The model changes the draft in bursts of tool calls, so the preview waits for a pause.
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FormDraftingViewModel.PreviewLayout)
            or nameof(FormDraftingViewModel.IsPreviewCompact)
            or nameof(FormDraftingViewModel.IsPreviewViewMode))
        {
            Render();
        }
    }

    private void Render()
    {
        if (_viewModel is null) return;
        var compact = _viewModel.IsPreviewCompact;
        LayoutPresenter.Width = compact ? CompactWidth : double.NaN;
        PreviewSheet.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        PreviewSheet.Classes.Set("phone", compact);
        PreviewScroller.HorizontalScrollBarVisibility = compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        try
        {
            FormScope.SetFormMode(LayoutPresenter, _viewModel.IsPreviewViewMode ? SingleFormMode.View : SingleFormMode.Edit);
            LayoutPresenter.IsCompact = compact;
            LayoutPresenter.Layout = _viewModel.PreviewLayout;
            LayoutPresenter.Refresh();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            LayoutPresenter.Layout = null;
            _viewModel.PreviewError = ex.Message;
        }
    }

    // A click on a field of the form, or on a column of a detail grid, shows that field in the Fields tab.
    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null || e.Source is not Visual source) return;
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is IFieldEditor editor && !string.IsNullOrEmpty(editor.FieldName))
            {
                RevealLater(editor.FieldName);
                return;
            }
            if (visual is DataGridColumnHeader header && header.FindAncestorOfType<GridControl>() is { } grid
                && ColumnFieldName(grid, header) is { } fieldName)
            {
                RevealLater(fieldName);
                return;
            }
        }
    }

    // The editor still has to take the click that is tunnelling to it, so the tab switches afterwards.
    private void RevealLater(string fieldName)
        => Dispatcher.UIThread.Post(() => _viewModel?.RevealField(fieldName));

    // NOTE: the grid builds one column per visible layout column, in layout order.
    private static string? ColumnFieldName(GridControl grid, DataGridColumnHeader header)
    {
        var index = grid.InnerGrid.Columns.ToList().FindIndex(c => ReferenceEquals(c.Header, header.Content));
        var visible = grid.Layout?.Columns?.Where(c => c.Visible).ToList();
        return index >= 0 && visible is not null && index < visible.Count ? visible[index].FieldName : null;
    }

    private void OnFieldRowEditEnded(object? sender, DataGridRowEditEndedEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit && e.Row.DataContext is DraftFieldRow row)
            Dispatcher.UIThread.Post(() => _viewModel?.CommitFieldEdit(row));
    }

    private void OnPromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        if (_viewModel is null) return;
        _ = _viewModel.SendAsync(e.Text);
    }

    private void OnStopRequested(object? sender, EventArgs e) => _viewModel?.Stop();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
