using System.Globalization;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Turns a cell value as MiniExcel returns it into the trimmed text the columns parse.
/// </summary>
internal static class CellText
{
    public static string From(object? value) => value switch
    {
        null => string.Empty,
        string text => text.Trim(),
        bool flag => flag ? "Y" : "N",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        DateTime date => date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
        _ => value.ToString()?.Trim() ?? string.Empty,
    };
}
