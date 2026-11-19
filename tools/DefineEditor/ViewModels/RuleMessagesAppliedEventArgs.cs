namespace Polhem.DefineEditor.ViewModels;

/// <summary>The translations of a drafted rule's message the user applied.</summary>
/// <param name="ruleId">The rule's id, which the translations are stored under.</param>
/// <param name="translations">The message in each language of the form, as the user left it.</param>
public sealed class RuleMessagesAppliedEventArgs(string ruleId, IReadOnlyList<RuleMessageTranslation> translations) : EventArgs
{
    /// <summary>The rule's id.</summary>
    public string RuleId { get; } = ruleId;

    /// <summary>The message in each language of the form.</summary>
    public IReadOnlyList<RuleMessageTranslation> Translations { get; } = translations;
}
