using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Expressions;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// One expression being edited, checked as it is typed: why it does not parse and where, and what it uses outside the
/// expression allowlist. The expression editor edits one; the rule dialog edits a rule's When and Condition.
/// </summary>
public partial class ExpressionFieldViewModel : ObservableObject
{
    private Action<string> _validate;
    private Func<string, IReadOnlyList<ExpressionViolation>>? _findViolations;

    /// <param name="expression">The current expression.</param>
    /// <param name="validate">Checks the expression, throwing <see cref="ExpressionEvaluationException"/> when it does not
    /// parse.</param>
    /// <param name="findViolations">Lists what the expression uses outside the expression allowlist, shown as a warning;
    /// <c>null</c> for no such check.</param>
    /// <param name="completionSources">What the completion offers names from.</param>
    public ExpressionFieldViewModel(string expression, Action<string> validate,
        Func<string, IReadOnlyList<ExpressionViolation>>? findViolations, ExpressionCompletionSources completionSources)
    {
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(completionSources);
        _validate = validate;
        _findViolations = findViolations;
        _completionSources = completionSources;
        _expression = expression ?? string.Empty;
        Validate();
    }

    /// <summary>The expression being edited.</summary>
    [ObservableProperty]
    private string _expression;

    /// <summary>What the completion offers names from.</summary>
    [ObservableProperty]
    private ExpressionCompletionSources _completionSources;

    /// <summary>Why the expression does not parse; empty when it does.</summary>
    [ObservableProperty]
    private string _error = string.Empty;

    /// <summary>The offset in <see cref="Expression"/> where parsing failed, when the parser reports one.</summary>
    [ObservableProperty]
    private int? _errorPosition;

    /// <summary>Whether <see cref="Error"/> has a message to show.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>
    /// What the expression uses outside the expression allowlist; empty when nothing, or when it does not parse. The
    /// expression still works in a definition file, but an untrusted author would be refused it.
    /// </summary>
    [ObservableProperty]
    private string _warning = string.Empty;

    /// <summary>Whether <see cref="Warning"/> has a message to show.</summary>
    public bool HasWarning => Warning.Length > 0;

    partial void OnWarningChanged(string value) => OnPropertyChanged(nameof(HasWarning));

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnExpressionChanged(string value) => Validate();

    /// <summary>
    /// Checks the expression against other variables from now on, such as when a rule moves to another table, and checks
    /// it again.
    /// </summary>
    public void Reconfigure(Action<string> validate, Func<string, IReadOnlyList<ExpressionViolation>>? findViolations,
        ExpressionCompletionSources completionSources)
    {
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(completionSources);
        _validate = validate;
        _findViolations = findViolations;
        CompletionSources = completionSources;
        Validate();
    }

    /// <summary>Called after the expression was checked, so a subclass can follow it.</summary>
    protected virtual void OnValidated()
    {
    }

    // An empty expression is valid: an empty When means the rule always applies, and an empty field expression means
    // the field has none. Whether a rule may have an empty Condition is the rule dialog's to say.
    private void Validate()
    {
        ErrorPosition = null;
        Error = string.Empty;
        Warning = string.Empty;
        if (!string.IsNullOrWhiteSpace(Expression))
        {
            try
            {
                _validate(Expression);
            }
            catch (ExpressionEvaluationException ex)
            {
                ErrorPosition = ex.Position;
                Error = ex.Message;
            }
        }
        if (!HasError && _findViolations is not null && !string.IsNullOrWhiteSpace(Expression))
        {
            var violations = _findViolations(Expression);
            if (violations.Count > 0)
            {
                Warning = string.Format(CultureInfo.InvariantCulture,
                    LocalizationService.Current["Validation_ExpressionOutsideAllowlist"],
                    string.Join(" ", violations.Select(v => v.Message)));
            }
        }
        OnValidated();
    }
}
