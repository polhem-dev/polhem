using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Expressions;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The expression editor's try-out area: sample values for the fields the expression reads, and what the expression
/// gives for them.
/// </summary>
/// <remarks>
/// The expression is evaluated by <see cref="FormExpressionCalculator.EvaluateRuleSample"/> or
/// <see cref="FormExpressionCalculator.EvaluateFieldSample"/>, which coerce the values and the result as the run-time
/// passes do, so the editor keeps no rule of its own about them.
/// </remarks>
public sealed partial class ExpressionTrialViewModel : ObservableObject
{
    private readonly FormTable _table;
    private readonly FormField? _field;
    private readonly string _propertyName;
    private readonly FormExpressionCalculator _calculator;
    private readonly IExpressionEvaluator _evaluator;
    private readonly string _timeZoneId;
    private string _expression = string.Empty;
    private string _message = string.Empty;
    private bool _parses;

    /// <param name="table">The table whose fields the expression reads.</param>
    /// <param name="field">The field a field expression computes; <c>null</c> for a rule.</param>
    /// <param name="propertyName">The property the expression belongs to, such as <c>Condition</c>.</param>
    /// <param name="calculator">Evaluates the expression as the run time does.</param>
    /// <param name="evaluator">Finds the fields the expression reads.</param>
    /// <param name="timeZoneId">The IANA time zone id <c>Today()</c> and <c>Now()</c> see.</param>
    public ExpressionTrialViewModel(FormTable table, FormField? field, string propertyName,
        FormExpressionCalculator calculator, IExpressionEvaluator evaluator, string timeZoneId)
    {
        _table = table;
        _field = field;
        _propertyName = propertyName;
        _calculator = calculator;
        _evaluator = evaluator;
        _timeZoneId = timeZoneId;
    }

    /// <summary>The fields the expression reads, in table order, each with its sample value.</summary>
    public ObservableCollection<ExpressionTrialInput> Inputs { get; } = [];

    /// <summary>What the try-out area shows.</summary>
    [ObservableProperty]
    private ExpressionTrialOutcome _outcome;

    /// <summary>Whether there is a result to show.</summary>
    public bool HasResult => Outcome != ExpressionTrialOutcome.None;

    /// <summary>Whether the result is a failed rule or an error, shown as a problem.</summary>
    public bool IsProblem => Outcome is ExpressionTrialOutcome.Failed or ExpressionTrialOutcome.Error;

    /// <summary>Whether the result is a value or a rule that holds.</summary>
    public bool IsFine => Outcome is ExpressionTrialOutcome.Value or ExpressionTrialOutcome.Passed;

    /// <summary>Whether the expression reads no field, so there is nothing to type.</summary>
    public bool HasNoInputs => _parses && Inputs.Count == 0;

    partial void OnOutcomeChanged(ExpressionTrialOutcome value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(IsProblem));
        OnPropertyChanged(nameof(IsFine));
    }

    /// <summary>The result in words; empty when <see cref="Outcome"/> is <see cref="ExpressionTrialOutcome.None"/>.</summary>
    [ObservableProperty]
    private string _result = string.Empty;

    /// <summary>
    /// Takes the expression as it now stands. The inputs follow the fields it reads, keeping what was typed for a
    /// field it still reads.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="parses">Whether it parses; one that does not is not evaluated.</param>
    /// <param name="message">The rule's message, shown when its condition fails.</param>
    public void Update(string expression, bool parses, string message)
    {
        _expression = expression;
        _parses = parses && !string.IsNullOrWhiteSpace(expression);
        _message = message;
        if (_parses) SyncInputs();
        OnPropertyChanged(nameof(HasNoInputs));
        Evaluate();
    }

    private void SyncInputs()
    {
        IReadOnlyList<string> referenced;
        try
        {
            referenced = _evaluator.GetReferencedVariables(_expression);
        }
        catch (ExpressionEvaluationException)
        {
            return;
        }
        var wanted = (_table.Fields ?? [])
            .Where(f => referenced.Contains(f.FieldName, StringComparer.Ordinal))
            .ToList();
        if (wanted.Select(f => f.FieldName).SequenceEqual(Inputs.Select(i => i.FieldName), StringComparer.Ordinal)) return;

        var typed = Inputs.ToDictionary(i => i.FieldName, i => i.Text, StringComparer.Ordinal);
        foreach (var input in Inputs) input.PropertyChanged -= OnInputChanged;
        Inputs.Clear();
        foreach (var field in wanted)
        {
            var type = ExpressionPolicy.CoerceValue(null, field.DbType).GetType();
            var input = new ExpressionTrialInput(field.FieldName, ExpressionMembers.TypeName(type), field.DbType)
            {
                Text = typed.GetValueOrDefault(field.FieldName) ?? string.Empty,
            };
            input.PropertyChanged += OnInputChanged;
            Inputs.Add(input);
        }
    }

    private void OnInputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExpressionTrialInput.Text)) Evaluate();
    }

    private void Evaluate()
    {
        if (!_parses || !ReadInputs(out var values))
        {
            Show(ExpressionTrialOutcome.None, string.Empty);
            return;
        }
        try
        {
            if (_field is null)
            {
                var holds = _calculator.EvaluateRuleSample(_expression, _table, values, _timeZoneId);
                Show(holds ? ExpressionTrialOutcome.Passed : ExpressionTrialOutcome.Failed, RuleResult(holds));
            }
            else
            {
                var value = _calculator.EvaluateFieldSample(_field, _propertyName, _expression, values, _timeZoneId);
                Show(ExpressionTrialOutcome.Value, value is null ? L("ExpressionTrial_Empty") : $"{Format(value)}  ({ExpressionMembers.TypeName(value.GetType())})");
            }
        }
        catch (Exception ex) when (ex is ExpressionEvaluationException or ArithmeticException or InvalidCastException
                                       or FormatException or ArgumentException or NullReferenceException)
        {
            // The expression parsed, so what fails here is the evaluation itself: a division by zero, a conversion
            // the sample values make impossible, a member called on an empty value.
            Show(ExpressionTrialOutcome.Error, ex.Message);
        }
    }

    // Reads every sample as the field's type, marking the ones that cannot be read. An empty box is an empty cell.
    private bool ReadInputs(out Dictionary<string, object?> values)
    {
        values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var readable = true;
        foreach (var input in Inputs)
        {
            object? text = input.Text.Length == 0 ? null : input.Text;
            try
            {
                ExpressionPolicy.CoerceValue(text, input.DbType);
                input.Error = string.Empty;
                values[input.FieldName] = text;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                input.Error = ex.Message;
                readable = false;
            }
        }
        return readable;
    }

    private string RuleResult(bool holds) => _propertyName switch
    {
        nameof(FormRule.When) => L(holds ? "ExpressionTrial_WhenApplies" : "ExpressionTrial_WhenSkips"),
        _ when holds => L("ExpressionTrial_ConditionPasses"),
        _ => string.Format(CultureInfo.CurrentCulture, L("ExpressionTrial_ConditionFails"), _message),
    };

    private void Show(ExpressionTrialOutcome outcome, string result)
    {
        Outcome = outcome;
        Result = result;
    }

    // Written the way an expression writes it, so the result reads the same whatever the editor's culture is.
    private static string Format(object value) => value switch
    {
        string text => $"\"{text}\"",
        bool flag => flag ? "true" : "false",
        DateTime date when date.TimeOfDay == TimeSpan.Zero => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string L(string key) => LocalizationService.Current[key];
}
