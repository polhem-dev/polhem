using Polhem.Definition.Collections;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Writes a small collection into one cell as <c>a=b; c=d</c> and reads it back. Used for field mappings and
/// list items, which a person reads more easily inline than on a sheet of their own. A lookup-only field mapping
/// carries a trailing <c>(lookup)</c>.
/// </summary>
internal static class CellLists
{
    private const char ItemSeparator = ';';
    private const char PairSeparator = '=';
    private const string LookupOnlyMarker = "(lookup)";

    public static string FormatMappings(FieldMappingCollection? mappings)
    {
        if (mappings is not { Count: > 0 }) return string.Empty;
        return string.Join("; ", mappings.Select(m =>
        {
            var pair = $"{Checked(m.SourceField, "SourceField")}{PairSeparator}{Checked(m.DestinationField, "DestinationField")}";
            return m.LookupOnly ? $"{pair} {LookupOnlyMarker}" : pair;
        }));
    }

    public static string? ParseMappings(string text, FieldMappingCollection mappings)
    {
        mappings.Clear();
        foreach (var item in Split(text))
        {
            var lookupOnly = item.EndsWith(LookupOnlyMarker, StringComparison.OrdinalIgnoreCase);
            var pair = lookupOnly ? item[..^LookupOnlyMarker.Length].TrimEnd() : item;
            int at = pair.IndexOf(PairSeparator);
            if (at <= 0 || at == pair.Length - 1)
                return $"'{item}' must be written as source=destination, for example sys_id=ref_customer_id, " +
                       $"optionally followed by {LookupOnlyMarker}.";
            mappings.Add(new FieldMapping(pair[..at].Trim(), pair[(at + 1)..].Trim()) { LookupOnly = lookupOnly });
        }
        return null;
    }

    /// <summary>
    /// Writes each item as <c>value=text</c>, or just <c>value</c> when the text is the same.
    /// </summary>
    public static string FormatListItems(ListItemCollection? items)
    {
        if (items is not { Count: > 0 }) return string.Empty;
        return string.Join("; ", items.Select(i =>
        {
            var value = Checked(i.Value, "ListItem value");
            var text = i.Text.Contains(ItemSeparator, StringComparison.Ordinal)
                ? throw Unrepresentable(i.Text, "ListItem text")
                : i.Text;
            return string.Equals(value, text, StringComparison.Ordinal) ? value : $"{value}{PairSeparator}{text}";
        }));
    }

    public static string? ParseListItems(string text, ListItemCollection items)
    {
        items.Clear();
        foreach (var item in Split(text))
        {
            int at = item.IndexOf(PairSeparator);
            var value = at < 0 ? item : item[..at].Trim();
            var display = at < 0 ? item : item[(at + 1)..].Trim();
            if (value.Length == 0)
                return $"'{item}' has no value before '='.";
            if (items.Contains(value))
                return $"The value '{value}' appears more than once.";
            items.Add(new ListItem(value, display));
        }
        return null;
    }

    private static IEnumerable<string> Split(string text)
        => text.Split(ItemSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Checked(string value, string what)
        => value.Contains(ItemSeparator, StringComparison.Ordinal) || value.Contains(PairSeparator, StringComparison.Ordinal)
            ? throw Unrepresentable(value, what)
            : value;

    private static InvalidOperationException Unrepresentable(string value, string what)
        => new($"The {what} '{value}' contains '{ItemSeparator}' or '{PairSeparator}', which a workbook cell cannot hold in this format.");
}
