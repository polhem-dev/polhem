using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// What a definition file may contain, element by element: the child elements and the attributes of each, with their
/// types, defaults and allowed values. The XML view's completion reads it.
/// </summary>
/// <remarks>
/// It is the XML schema <see cref="XmlSchemaExporter"/> writes for the root type, from the same
/// <see cref="XmlReflectionImporter"/> mapping <c>XmlSerializer</c> reads and writes the file with. So nothing is
/// suggested that the serializer would not read, and a change to a definition type reaches the completion without
/// a second description of the format to keep in step.
/// </remarks>
public sealed class DefineXmlSchema
{
    private static readonly ConcurrentDictionary<Type, DefineXmlSchema> s_cache = new();

    private readonly XmlSchemaElement _root;

    private DefineXmlSchema(XmlSchemaElement root) => _root = root;

    /// <summary>The schema of the definition files whose root object is a <paramref name="rootType"/>.</summary>
    public static DefineXmlSchema For(Type rootType) => s_cache.GetOrAdd(rootType, Build);

    /// <summary>The name of the root element.</summary>
    public string RootName => _root.Name ?? string.Empty;

    /// <summary>
    /// The element at <paramref name="path"/>, the names of the elements from the root down; <c>null</c> when the
    /// schema has no such element there.
    /// </summary>
    public XmlSchemaElement? Resolve(IReadOnlyList<string> path)
    {
        if (path.Count == 0 || path[0] != RootName) return null;
        var element = _root;
        for (var i = 1; i < path.Count; i++)
        {
            element = ChildElements(element).FirstOrDefault(c => c.Name == path[i]);
            if (element is null) return null;
        }
        return element;
    }

    /// <summary>The elements the element at <paramref name="path"/> may contain, in schema order.</summary>
    public IReadOnlyList<DefineXmlElementInfo> ChildrenOf(IReadOnlyList<string> path) =>
        Resolve(path) is { } element
            ? ChildElements(element)
                .Select(c => new DefineXmlElementInfo(c.Name ?? string.Empty, c.MaxOccurs > 1))
                .DistinctBy(c => c.Name)
                .ToList()
            : [];

    /// <summary>The attributes of the element at <paramref name="path"/>, in schema order.</summary>
    public IReadOnlyList<DefineXmlAttributeInfo> AttributesOf(IReadOnlyList<string> path)
    {
        if (Resolve(path)?.ElementSchemaType is not XmlSchemaComplexType type) return [];

        return type.AttributeUses.Values.Cast<XmlSchemaAttribute>()
            .Select(a => new DefineXmlAttributeInfo(
                a.Name ?? string.Empty,
                TypeName(a.AttributeSchemaType),
                a.DefaultValue,
                a.Use == XmlSchemaUse.Required,
                AllowedValues(a.AttributeSchemaType),
                IsList(a.AttributeSchemaType)))
            .OrderBy(a => Order(type, a.Name))
            .ToList();
    }

    private static DefineXmlSchema Build(Type rootType)
    {
        var schemas = new XmlSchemas();
        new XmlSchemaExporter(schemas).ExportTypeMapping(new XmlReflectionImporter().ImportTypeMapping(rootType));

        var set = new XmlSchemaSet { XmlResolver = null };
        foreach (XmlSchema schema in schemas)
            set.Add(schema);
        set.Compile();

        var root = set.GlobalElements.Values.Cast<XmlSchemaElement>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No root element was exported for {rootType.Name}.");
        return new DefineXmlSchema(root);
    }

    private static IEnumerable<XmlSchemaElement> ChildElements(XmlSchemaElement element) =>
        element.ElementSchemaType is XmlSchemaComplexType type ? Elements(type.ContentTypeParticle) : [];

    private static IEnumerable<XmlSchemaElement> Elements(XmlSchemaParticle? particle) => particle switch
    {
        XmlSchemaElement element => [element],
        XmlSchemaGroupBase group => group.Items.OfType<XmlSchemaParticle>().SelectMany(Elements),
        _ => [],
    };

    // The compiled attribute table is a hash table; the order the type declares them in, its own before the
    // inherited ones, is what a reader expects from the file.
    private static int Order(XmlSchemaComplexType type, string name)
    {
        var index = 0;
        for (XmlSchemaType? current = type; current is XmlSchemaComplexType complex; current = current.BaseXmlSchemaType)
        {
            foreach (var attribute in DeclaredAttributes(complex))
            {
                if (attribute.Name == name) return index;
                index++;
            }
        }
        return int.MaxValue;
    }

    private static IEnumerable<XmlSchemaAttribute> DeclaredAttributes(XmlSchemaComplexType type)
    {
        var attributes = type.ContentModel?.Content switch
        {
            XmlSchemaComplexContentExtension extension => extension.Attributes,
            XmlSchemaComplexContentRestriction restriction => restriction.Attributes,
            _ => type.Attributes,
        };
        return attributes.OfType<XmlSchemaAttribute>();
    }

    private static string TypeName(XmlSchemaSimpleType? type) =>
        type is null ? string.Empty
        : !type.QualifiedName.IsEmpty ? type.QualifiedName.Name
        : type.Datatype?.TypeCode.ToString() ?? string.Empty;

    private static bool IsList(XmlSchemaSimpleType? type) => type?.Content is XmlSchemaSimpleTypeList;

    private static IReadOnlyList<string> AllowedValues(XmlSchemaSimpleType? type)
    {
        if (type is null) return [];
        if (type.Datatype?.TypeCode == XmlTypeCode.Boolean) return ["true", "false"];

        var restriction = type.Content switch
        {
            XmlSchemaSimpleTypeRestriction r => r,
            XmlSchemaSimpleTypeList { BaseItemType.Content: XmlSchemaSimpleTypeRestriction r } => r,
            _ => null,
        };
        return restriction?.Facets.OfType<XmlSchemaEnumerationFacet>()
            .Select(f => f.Value ?? string.Empty)
            .ToList() ?? [];
    }
}
