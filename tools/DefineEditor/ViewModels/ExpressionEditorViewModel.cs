using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Edits a field's ValueExpression or DefaultValueExpression, checking it as it is typed against the variables it is
/// evaluated with. A rule's When and Condition are edited together in <see cref="RuleDialogViewModel"/>.
/// </summary>
/// <remarks>
/// The expression is checked by <see cref="FormExpressionCalculator.ValidateExpression"/>, the check behind
/// <see cref="FormExpressionCalculator.ValidateExpressions"/>, so the dialog and the validation panel agree on what is
/// an error, detail aggregates included.
/// </remarks>
public sealed class ExpressionEditorViewModel : ExpressionFieldViewModel
{
    /// <param name="title">The window title: what is being edited.</param>
    /// <param name="symbols">What the expression can name.</param>
    /// <param name="validate">Checks the expression, throwing <see cref="ExpressionEvaluationException"/> when it does not
    /// parse.</param>
    /// <param name="expression">The current expression.</param>
    /// <param name="trial">The try-out area; <c>null</c> for none.</param>
    /// <param name="findViolations">Lists what the expression uses outside the expression allowlist, shown as a warning;
    /// <c>null</c> for no such check.</param>
    public ExpressionEditorViewModel(string title, ExpressionSymbolSet symbols, Action<string> validate, string expression,
        ExpressionTrialViewModel? trial = null, Func<string, IReadOnlyList<ExpressionViolation>>? findViolations = null)
        : base(expression, validate, findViolations, (symbols ?? throw new ArgumentNullException(nameof(symbols))).CompletionSources)
    {
        Title = title;
        Symbols = symbols;
        Trial = trial;
        Trial?.Update(Expression, !HasError, string.Empty);
    }

    /// <summary>The try-out area, where sample values give the expression's result; <c>null</c> when there is none.</summary>
    public ExpressionTrialViewModel? Trial { get; }

    /// <summary>The window title: what is being edited.</summary>
    public string Title { get; }

    /// <summary>What the expression can name.</summary>
    public ExpressionSymbolSet Symbols { get; }

    /// <summary>The fields of the table, which the expression names as variables.</summary>
    public IReadOnlyList<ExpressionSymbol> Fields => Symbols.Fields;

    /// <summary>The helper functions every expression can call.</summary>
    public IReadOnlyList<ExpressionSymbol> Functions => Symbols.Functions;

    /// <summary>The types an expression can name, each with its static members.</summary>
    public IReadOnlyList<ExpressionSymbolGroup> Types => Symbols.Types;

    /// <summary>The fields, the functions and the types, as the headings of the symbol tree.</summary>
    public IReadOnlyList<ExpressionSymbolGroup> SymbolGroups => Symbols.Groups;

    /// <inheritdoc/>
    protected override void OnValidated() => Trial?.Update(Expression, !HasError, string.Empty);
}
