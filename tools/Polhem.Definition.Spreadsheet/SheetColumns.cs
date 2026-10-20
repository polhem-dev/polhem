using System.Globalization;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Factories for <see cref="SheetColumn{TRow}"/> by value kind. A value equal to the property's default is
/// written as an empty cell, so the person filling in the workbook only sees what differs.
/// </summary>
internal static class SheetColumns
{
    /// <summary>The name of the yes/no list on the Lists sheet.</summary>
    public const string YesNoList = "YesNo";

    private static readonly string[] s_yes = ["Y", "YES", "TRUE", "1", "是"];
    private static readonly string[] s_no = ["N", "NO", "FALSE", "0", "否"];

    public static SheetColumn<T> Text<T>(
        string key, string label, Func<T, string> get, Action<T, string> set,
        string hint = "", bool advanced = false, double width = 14, string? listName = null, bool mapsProperty = true)
        => new()
        {
            Key = key,
            Label = label,
            Hint = hint,
            Advanced = advanced,
            Width = width,
            ListName = listName,
            MapsProperty = mapsProperty,
            Export = row => get(row),
            Import = (row, text) =>
            {
                set(row, text);
                return null;
            },
        };

    public static SheetColumn<T> Int<T>(
        string key, string label, Func<T, int> get, Action<T, int> set,
        string hint = "", bool advanced = false, double width = 10)
        => new()
        {
            Key = key,
            Label = label,
            Hint = hint,
            Advanced = advanced,
            Width = width,
            Export = row => get(row) is var value && value != 0 ? value : string.Empty,
            Import = (row, text) =>
            {
                if (text.Length == 0)
                {
                    set(row, 0);
                    return null;
                }
                if (TryParseInt(text, out var value))
                {
                    set(row, value);
                    return null;
                }
                return $"'{text}' is not a whole number.";
            },
        };

    public static SheetColumn<T> Bool<T>(
        string key, string label, Func<T, bool> get, Action<T, bool> set, bool defaultValue,
        string hint = "", bool advanced = false, double width = 8)
        => new()
        {
            Key = key,
            Label = label,
            Hint = hint,
            Advanced = advanced,
            Width = width,
            ListName = YesNoList,
            Export = row => get(row) == defaultValue ? string.Empty : (get(row) ? "Y" : "N"),
            Import = (row, text) =>
            {
                if (text.Length == 0)
                    set(row, defaultValue);
                else if (s_yes.Contains(text, StringComparer.OrdinalIgnoreCase))
                    set(row, true);
                else if (s_no.Contains(text, StringComparer.OrdinalIgnoreCase))
                    set(row, false);
                else
                    return $"'{text}' is not Y or N.";
                return null;
            },
        };

    /// <summary>
    /// A column whose drop-down lists the names of <typeparamref name="TEnum"/>. With <c>alwaysWrite</c> the
    /// value is written even when it equals the default, for columns every row should show.
    /// </summary>
    public static SheetColumn<T> Enum<T, TEnum>(
        string key, string label, Func<T, TEnum> get, Action<T, TEnum> set, TEnum defaultValue,
        string hint = "", bool advanced = false, double width = 14, bool alwaysWrite = false)
        where TEnum : struct, System.Enum
        => new()
        {
            Key = key,
            Label = label,
            Hint = hint,
            Advanced = advanced,
            Width = width,
            ListName = typeof(TEnum).Name,
            Export = row => alwaysWrite || !EqualityComparer<TEnum>.Default.Equals(get(row), defaultValue)
                ? get(row).ToString()
                : string.Empty,
            Import = (row, text) =>
            {
                if (text.Length == 0)
                {
                    set(row, defaultValue);
                    return null;
                }
                // Rejects numeric text: `Enum.TryParse` accepts "7" for any enum, and a number in this
                // column is a typing mistake, not a choice.
                if (!TryParseInt(text, out _)
                    && System.Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
                    && System.Enum.IsDefined(value))
                {
                    set(row, value);
                    return null;
                }
                return $"'{text}' is not one of: {string.Join(", ", System.Enum.GetNames<TEnum>())}.";
            },
        };

    private static bool TryParseInt(string text, out int value)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        // Excel stores every number as a double, so a cell typed as 50 can come back as "50.0".
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)number;
            return true;
        }
        return false;
    }
}
