using Microsoft.Extensions.AI;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
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
    /// Raised after the user confirmed a rule filled from a draft, with the message translations to write into the
    /// form's Language files. The main window writes them, since it knows which of those files are open.
    /// </summary>
    public event EventHandler<RuleMessagesAppliedEventArgs>? RuleMessagesApplied;

    /// <summary>
    /// Builds the dialog that adds a rule (<paramref name="rule"/> <c>null</c>, starting with
    /// <paramref name="newRuleId"/>) or edits <paramref name="rule"/>, with the AI assistant beside it.
    /// </summary>
    public RuleDialogViewModel CreateRuleDialog(FormRule? rule, string newRuleId = "new_rule")
    {
        var dialog = new RuleDialogViewModel(Schema, rule, newRuleId, s_expressionCalculator, s_evaluator, LocalTimeZoneId());
        dialog.Assistant = CreateRuleAssistant(dialog);
        return dialog;
    }

    /// <summary>
    /// Opens the rule dialog on <paramref name="rule"/> with the caret in <paramref name="focus"/>'s expression and writes
    /// what the user confirms back to it. Returns whether the rule changed.
    /// </summary>
    public async Task<bool> EditRuleAsync(FormRule rule, string focus = nameof(FormRule.Condition))
    {
        ArgumentNullException.ThrowIfNull(rule);
        var owner = GetOwnerWindow();
        if (owner is null) return false; // smoke / headless
        using var dialog = CreateRuleDialog(rule);
        if (!await RuleDialog.ShowAsync(owner, dialog, focus)) return false;
        var changed = dialog.ApplyTo(rule);
        RaiseRuleMessages(dialog, rule.RuleId);
        return changed;
    }

    /// <summary>
    /// Opens the rule dialog on the selected rule, for a double-click on its node; the tree and the property grid follow
    /// what the user confirms.
    /// </summary>
    public async Task EditSelectedRuleAsync()
    {
        if (SelectedTreeNode?.Value is not FormRule rule) return;
        if (await EditRuleAsync(rule)) OnSelectedObjectChangedByCommand();
    }

    // The translations are written only once the rule is confirmed: a draft the user filled in and then cancelled
    // leaves the Language files alone.
    private void RaiseRuleMessages(RuleDialogViewModel dialog, string ruleId)
    {
        if (dialog.Translations.Count > 0)
            RuleMessagesApplied?.Invoke(this, new RuleMessagesAppliedEventArgs(ruleId, dialog.Translations));
    }

    /// <summary>
    /// Creates the assistant that drafts and explains the rule in <paramref name="dialog"/>. The explanation reads the
    /// rule as the dialog holds it; a draft fills the dialog.
    /// </summary>
    public RuleAssistantViewModel CreateRuleAssistant(RuleDialogViewModel dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var files = RuleMessageFiles.Find(Solution, Schema.ProgId);
        var provider = UserSettings.Load().DraftingProvider;
        var credential = RuleDraftingCredentialProvider?.Invoke() ?? DraftingChatClients.DescribeCredential(provider);
        var createClient = RuleDraftingChatClientFactory ?? (() => DraftingChatClients.Create(provider, DraftingChatClients.RuleAssistantPurpose));
        var assistant = new RuleAssistantViewModel(Schema, dialog.RuleId, dialog.BuildRule,
            s_expressionCalculator, files, credential, createClient, LocalTimeZoneId());
        if (dialog.IsNew) return assistant;
        foreach (var translation in assistant.Translations)
        {
            try
            {
                translation.Text = RuleMessageFiles.Read(translation.File, dialog.RuleId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A file that cannot be read starts with no translation; writing it reports the problem.
            }
        }
        return assistant;
    }
}
