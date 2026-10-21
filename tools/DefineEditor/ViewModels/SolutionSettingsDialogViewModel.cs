using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The Solution Settings dialog: edits the <see cref="SolutionSettings"/> of the open solution, currently the database
/// upgrade command.
/// </summary>
public sealed partial class SolutionSettingsDialogViewModel : ViewModelBase
{
    private readonly SolutionSettings _settings;

    public SolutionSettingsDialogViewModel(SolutionSettings settings)
    {
        _settings = settings;
        _command = settings.DatabaseUpgrade.Command;
        _argumentsText = ExternalCommand.JoinArguments(settings.DatabaseUpgrade.Arguments);
        _workingDirectory = settings.DatabaseUpgrade.WorkingDirectory;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecret))]
    private string _command;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecret))]
    private string _argumentsText;

    [ObservableProperty]
    private string _workingDirectory;

    /// <summary>
    /// True when the command line carries a password. It would sit in a file that is committed with the project; a
    /// password belongs in DatabaseSettings or in an environment variable instead.
    /// </summary>
    public bool HasSecret => SecretMasker.ContainsSecret(Command + " " + ArgumentsText);

    /// <summary>Raised after <see cref="ApplyCommand"/> copied the fields into the settings.</summary>
    public event EventHandler? Applied;

    /// <summary>Copies the fields into the settings the dialog was opened with.</summary>
    [RelayCommand]
    private void Apply()
    {
        _settings.DatabaseUpgrade = new ExternalCommand
        {
            Command = Command.Trim(),
            Arguments = ExternalCommand.SplitArguments(ArgumentsText),
            WorkingDirectory = WorkingDirectory.Trim(),
        };
        Applied?.Invoke(this, EventArgs.Empty);
    }
}
