using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The batch import wizard: choose form workbooks, choose the options, confirm, and watch them import one by one.
/// </summary>
public partial class ImportBatchWindow : Window
{
    private ImportBatchViewModel? _viewModel;

    public ImportBatchWindow()
    {
        InitializeComponent();
    }

    /// <summary>Opens the wizard over <paramref name="owner"/>.</summary>
    public static ImportBatchWindow Open(Window owner, ImportBatchViewModel viewModel)
    {
        var window = new ImportBatchWindow { DataContext = viewModel };
        window._viewModel = viewModel;
        viewModel.CloseRequested += (_, _) => window.Close();
        viewModel.PropertyChanged += window.OnViewModelPropertyChanged;
        window.Show(owner);
        return window;
    }

    private async void OnAddFiles(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Current["Dialog_PickFormWorkbooks"],
            AllowMultiple = true,
            FileTypeFilter = [MainWindow.ExcelWorkbookFileType],
        });
        _viewModel.AddFiles(files.Select(f => f.Path.LocalPath));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Keeps the newest line in view, as the database command window does.
        if (e.PropertyName == nameof(ImportBatchViewModel.OutputText))
            OutputBox.CaretIndex = OutputBox.Text?.Length ?? 0;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // A running batch finishes the workbook it is on, so its files and its tables do not end up half done. The
        // application quitting is the exception: then the upgrade under way is killed.
        if (_viewModel is { IsRunning: true } && e.CloseReason != WindowCloseReason.ApplicationShutdown)
        {
            e.Cancel = true;
            _viewModel.RequestStop();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Abort();
        }
        base.OnClosed(e);
    }
}
