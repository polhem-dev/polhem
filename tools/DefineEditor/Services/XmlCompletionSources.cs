namespace Polhem.DefineEditor.Services;

/// <summary>What <see cref="XmlCompletion"/> asks of the document beyond the definition's schema.</summary>
/// <param name="Describe">Formats an attribute's details for the list: its type, whether it is required, its default.</param>
/// <param name="DefaultMarker">The detail of the value that is an attribute's default.</param>
/// <param name="Values">
/// The values of a free-text attribute of the element at the given steps, such as the solution's ProgIds; <c>null</c>
/// when there are none.
/// </param>
/// <param name="Help">What an attribute of the element at the given steps is for; <c>null</c> when nothing is said.</param>
public sealed record XmlCompletionSources(
    Func<DefineXmlAttributeInfo, string> Describe,
    string DefaultMarker,
    Func<IReadOnlyList<XmlElementStep>, string, IReadOnlyList<string>?>? Values = null,
    Func<IReadOnlyList<XmlElementStep>, string, string?>? Help = null);
