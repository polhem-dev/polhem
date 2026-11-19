using System.ComponentModel;
using Microsoft.Extensions.AI;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.ViewModels;

public sealed partial class FormSchemaDocumentViewModel
{
    /// <summary>
    /// Creates the chat client the rule assistant talks to. <see langword="null"/>, the default, uses the provider in
    /// <see cref="UserSettings.DraftingProvider"/>.
    /// </summary>
    public Func<IChatClient>? RuleDraftingChatClientFactory { get; set; }

    /// <summary>
    /// Describes the credential the rule assistant will use. <see langword="null"/>, the default, asks the provider in
    /// <see cref="UserSettings.DraftingProvider"/>.
    /// </summary>
    public Func<DraftingCredential>? RuleDraftingCredentialProvider { get; set; }

    /// <summary>
    /// Raised after the user applied a drafted rule, with the message translations to write into the form's Language
    /// files. The main window writes them, since it knows which of those files are open.
    /// </summary>
    public event EventHandler<RuleMessagesAppliedEventArgs>? RuleMessagesApplied;

    /// <summary>
    /// Creates the assistant that drafts and explains <paramref name="rule"/> beside <paramref name="editor"/>. The
    /// explanation reads the rule as the editor holds it: the expression being typed in place of
    /// <paramref name="property"/>, and the message beside the condition.
    /// </summary>
    public RuleAssistantViewModel CreateRuleAssistant(ExpressionEditorViewModel editor, FormRule rule, PropertyDescriptor property)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(property);
        var files = RuleMessageFiles.Find(Solution, Schema.ProgId);
        var provider = UserSettings.Load().DraftingProvider;
        var credential = RuleDraftingCredentialProvider?.Invoke() ?? DraftingChatClients.DescribeCredential(provider);
        var createClient = RuleDraftingChatClientFactory ?? (() => DraftingChatClients.Create(provider));
        var assistant = new RuleAssistantViewModel(Schema, rule.RuleId, () => CurrentRule(editor, rule, property),
            s_expressionCalculator, files, credential, createClient, LocalTimeZoneId());
        foreach (var translation in assistant.Translations)
        {
            try
            {
                translation.Text = RuleMessageFiles.Read(translation.File, rule.RuleId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A file that cannot be read starts with no translation; writing it reports the problem.
            }
        }
        return assistant;
    }

    private static FormRule CurrentRule(ExpressionEditorViewModel editor, FormRule rule, PropertyDescriptor property) =>
        new(rule.RuleId,
            property.Name == nameof(FormRule.Condition) ? editor.Expression : rule.Condition,
            editor.HasMessage ? editor.Message : rule.Message)
        {
            TargetTable = rule.TargetTable,
            When = property.Name == nameof(FormRule.When) ? editor.Expression : rule.When,
        };

    private static bool ApplyRule(FormRule applied, FormRule rule)
    {
        var changed = !string.Equals(rule.TargetTable, applied.TargetTable, StringComparison.Ordinal)
            || !string.Equals(rule.When, applied.When, StringComparison.Ordinal)
            || !string.Equals(rule.Condition, applied.Condition, StringComparison.Ordinal)
            || !string.Equals(rule.Message, applied.Message, StringComparison.Ordinal);
        rule.TargetTable = applied.TargetTable;
        rule.When = applied.When;
        rule.Condition = applied.Condition;
        rule.Message = applied.Message;
        return changed;
    }
}
