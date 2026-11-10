using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Core.Expressions;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Edits one expression of a form schema (a rule's When or Condition, a field's ValueExpression or
/// DefaultValueExpression), checking it as it is typed against the variables it is evaluated with.
/// </summary>
/// <remarks>
/// The variables and the result type are the ones <see cref="FormExpressionCalculator.ValidateExpressions"/> parses the
/// expression with, so the dialog and the validation panel agree on what is an error.
/// </remarks>
public sealed partial class ExpressionEditorViewModel : ObservableObject
{
    private readonly IExpressionEvaluator _evaluator;
    private readonly IReadOnlyDictionary<string, Type> _variableTypes;
    private readonly Type _returnType;

    /// <param name="title">The window title: what is being edited.</param>
    /// <param name="table">The table whose fields are the expression's variables.</param>
    /// <param name="returnType">The type the expression must produce: <see cref="bool"/> for a rule.</param>
    /// <param name="expression">The current expression.</param>
    /// <param name="message">The rule's message, edited beside its condition; <c>null</c> when there is none to edit.</param>
    /// <param name="evaluator">The evaluator that parses the expression.</param>
    public ExpressionEditorViewModel(string title, FormTable table, Type returnType, string expression, string? message,
        DynamicExpressoEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(table);
        Title = title;
        _returnType = returnType;
        _evaluator = evaluator;
        _variableTypes = FormExpressionCalculator.GetVariableTypes(table);
        HasMessage = message is not null;
        _message = message ?? string.Empty;
        Fields = (table.Fields ?? [])
            .Where(f => _variableTypes.ContainsKey(f.FieldName))
            .Select(f => new ExpressionSymbol(f.FieldName, f.FieldName,
                $"{ExpressionMembers.TypeName(_variableTypes[f.FieldName])} · {f.Caption}"))
            .ToList();
        Functions = DynamicExpressoEvaluator.HelperFunctions
            .Select(f => new ExpressionSymbol(f.Name, f.Name + "(" + (f.Parameters.Count > 0 ? "" : ")"),
                ExpressionMembers.Signature(f)))
            .ToList();
        var referencedTypes = evaluator.ReferencedTypes;
        Types = ExpressionMembers.FeaturedTypes(referencedTypes)
            .Select(t => new ExpressionSymbolGroup(t.Key, ExpressionMembers.Of(t.Value, isStatic: true, t.Key + ".")
                .Select(m => new ExpressionSymbol(m.Label, m.InsertText, m.Detail))
                .ToList()))
            .ToList();
        SymbolGroups =
        [
            new ExpressionSymbolGroup(LocalizationService.Current["ExpressionEditor_Fields"], Fields, IsExpanded: true),
            new ExpressionSymbolGroup(LocalizationService.Current["ExpressionEditor_Functions"], Functions, IsExpanded: true),
            new ExpressionSymbolGroup(LocalizationService.Current["ExpressionEditor_Types"], Types, IsExpanded: true),
        ];
        var listValues = (table.Fields ?? [])
            .Where(f => f.ListItems is { Count: > 0 })
            .ToDictionary(f => f.FieldName, f => (IReadOnlyList<string>)f.ListItems!.Select(i => i.Value).ToList(),
                StringComparer.Ordinal);
        CompletionSources = new ExpressionCompletionSources(_variableTypes, DynamicExpressoEvaluator.HelperFunctions,
            referencedTypes, name => listValues.GetValueOrDefault(name));
        _expression = expression;
        Validate();
    }

    /// <summary>The window title: what is being edited.</summary>
    public string Title { get; }

    /// <summary>The fields of the table, which the expression names as variables.</summary>
    public IReadOnlyList<ExpressionSymbol> Fields { get; }

    /// <summary>The helper functions every expression can call.</summary>
    public IReadOnlyList<ExpressionSymbol> Functions { get; }

    /// <summary>The types an expression can name, each with its static members.</summary>
    public IReadOnlyList<ExpressionSymbolGroup> Types { get; }

    /// <summary>What the completion offers names from.</summary>
    public ExpressionCompletionSources CompletionSources { get; }

    /// <summary>The fields, the functions and the types, as the headings of the symbol tree.</summary>
    public IReadOnlyList<ExpressionSymbolGroup> SymbolGroups { get; }

    /// <summary>Whether the rule's message is edited beside the expression.</summary>
    public bool HasMessage { get; }

    /// <summary>The expression being edited.</summary>
    [ObservableProperty]
    private string _expression;

    /// <summary>The rule's message; empty when <see cref="HasMessage"/> is <c>false</c>.</summary>
    [ObservableProperty]
    private string _message;

    /// <summary>Why the expression does not parse; empty when it does.</summary>
    [ObservableProperty]
    private string _error = string.Empty;

    /// <summary>The offset in <see cref="Expression"/> where parsing failed, when the parser reports one.</summary>
    [ObservableProperty]
    private int? _errorPosition;

    /// <summary>Whether <see cref="Error"/> has a message to show.</summary>
    public bool HasError => Error.Length > 0;

    partial void OnExpressionChanged(string value) => Validate();

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    // An empty expression is valid: an empty When means the rule always applies, and an empty field expression means
    // the field has none.
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Expression))
        {
            ErrorPosition = null;
            Error = string.Empty;
            return;
        }
        try
        {
            _evaluator.Validate(Expression, _variableTypes, _returnType);
            ErrorPosition = null;
            Error = string.Empty;
        }
        catch (ExpressionEvaluationException ex)
        {
            ErrorPosition = ex.Position;
            Error = ex.Message;
        }
    }
}
