using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
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

    // The width a phone-sized form gets; below the framework's compact threshold, like a phone in portrait.
    private const double CompactWidth = 400;

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
        if (_document is not { IsPreviewVisible: true } document)
        {
            LayoutPresenter.Layout = null;
            return;
        }

        var compact = document.IsPreviewCompact;
        LayoutPresenter.MaxWidth = compact ? CompactWidth : double.PositiveInfinity;
        LayoutPresenter.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
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

    // Every render builds new editors, so turning the names off needs no clean-up. An editor that already has a
    // placeholder (the date-time input pattern) keeps it.
    private void ShowFieldNames()
    {
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

    private void ShowError(Exception ex)
    {
        LayoutPresenter.Layout = null;
        ErrorText.Text = string.Format(LocalizationService.Current.Culture, LocalizationService.Current["Preview_RenderFailed"], ex.Message);
        ErrorText.IsVisible = true;
    }
}
