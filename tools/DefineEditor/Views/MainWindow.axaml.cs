using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class MainWindow : Window
{
    private GridLength _sidebarWidth = new(320);

    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplySidebarVisibility(_viewModel.IsSidebarVisible);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarVisible) && _viewModel is not null)
            ApplySidebarVisibility(_viewModel.IsSidebarVisible);
    }

    /// <summary>
    /// Collapses or restores the sidebar column. The width the splitter last left is kept and given back on restore.
    /// </summary>
    private void ApplySidebarVisibility(bool visible)
    {
        var column = BodyGrid.ColumnDefinitions[1];
        if (!visible && column.Width.Value > 0) _sidebarWidth = column.Width;
        column.Width = visible ? _sidebarWidth : new GridLength(0);
        Sidebar.IsVisible = visible;
        SidebarSplitter.IsVisible = visible;
    }

    /// <summary>
    /// Pops the OS folder-picker and opens the chosen folder as a DefinePath
    /// solution. Public so the macOS NativeMenu (set up in
    /// <see cref="App.ConfigureWindowMenu"/>) can invoke it through a
    /// command — the picker needs this window's <see cref="StorageProvider"/>,
    /// which only Window-derived types expose.
    /// </summary>
    public async void PromptOpenSolution()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationService.Current["Welcome_OpenFolderTip"],
            AllowMultiple = false,
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
        {
            vm.OpenSolution(folders[0].Path.LocalPath);
        }
    }

    /// <summary>
    /// Pops the OS file picker for a form workbook and imports the chosen one into the open solution.
    /// Public for the same reason as <see cref="PromptOpenSolution"/>: the picker needs this window's
    /// <see cref="StorageProvider"/>.
    /// </summary>
    public async Task PromptImportFormWorkbookAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Current["Dialog_PickFormWorkbook"],
            AllowMultiple = false,
            FileTypeFilter = [ExcelWorkbookFileType],
        });

        if (files.Count > 0 && DataContext is MainWindowViewModel vm)
            await vm.ImportFormWorkbookAsync(files[0].Path.LocalPath);
    }

    /// <summary>The file type of a form workbook, for the open and save pickers.</summary>
    public static FilePickerFileType ExcelWorkbookFileType => new(LocalizationService.Current["Dialog_ExcelWorkbook"])
    {
        Patterns = ["*.xlsx"],
        AppleUniformTypeIdentifiers = ["org.openxmlformats.spreadsheetml.sheet"],
        MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
    };

    /// <summary>
    /// Shows the in-window File menu and binds its items to the given commands. Called off macOS,
    /// where the native menu bar that <see cref="App.ConfigureWindowMenu"/> builds is not rendered.
    /// </summary>
    /// <remarks>
    /// The welcome panel's "File → Open Folder" hint is hidden at the same time: this menu does not
    /// carry Open Folder, and the panel's own button is the way to open a solution here.
    /// </remarks>
    /// <param name="addFrameworkDefaults">Adds the framework default files the open solution lacks.</param>
    /// <param name="importFormWorkbook">Imports a form described in an Excel workbook.</param>
    /// <param name="importFormWorkbooks">Opens the wizard that imports several workbooks in a row.</param>
    /// <param name="checkConnections">Opens a connection to each database of the open solution.</param>
    /// <param name="upgradeDatabase">Builds or upgrades the open solution's database tables.</param>
    /// <param name="previewDatabaseUpgrade">Lists what the upgrade would change without changing it.</param>
    /// <param name="save">Saves the active document.</param>
    /// <param name="saveAll">Saves every dirty document.</param>
    /// <param name="validate">Validates the active document.</param>
    /// <param name="closeTab">Closes the active tab.</param>
    public void ShowInWindowFileMenu(
        ICommand addFrameworkDefaults, ICommand importFormWorkbook, ICommand importFormWorkbooks, ICommand checkConnections, ICommand upgradeDatabase,
        ICommand previewDatabaseUpgrade, ICommand save, ICommand saveAll, ICommand validate, ICommand closeTab)
    {
        AddFrameworkDefaultsMenuItem.Command = addFrameworkDefaults;
        ImportFormWorkbookMenuItem.Command = importFormWorkbook;
        ImportFormWorkbooksMenuItem.Command = importFormWorkbooks;
        CheckConnectionsMenuItem.Command = checkConnections;
        UpgradeDatabaseMenuItem.Command = upgradeDatabase;
        PreviewDatabaseUpgradeMenuItem.Command = previewDatabaseUpgrade;
        SaveMenuItem.Command = save;
        SaveAllMenuItem.Command = saveAll;
        ValidateMenuItem.Command = validate;
        CloseTabMenuItem.Command = closeTab;
        InWindowMenu.IsVisible = true;
        MenuAlternativeHint.IsVisible = false;
    }

    // Welcome-panel "Open Folder" button click handler.
    private void OnOpenSolutionClick(object? sender, RoutedEventArgs e) => PromptOpenSolution();
}
