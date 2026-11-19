using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Expressions;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Adds a form rule, or edits one, as a whole: its id, the table it checks, when it applies, the condition that must
/// hold and the message, with the names it can use, a try-out of the whole rule and the AI assistant beside them.
/// Nothing reaches the form until the user confirms.
/// </summary>
/// <remarks>
/// Each expression is checked as <see cref="FormExpressionCalculator.ValidateExpressions"/> checks it, against the
/// fields of the table the rule checks, and again when the table changes.
/// </remarks>
public sealed partial class RuleDialogViewModel : ViewModelBase, IDisposable
{
    private readonly FormSchema _schema;
    private readonly FormExpressionCalculator _calculator;
    private readonly DynamicExpressoEvaluator _evaluator;
    private readonly string _timeZoneId;
    private readonly HashSet<string> _otherRuleIds;

    /// <param name="schema">The form; it is read, never changed.</param>
    /// <param name="rule">The rule to edit; <c>null</c> to add one.</param>
    /// <param name="newRuleId">The id a new rule starts with.</param>
    /// <param name="calculator">Checks the expressions and evaluates the try-out.</param>
    /// <param name="evaluator">The evaluator whose names the dialog lists.</param>
    /// <param name="timeZoneId">The IANA time zone id <c>Today()</c> and <c>Now()</c> see in the try-out.</param>
    public RuleDialogViewModel(FormSchema schema, FormRule? rule, string newRuleId, FormExpressionCalculator calculator,
        DynamicExpressoEvaluator evaluator, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(evaluator);
        _schema = schema;
        _calculator = calculator;
        _evaluator = evaluator;
        _timeZoneId = timeZoneId;
        IsNew = rule is null;
        _otherRuleIds = (schema.Rules ?? []).Where(r => !ReferenceEquals(r, rule)).Select(r => r.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var choices = (schema.Tables ?? [])
            .Select(t => new RuleTableChoice(ReferenceEquals(t, schema.MasterTable) ? string.Empty : t.TableName, t,
                ReferenceEquals(t, schema.MasterTable)
                    ? L("RuleDialog_MasterTable", t.TableName, t.DisplayName)
                    : L("RuleDialog_DetailTable", t.TableName, t.DisplayName)))
            .ToList();
        var target = rule?.TargetTable ?? string.Empty;
        var selected = FormExpressionCalculator.ResolveRuleTable(rule ?? new FormRule("x", "true", string.Empty), schema)
            is { } resolved ? choices.FirstOrDefault(c => ReferenceEquals(c.Table, resolved)) : null;
        if (selected is null)
        {
            selected = new RuleTableChoice(target, null, L("RuleDialog_MissingTable", target));
            choices.Add(selected);
        }
        Tables = choices;
        _selectedTable = selected;

        _ruleId = rule?.RuleId ?? newRuleId;
        _trigger = rule?.Trigger ?? FormRuleTrigger.BeforeSave;
        _enabled = rule?.Enabled ?? true;
        _message = rule?.Message ?? string.Empty;
        _symbols = BuildSymbols(selected);
        When = new ExpressionFieldViewModel(rule?.When ?? string.Empty, Validator(nameof(FormRule.When)),
            Violations(nameof(FormRule.When)), _symbols.CompletionSources);
        Condition = new ExpressionFieldViewModel(rule?.Condition ?? string.Empty, Validator(nameof(FormRule.Condition)),
            Violations(nameof(FormRule.Condition)), _symbols.CompletionSources);
        _trial = BuildTrial(selected);
        When.PropertyChanged += OnExpressionChanged;
        Condition.PropertyChanged += OnExpressionChanged;
        Refresh();
    }

    /// <summary>Whether the dialog adds a rule rather than editing one.</summary>
    public bool IsNew { get; }

    /// <summary>The window title.</summary>
    public string Title => IsNew ? L("RuleDialog_AddTitle") : L("RuleDialog_EditTitle", RuleId);

    /// <summary>The rule id; only a new rule's can be changed here, since translations are stored under it.</summary>
    [ObservableProperty]
    private string _ruleId;

    /// <summary>The tables the rule can check: the master table, then each detail table.</summary>
    public IReadOnlyList<RuleTableChoice> Tables { get; }

    /// <summary>The table the rule checks.</summary>
    [ObservableProperty]
    private RuleTableChoice _selectedTable;

    /// <summary>The actions a rule can run before.</summary>
    public IReadOnlyList<FormRuleTrigger> Triggers { get; } = Enum.GetValues<FormRuleTrigger>();

    /// <summary>When the rule is checked.</summary>
    [ObservableProperty]
    private FormRuleTrigger _trigger;

    /// <summary>Whether the rule is checked at all.</summary>
    [ObservableProperty]
    private bool _enabled;

    /// <summary>The message shown when the condition fails, the English base text.</summary>
    [ObservableProperty]
    private string _message;

    /// <summary>When the rule applies; empty when it always does.</summary>
    public ExpressionFieldViewModel When { get; }

    /// <summary>What must hold for the save or delete to go on.</summary>
    public ExpressionFieldViewModel Condition { get; }

    /// <summary>What the expressions can name, for the table the rule checks.</summary>
    [ObservableProperty]
    private ExpressionSymbolSet _symbols;

    /// <summary>The try-out of the whole rule, for the table the rule checks.</summary>
    [ObservableProperty]
    private ExpressionTrialViewModel _trial;

    /// <summary>Why the rule cannot be confirmed yet; empty when it can.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _problem = string.Empty;

    /// <summary>Whether the rule can be confirmed.</summary>
    public bool CanConfirm => Problem.Length == 0;

    /// <summary>
    /// Writes the rule from plain words and explains it; <c>null</c> when the dialog runs without one.
    /// </summary>
    public RuleAssistantViewModel? Assistant
    {
        get => _assistant;
        set
        {
            if (_assistant is not null) _assistant.Applied -= OnDraftApplied;
            _assistant = value;
            if (_assistant is not null) _assistant.Applied += OnDraftApplied;
            OnPropertyChanged();
        }
    }

    private RuleAssistantViewModel? _assistant;

    /// <summary>
    /// The message translations of the draft the user used, to write into the form's Language files when the dialog is
    /// confirmed; empty when no draft was used.
    /// </summary>
    public IReadOnlyList<RuleMessageTranslation> Translations { get; private set; } = [];

    /// <summary>The rule as the dialog holds it now.</summary>
    public FormRule BuildRule() => new(RuleId.Trim(), Condition.Expression.Trim(), Message)
    {
        TargetTable = SelectedTable.TargetTable,
        When = When.Expression.Trim(),
        Trigger = Trigger,
        Enabled = Enabled,
    };

    /// <summary>Writes what the dialog holds into <paramref name="rule"/>; returns whether anything changed.</summary>
    public bool ApplyTo(FormRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var built = BuildRule();
        var changed = !string.Equals(rule.TargetTable, built.TargetTable, StringComparison.Ordinal)
            || !string.Equals(rule.When, built.When, StringComparison.Ordinal)
            || !string.Equals(rule.Condition, built.Condition, StringComparison.Ordinal)
            || !string.Equals(rule.Message, built.Message, StringComparison.Ordinal)
            || rule.Trigger != built.Trigger || rule.Enabled != built.Enabled;
        rule.TargetTable = built.TargetTable;
        rule.When = built.When;
        rule.Condition = built.Condition;
        rule.Message = built.Message;
        rule.Trigger = built.Trigger;
        rule.Enabled = built.Enabled;
        return changed;
    }

    partial void OnRuleIdChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        Refresh();
    }

