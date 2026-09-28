using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
    /// Shows the in-window File menu and binds its items to the given commands. Called off macOS,
    /// where the native menu bar that <see cref="App.ConfigureWindowMenu"/> builds is not rendered.
    /// </summary>
    /// <remarks>
    /// The welcome panel's "File → Open Folder" hint is hidden at the same time: this menu does not
    /// carry Open Folder, and the panel's own button is the way to open a solution here.
    /// </remarks>
    /// <param name="save">Saves the active document.</param>
    /// <param name="saveAll">Saves every dirty document.</param>
    /// <param name="validate">Validates the active document.</param>
    /// <param name="closeTab">Closes the active tab.</param>
    public void ShowInWindowFileMenu(ICommand save, ICommand saveAll, ICommand validate, ICommand closeTab)
    {
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
