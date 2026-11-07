using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Finds where an object of a definition is written in the XML that <c>XmlSerializer</c> produced for the whole
/// definition, so the XML view can put the caret on the node the tree had selected.
/// </summary>
/// <remarks>
/// The object is found by reference in the definition's object graph, and the path to it is replayed on the XML: a
/// property becomes the element of its name (<see cref="XmlElementAttribute"/> or <see cref="XmlArrayAttribute"/>
/// when it renames it), a collection item becomes the child element at its index. Where a step finds no element
/// (an empty collection that is not written, say), the deepest element reached is returned instead.
/// </remarks>
public static class XmlElementLocator
{
    // Definition objects nest a handful of levels; the limit only guards against a graph this walk was not meant for.
    private const int MaxDepth = 16;

    /// <summary>
    /// Returns the position of the start tag of <paramref name="target"/>'s element in <paramref name="xml"/>, or
    /// <c>null</c> when <paramref name="target"/> is not reachable from <paramref name="root"/> or the XML cannot be
    /// read.
    /// </summary>
    /// <param name="xml">The XML of <paramref name="root"/>, as <c>XmlSerializer</c> wrote it.</param>
    /// <param name="root">The definition object <paramref name="xml"/> was written from.</param>
    /// <param name="target">The object, or collection, to find.</param>
    public static XmlTextPosition? Find(string xml, object root, object target)
    {
        var steps = new List<Step>();
        if (!ReferenceEquals(root, target)
            && !Search(root, target, steps, new HashSet<object>(ReferenceEqualityComparer.Instance), 0))
            return null;

        var document = Load(xml);
        if (document?.Root is not { } element)
            return null;

        foreach (var step in steps)
        {
            if (step.Apply(element) is not { } next) break;
            element = next;
        }

        IXmlLineInfo info = element;
        return info.HasLineInfo()
            ? new XmlTextPosition(info.LineNumber, info.LinePosition, element.Name.LocalName.Length)
            : null;
    }

    private static XDocument? Load(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static bool Search(object current, object target, List<Step> path, HashSet<object> visited, int depth)
    {
        if (depth > MaxDepth || !visited.Add(current)) return false;

        foreach (var property in SerializedProperties(current.GetType()))
        {
            if (property.GetValue(current) is not { } value || IsLeaf(value.GetType())) continue;

            var flattened = property.IsDefined(typeof(XmlElementAttribute));
            var name = ElementName(property);
            if (value is IEnumerable items)
            {
                if (ReferenceEquals(value, target))
                {
                    path.Add(new Step(name, flattened, -1));
                    return true;
                }

                var index = 0;
                foreach (var item in items)
                {
                    if (item is not null && !IsLeaf(item.GetType()))
                    {
                        path.Add(new Step(name, flattened, index));
                        if (ReferenceEquals(item, target) || Search(item, target, path, visited, depth + 1))
                            return true;
                        path.RemoveAt(path.Count - 1);
                    }
                    index++;
                }
                continue;
            }

            path.Add(new Step(name, false, -1));
            if (ReferenceEquals(value, target) || Search(value, target, path, visited, depth + 1))
                return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    // The properties XmlSerializer writes as elements: public, readable, not ignored, not attributes or text, and
    // settable unless they are collections (which it fills through their getter).
    private static IEnumerable<PropertyInfo> SerializedProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead
                        && p.GetIndexParameters().Length == 0
                        && !p.IsDefined(typeof(XmlIgnoreAttribute))
                        && !p.IsDefined(typeof(XmlAttributeAttribute))
                        && !p.IsDefined(typeof(XmlTextAttribute))
                        && (p.CanWrite || typeof(IEnumerable).IsAssignableFrom(p.PropertyType)));

    private static string ElementName(PropertyInfo property)
    {
        var element = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName;
        var array = property.GetCustomAttribute<XmlArrayAttribute>()?.ElementName;
        return !string.IsNullOrEmpty(element) ? element
            : !string.IsNullOrEmpty(array) ? array
            : property.Name;
    }

    private static bool IsLeaf(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum
            || underlying == typeof(string) || underlying == typeof(decimal) || underlying == typeof(byte[])
            || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(TimeSpan)
            || underlying == typeof(DateOnly) || underlying == typeof(TimeOnly) || underlying == typeof(Guid);
    }

    // One step down the XML: the element of a property, or the item at Index of a collection. A flattened collection
    // ([XmlElement]) writes its items directly under the owner; any other collection wraps them in an element of its own.
    private sealed record Step(string Name, bool Flattened, int Index)
    {
        public XElement? Apply(XElement parent)
        {
            if (Flattened)
                return parent.Elements().Where(e => e.Name.LocalName == Name).ElementAtOrDefault(Math.Max(Index, 0));

            var element = parent.Elements().FirstOrDefault(e => e.Name.LocalName == Name);
            return Index < 0 ? element : element?.Elements().ElementAtOrDefault(Index);
        }
    }
}

/// <summary>A position in an XML text: the 1-based line and column of an element's name, and the name's length.</summary>
/// <param name="Line">The 1-based line.</param>
/// <param name="Column">The 1-based column of the element's name, after its <c>&lt;</c>.</param>
/// <param name="Length">The length of the element's name.</param>
public sealed record XmlTextPosition(int Line, int Column, int Length);