    partial void OnMessageChanged(string value) => Refresh();

    partial void OnSelectedTableChanged(RuleTableChoice value)
    {
        Symbols = BuildSymbols(value);
        Trial = BuildTrial(value);
        When.Reconfigure(Validator(nameof(FormRule.When)), Violations(nameof(FormRule.When)), Symbols.CompletionSources);
        Condition.Reconfigure(Validator(nameof(FormRule.Condition)), Violations(nameof(FormRule.Condition)),
            Symbols.CompletionSources);
        Refresh();
    }

    // A draft from the assistant fills the dialog; the user confirms it like anything typed.
    private void OnDraftApplied(object? sender, EventArgs e)
    {
        if (Assistant?.AppliedRule is not { } draft) return;
        SelectedTable = Tables.FirstOrDefault(c => c.Table is not null
                && string.Equals(c.TargetTable, draft.TargetTable, StringComparison.OrdinalIgnoreCase))
            ?? SelectedTable;
        When.Expression = draft.When;
        Condition.Expression = draft.Condition;
        Message = draft.Message;
        Translations = Assistant.AppliedTranslations;
    }

    private void OnExpressionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExpressionFieldViewModel.Expression) or nameof(ExpressionFieldViewModel.Error))
            Refresh();
    }

    private void Refresh()
    {
        Trial.UpdateRule(When.Expression, !When.HasError, Condition.Expression, !Condition.HasError, Message);
        Problem = FindProblem();
    }

    private string FindProblem()
    {
        var id = RuleId.Trim();
        if (id.Length == 0) return L("RuleDialog_NoRuleId");
        if (_otherRuleIds.Contains(id)) return L("RuleDialog_DuplicateRuleId", id);
        if (SelectedTable.Table is null) return L("RuleDialog_MissingTable", SelectedTable.TargetTable);
        if (string.IsNullOrWhiteSpace(Condition.Expression)) return L("RuleDialog_NoCondition");
        if (When.HasError || Condition.HasError) return L("RuleDialog_ExpressionError");
        return string.Empty;
    }

    private ExpressionSymbolSet BuildSymbols(RuleTableChoice choice) =>
        new(choice.Table ?? _schema.MasterTable ?? new FormTable(), _evaluator);

    private ExpressionTrialViewModel BuildTrial(RuleTableChoice choice) =>
        new(choice.Table ?? _schema.MasterTable ?? new FormTable(), null, nameof(FormRule.Condition), _calculator,
            _evaluator, _timeZoneId);

    // A rule whose table is missing gets no check: validation skips it too, and confirming is refused.
    private Action<string> Validator(string propertyName) => text =>
    {
        if (SelectedTable?.Table is { } table)
            _calculator.ValidateExpression(_schema, table, propertyName, text);
    };

    private Func<string, IReadOnlyList<ExpressionViolation>> Violations(string propertyName) => text =>
        SelectedTable?.Table is { } table
            ? _calculator.FindAllowlistViolations(_schema, table, propertyName, text)
            : [];

    /// <summary>Stops the assistant's request in progress and releases its chat client.</summary>
    public void Dispose()
    {
        if (_assistant is not null)
        {
            _assistant.Applied -= OnDraftApplied;
            _assistant.Dispose();
        }
    }
}
