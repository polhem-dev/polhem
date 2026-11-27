using Polhem.Chat;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// A rule the assistant drafted or explained, as a card in its conversation: what the rule does, its expressions and the
/// samples the expression engine computed. Only the newest card of a draft offers its message and applies it.
/// </summary>
/// <remarks>
/// An entry of the rule assistant's conversation of the application's own, with its template in the rule assistant
/// panel. A card keeps what it showed when it was added, so an older one still reads as it did after a revision.
/// </remarks>
public sealed class RuleProposalEntry : ChatEntry
{
    /// <summary>Initializes a new instance of the <see cref="RuleProposalEntry"/> class.</summary>
    /// <param name="assistant">The assistant whose message, translations and apply command the newest card shows.</param>
    /// <param name="explanation">What the rule does, in plain words, as the model wrote it.</param>
    /// <param name="ruleText">The drafted rule's expressions; empty for an explanation.</param>
    /// <param name="samples">The samples, with what the engine computed for each.</param>
    public RuleProposalEntry(RuleAssistantViewModel assistant, string explanation, string ruleText,
        IReadOnlyList<RuleAssistantSample> samples)
    {
        ArgumentNullException.ThrowIfNull(assistant);
        ArgumentNullException.ThrowIfNull(explanation);
        ArgumentNullException.ThrowIfNull(ruleText);
        ArgumentNullException.ThrowIfNull(samples);
        Assistant = assistant;
        // The explanation is the model's text: a right-to-left override in it would make it read differently from what
        // it holds, so it shows escaped, as the chat view shows a reply.
        Explanation = VisibleText.EscapeBidiControls(explanation);
        RuleText = ruleText;
        Samples = samples;
    }

    /// <summary>Gets the assistant that added the card.</summary>
    public RuleAssistantViewModel Assistant { get; }

    /// <summary>Gets what the rule does, in plain words, with its bidirectional controls escaped.</summary>
    public string Explanation { get; }

    /// <summary>Gets the drafted rule's expressions, as text to read; empty for an explanation.</summary>
    public string RuleText { get; }

    /// <summary>Gets the samples, with what the engine computed for each.</summary>
    public IReadOnlyList<RuleAssistantSample> Samples { get; }

    /// <summary>Gets whether the card holds a drafted rule, rather than the explanation of the rule being edited.</summary>
    public bool IsDraft => RuleText.Length > 0;

    /// <summary>Gets whether this is the newest card of the conversation.</summary>
    public bool IsCurrent
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(CanApplyHere));
        }
    } = true;

    /// <summary>Gets whether the card offers the draft's message and applies it: only the newest card of a draft does.</summary>
    public bool CanApplyHere => IsDraft && IsCurrent;

    /// <summary>Marks the card as replaced by a newer one, which the user applies instead.</summary>
    public void Supersede() => IsCurrent = false;
}
