namespace Polhem.DefineEditor.Services;

/// <summary>
/// What the XML view of a definition offers at the caret: the elements and attributes the definition type allows
/// there (<see cref="DefineXmlSchema"/>), and the values of an enum or boolean attribute.
/// </summary>
public static class XmlCompletion
{
    /// <summary>The suggestions at <paramref name="offset"/> in <paramref name="text"/>, a definition of <paramref name="rootType"/>.</summary>
    /// <param name="text">The whole XML text.</param>
    /// <param name="offset">The caret.</param>
    /// <param name="rootType">The type of the definition's root object.</param>
    /// <param name="sources">The document's own values and help, and how details are worded.</param>
    public static XmlSuggestions At(string text, int offset, Type rootType, XmlCompletionSources sources)
    {
        var context = XmlCaretContext.At(text, offset);
        var schema = DefineXmlSchema.For(rootType);
        var from = offset - context.Prefix.Length;

        return context.Kind switch
        {
            XmlCaretContextKind.ElementName => new XmlSuggestions(Elements(schema, context.Path), from),
            XmlCaretContextKind.EndTagName when context.Path.Count > 0 =>
                new XmlSuggestions([EndTag(context.Path[^1], close: true)], from),
            XmlCaretContextKind.AttributeName => new XmlSuggestions(Attributes(schema, context, sources), from),
            XmlCaretContextKind.AttributeValue => Values(schema, context, offset, sources),
            _ => XmlSuggestions.None(offset),
        };
    }

    private static List<XmlSuggestion> Attributes(DefineXmlSchema schema, XmlCaretContext context, XmlCompletionSources sources) =>
        schema.AttributesOf(context.Path)
            .Where(a => !context.PresentAttributes.Contains(a.Name, StringComparer.Ordinal))
            .Select(a =>
            {
                var help = sources.Help?.Invoke(context.Steps, a.Name);
                var detail = help is null ? sources.Describe(a) : sources.Describe(a) + "\n" + help;
                var offersValues = a.AllowedValues.Count > 0 || sources.Values?.Invoke(context.Steps, a.Name) is { Count: > 0 };
                return new XmlSuggestion(XmlSuggestionKind.Attribute, a.Name, a.Name + "=\"\"", detail, 1, offersValues);
            })
            .ToList();

    /// <summary>
    /// The end tag that a <c>&gt;</c> just typed at <paramref name="offset"/> calls for, or <c>null</c>: the one of the
    /// start tag it closed, when the text does not already go on with it.
    /// </summary>
    public static string? EndTagAfterClose(string text, int offset)
    {
        var context = XmlCaretContext.At(text, offset);
        if (context.JustClosedElement is not { } name) return null;
        var endTag = $"</{name}>";
        return string.CompareOrdinal(text, offset, endTag, 0, endTag.Length) == 0 ? null : endTag;
    }

    private static List<XmlSuggestion> Elements(DefineXmlSchema schema, IReadOnlyList<string> path)
    {
        if (path.Count == 0)
            return [new XmlSuggestion(XmlSuggestionKind.Element, schema.RootName, schema.RootName, string.Empty, 0, false)];

        var items = schema.ChildrenOf(path)
            .Select(c => new XmlSuggestion(XmlSuggestionKind.Element, c.Name, c.Name, string.Empty, 0, false))
            .ToList();
        items.Add(EndTag(path[^1], close: false));
        return items;
    }

    // The end tag offered after `<` carries its slash; after `</` the slash is already there.
    private static XmlSuggestion EndTag(string name, bool close) =>
        new(XmlSuggestionKind.EndTag, "/" + name, close ? name + ">" : "/" + name + ">", string.Empty, 0, false);

    private static XmlSuggestions Values(DefineXmlSchema schema, XmlCaretContext context, int offset, XmlCompletionSources sources)
    {
        var attribute = schema.AttributesOf(context.Path)
            .FirstOrDefault(a => string.Equals(a.Name, context.AttributeName, StringComparison.Ordinal));
        if (attribute is null) return XmlSuggestions.None(offset);

        if (attribute.AllowedValues.Count > 0)
        {
            // A flags enum is written as a space-separated list; only the word being typed is replaced.
            var prefix = attribute.IsList ? context.Prefix[(context.Prefix.LastIndexOf(' ') + 1)..] : context.Prefix;
            var members = attribute.AllowedValues
                .Select(v => new XmlSuggestion(XmlSuggestionKind.Value, v, v,
                    v == attribute.DefaultValue ? sources.DefaultMarker : string.Empty, 0, false))
                .ToList();
            return new XmlSuggestions(members, offset - prefix.Length);
        }

        var values = sources.Values?.Invoke(context.Steps, attribute.Name);
        if (values is not { Count: > 0 }) return XmlSuggestions.None(offset);
        var items = values.Distinct(StringComparer.Ordinal)
            .Select(v => new XmlSuggestion(XmlSuggestionKind.Value, v, v, string.Empty, 0, false))
            .ToList();
        return new XmlSuggestions(items, offset - context.Prefix.Length);
    }
}
