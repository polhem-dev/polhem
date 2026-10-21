using System.ComponentModel;
using Avalonia.Controls;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// Shows a database manager command running: the command line, its output line by line, and how it ended.
/// </summary>
public partial class DbCommandWindow : Window
{
    private DbCommandViewModel? _viewModel;

    public DbCommandWindow()
    {
        InitializeComponent();
    }

    /// <summary>Opens the window over <paramref name="owner"/> and starts the command.</summary>
    public static DbCommandWindow Start(Window owner, DbCommandViewModel viewModel)
    {
        var window = new DbCommandWindow { DataContext = viewModel };
        window._viewModel = viewModel;
        viewModel.CloseRequested += (_, _) => window.Close();
        viewModel.PropertyChanged += window.OnViewModelPropertyChanged;
        window.Show(owner);
        _ = viewModel.RunAsync();
        return window;
    }

    /// <summary>True while the command is still running.</summary>
    public bool IsRunning => _viewModel?.IsRunning ?? false;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Keeps the newest line in view, as a terminal does.
        if (e.PropertyName == nameof(DbCommandViewModel.OutputText))
            OutputBox.CaretIndex = OutputBox.Text?.Length ?? 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Closing the window while the command runs stops the command: nothing would show its outcome any more.
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
        }
        base.OnClosed(e);
    }
}
