using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A rule message's translation into one language of the form, which the user can change before it is applied.</summary>
public sealed partial class RuleMessageTranslation : ObservableObject
{
    /// <param name="file">The Language file the translation is written to.</param>
    public RuleMessageTranslation(RuleMessageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        File = file;
    }

    /// <summary>The Language file the translation is written to.</summary>
    public RuleMessageFile File { get; }

    /// <summary>The language, such as <c>zh-TW</c>.</summary>
    public string Language => File.Language;

    /// <summary>The translated message.</summary>
    [ObservableProperty]
    private string _text = string.Empty;
}
