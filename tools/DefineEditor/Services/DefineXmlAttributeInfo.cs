namespace Polhem.DefineEditor.Services;

/// <summary>An attribute of a definition element.</summary>
/// <param name="Name">The attribute's name.</param>
/// <param name="TypeName">The name of its schema type: an enum's name, or a built-in type such as <c>string</c>.</param>
/// <param name="DefaultValue">The value it has when it is left out; <c>null</c> when it has none.</param>
/// <param name="IsRequired">Whether the serializer always writes it.</param>
/// <param name="AllowedValues">The values it may take, for an enum or a boolean; empty for free text.</param>
/// <param name="IsList">Whether the value is a space-separated list of <paramref name="AllowedValues"/>, as for a flags enum.</param>
public sealed record DefineXmlAttributeInfo(
    string Name,
    string TypeName,
    string? DefaultValue,
    bool IsRequired,
    IReadOnlyList<string> AllowedValues,
    bool IsList);
