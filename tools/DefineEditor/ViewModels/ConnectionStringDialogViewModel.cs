using Polhem.Definition.Database;
using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The paste-and-parse dialog for a connection string: the user pastes a complete string, previews how
/// <see cref="ConnectionStringParser"/> splits it, and applies the result to a DatabaseServer or DatabaseItem.
/// </summary>
/// <remarks>
/// The preview never shows the password itself, only whether the string carried one.
/// </remarks>
public sealed partial class ConnectionStringDialogViewModel : ViewModelBase
{
    private const string MaskedPassword = "●●●●●●";

    public ConnectionStringDialogViewModel(DatabaseType databaseType, bool forItem)
    {
        DatabaseType = databaseType;
        ForItem = forItem;
    }

    /// <summary>The database type whose key aliases the parser uses.</summary>
    public DatabaseType DatabaseType { get; }

    /// <summary>Whether the target is a DatabaseItem, which also takes the database name.</summary>
    public bool ForItem { get; }

    public string Hint => L(ForItem ? "DatabaseSettings_ParseHintItem" : "DatabaseSettings_ParseHint");

    public string Placeholder => L(ForItem ? "DatabaseSettings_PastePlaceholderItem" : "DatabaseSettings_PastePlaceholderServer");

    public string ApplyLabel => L(ForItem ? "DatabaseSettings_ApplyItem" : "DatabaseSettings_ApplyServer");

    [ObservableProperty]
    private string _pasteInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParseResult))]
    [NotifyPropertyChangedFor(nameof(PasswordPreview))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private ConnectionStringParseResult? _parseResult;

    public bool HasParseResult => ParseResult is not null;

    /// <summary>A mask when the parsed string carried a password, otherwise empty.</summary>
    public string PasswordPreview => string.IsNullOrEmpty(ParseResult?.Password) ? string.Empty : MaskedPassword;

    /// <summary>Occurs when the user applies a parse result without errors.</summary>
    public event EventHandler? Applied;

    [RelayCommand]
    private void Parse() => ParseResult = ConnectionStringParser.Parse(PasteInput, DatabaseType);

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => Applied?.Invoke(this, EventArgs.Empty);

    private bool CanApply() => ParseResult is { IsOk: true };
}
