using Polhem.DefineEditor.Services;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// What an expression over one table can name, as an editor lists it: the table's fields, the helper functions and the
/// types, as headings of a symbol tree, and the same names for completion.
/// </summary>
public sealed class ExpressionSymbolSet
{
    /// <param name="table">The table whose fields are the expression's variables.</param>
    /// <param name="evaluator">The evaluator whose type names are listed.</param>
    public ExpressionSymbolSet(FormTable table, DynamicExpressoEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(evaluator);
        var variableTypes = FormExpressionCalculator.GetVariableTypes(table);
        Fields = (table.Fields ?? [])
            .Where(f => variableTypes.ContainsKey(f.FieldName))
            .Select(f => new ExpressionSymbol(f.FieldName, f.FieldName,
                $"{ExpressionMembers.TypeName(variableTypes[f.FieldName])} · {f.Caption}"))
            .ToList();
        Functions = DynamicExpressoEvaluator.HelperFunctions
            .Select(f => new ExpressionSymbol(f.Name, f.Name + "(" + (f.Parameters.Count > 0 ? "" : ")"),
                FunctionLabels.Summary(f), FunctionLabels.Usage(f)))
            .ToList();
        var referencedTypes = evaluator.ReferencedTypes;
        Types = ExpressionMembers.FeaturedTypes(referencedTypes)
            .Select(t => new ExpressionSymbolGroup(t.Key, ExpressionMembers.Of(t.Value, isStatic: true, t.Key + ".")
                .Select(m => new ExpressionSymbol(m.Label, m.InsertText, m.Detail))
                .ToList()))
            .ToList();
        Groups =
        [
            new ExpressionSymbolGroup(LocalizationService.Current["ExpressionEditor_Fields"], Fields, IsExpanded: true),
            new ExpressionSymbolGroup(LocalizationService.Current["ExpressionEditor_Functions"], Functions, IsExpanded: true),
            new ExpressionSymbolGroup(LocalizationService.Current["ExpressionEditor_Types"], Types, IsExpanded: true),
        ];
        var listValues = (table.Fields ?? [])
            .Where(f => f.ListItems is { Count: > 0 })
            .ToDictionary(f => f.FieldName, f => (IReadOnlyList<string>)f.ListItems!.Select(i => i.Value).ToList(),
                StringComparer.Ordinal);
        CompletionSources = new ExpressionCompletionSources(variableTypes, DynamicExpressoEvaluator.HelperFunctions,
            referencedTypes, name => listValues.GetValueOrDefault(name));
    }

    /// <summary>The fields of the table, which the expression names as variables.</summary>
    public IReadOnlyList<ExpressionSymbol> Fields { get; }

    /// <summary>The helper functions every expression can call.</summary>
    public IReadOnlyList<ExpressionSymbol> Functions { get; }

    /// <summary>The types an expression can name, each with its static members.</summary>
    public IReadOnlyList<ExpressionSymbolGroup> Types { get; }

    /// <summary>The fields, the functions and the types, as the headings of the symbol tree.</summary>
    public IReadOnlyList<ExpressionSymbolGroup> Groups { get; }

    /// <summary>What the completion offers names from.</summary>
    public ExpressionCompletionSources CompletionSources { get; }
}
