using System.Globalization;
using Avalonia.Data.Converters;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.Converters;

/// <summary>
/// Shows a <see cref="RelationSetupRowStatus"/> in the relation dialog as the editor's text for it, from the
/// <c>Relation_Status_&lt;member&gt;</c> string resources; <see cref="RelationSetupRowStatus.None"/> shows nothing.
/// </summary>
public sealed class RelationSetupStatusConverter : IValueConverter
{
    public static RelationSetupStatusConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RelationSetupRowStatus status and not RelationSetupRowStatus.None
            ? LocalizationService.Current[$"Relation_Status_{status}"]
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
