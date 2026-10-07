using System.Globalization;
using System.Resources;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Translates the property grid's texts into the editor's language through the <c>PropertyText</c> string resources.
/// </summary>
/// <remarks>
/// <para>
/// A category is looked up as <c>PropCategory_{category}</c>, and a description as <c>PropDesc_{type}_{property}</c>,
/// trying the type of the object shown and then its base types, so a property declared on a base type is translated
/// once. Property names stay as written: they are the attribute names of the define files. An item label goes through
/// <see cref="TreeLabels"/>, like the tree's.
/// </para>
/// <para>
/// The English text is the annotation itself, so the neutral resource file holds no entries, and a text without a
/// translation is shown as written.
/// </para>
/// </remarks>
public static class PropertyLabels
{
    private static readonly ResourceManager s_resources =
        new("Polhem.DefineEditor.Resources.PropertyText", typeof(PropertyLabels).Assembly);

    /// <summary>
    /// Returns the translation of <paramref name="text"/> in the editor's language, or <c>null</c> to show it as written.
    /// </summary>
    public static string? Translate(PropertyGridText text) => Translate(text, LocalizationService.Current.Culture);

    /// <summary>
    /// Returns the translation of <paramref name="text"/> in <paramref name="culture"/>, or <c>null</c> to show it as
    /// written. An item label always follows the editor's language, as the tree's labels do.
    /// </summary>
    public static string? Translate(PropertyGridText text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        return text.Kind switch
        {
            PropertyGridTextKind.Category => Lookup("PropCategory_" + text.Text, culture),
            PropertyGridTextKind.Description when text.PropertyName is { } property => LookupDescription(text.ComponentType, property, culture),
            PropertyGridTextKind.ItemLabel => TreeLabels.Translate(text.Text),
            _ => null,
        };
    }

    private static string? LookupDescription(Type componentType, string property, CultureInfo culture)
    {
        for (var type = componentType; type is not null && type != typeof(object); type = type.BaseType)
        {
            if (Lookup($"PropDesc_{type.Name}_{property}", culture) is { } translated)
                return translated;
        }
        return null;
    }

    private static string? Lookup(string key, CultureInfo culture) =>
        s_resources.GetString(key, culture) is { Length: > 0 } text ? text : null;
}
