using System.Globalization;
using System.Resources;
using Polhem.Expressions;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Translates the summaries of the expression helper functions into the editor's language through the
/// <c>FunctionText</c> string resources.
/// </summary>
/// <remarks>
/// A summary is looked up as <c>FuncSummary_{name}</c>. The English text is <see cref="ExpressionFunction.Summary"/>
/// itself, so the neutral resource file holds no entries, and a function without a translation shows its English
/// summary.
/// </remarks>
public static class FunctionLabels
{
    private static readonly ResourceManager s_resources =
        new("Polhem.DefineEditor.Resources.FunctionText", typeof(FunctionLabels).Assembly);

    /// <summary>The resource key of the summary of <paramref name="function"/>.</summary>
    public static string SummaryKey(ExpressionFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return "FuncSummary_" + function.Name;
    }

    /// <summary>The summary of <paramref name="function"/> in the editor's language.</summary>
    public static string Summary(ExpressionFunction function) => Summary(function, LocalizationService.Current.Culture);

    /// <summary>The summary of <paramref name="function"/> in <paramref name="culture"/>, or the English one.</summary>
    public static string Summary(ExpressionFunction function, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return s_resources.GetString(SummaryKey(function), culture) ?? function.Summary;
    }

    /// <summary>
    /// How <paramref name="function"/> is called: its signature, then its example when it has one.
    /// </summary>
    public static string Usage(ExpressionFunction function)
    {
        var signature = ExpressionMembers.Signature(function);
        return function.Example.Length == 0
            ? signature
            : signature + Environment.NewLine + LocalizationService.Current["ExpressionEditor_Example"] + function.Example;
    }
}
