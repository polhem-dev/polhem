namespace Polhem.Web.Blazor.Fluent.Controls.Editors
{
    /// <summary>
    /// Adapts the parameters of the field editor bases to what Fluent UI's inputs take.
    /// </summary>
    internal static class FluentAttributes
    {
        // WARNING: a Fluent input throws on a null `Id`, which ends the circuit, so it is passed only when there is one.
        internal static Dictionary<string, object> From(string? id, IReadOnlyDictionary<string, object>? attributes)
        {
            var result = attributes?.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal) ?? [];
            if (id is not null) result["Id"] = id;
            return result;
        }

        internal static int? MaxLength(int maxLength) => maxLength > 0 ? maxLength : null;

        // Display format equals storage format in every UI head (ADR-033), and the date picker formats with its culture.
        internal static System.Globalization.CultureInfo CultureFor(string pattern)
        {
            var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
            culture.DateTimeFormat.ShortDatePattern = pattern;
            culture.DateTimeFormat.YearMonthPattern = pattern;
            return culture;
        }
    }
}
