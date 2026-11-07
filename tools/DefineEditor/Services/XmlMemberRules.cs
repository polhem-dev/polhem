using System.Collections;
using System.Reflection;
using System.Xml.Serialization;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The part of <c>XmlSerializer</c>'s mapping the XML view needs to go between an element and the object it is
/// written from: which properties become elements and attributes, under which names, and what a collection holds.
/// </summary>
/// <remarks>
/// The schema the completion lists from (<see cref="DefineXmlSchema"/>) is exported by the serializer itself; these
/// rules only follow an element path back to an object or a type, and cover the attributes the definition types use:
/// <see cref="XmlElementAttribute"/>, <see cref="XmlArrayAttribute"/>, <see cref="XmlArrayItemAttribute"/>,
/// <see cref="XmlAttributeAttribute"/> and <see cref="XmlIgnoreAttribute"/>.
/// </remarks>
internal static class XmlMemberRules
{
    /// <summary>
    /// The properties written as elements: public, readable, not ignored, not attributes or text, and settable unless
    /// they are collections, which the serializer fills through their getter.
    /// </summary>
    public static IEnumerable<PropertyInfo> ElementProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead
                        && p.GetIndexParameters().Length == 0
                        && !p.IsDefined(typeof(XmlIgnoreAttribute))
                        && !p.IsDefined(typeof(XmlAttributeAttribute))
                        && !p.IsDefined(typeof(XmlTextAttribute))
                        && (p.CanWrite || typeof(IEnumerable).IsAssignableFrom(p.PropertyType)));

    /// <summary>The property written as the attribute <paramref name="name"/>, or <c>null</c>.</summary>
    public static PropertyInfo? AttributeProperty(Type type, string name) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetCustomAttribute<XmlAttributeAttribute>() is { } attribute
                                 && !p.IsDefined(typeof(XmlIgnoreAttribute))
                                 && (string.IsNullOrEmpty(attribute.AttributeName) ? p.Name : attribute.AttributeName) == name);

    /// <summary>The name of the element a property is written as; for a wrapped collection, the wrapper's.</summary>
    public static string ElementName(PropertyInfo property)
    {
        var element = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName;
        var array = property.GetCustomAttribute<XmlArrayAttribute>()?.ElementName;
        return !string.IsNullOrEmpty(element) ? element
            : !string.IsNullOrEmpty(array) ? array
            : property.Name;
    }

    /// <summary>Whether a collection property writes its items directly under its owner, with no wrapper element.</summary>
    public static bool IsFlattened(PropertyInfo property) => property.IsDefined(typeof(XmlElementAttribute));

    /// <summary>Whether a property's type is a collection of elements.</summary>
    public static bool IsCollection(Type type) => !IsLeaf(type) && typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>
    /// The type of the item a wrapped collection writes as <paramref name="elementName"/>: the type its
    /// <see cref="XmlArrayItemAttribute"/> names for it, otherwise the collection's item type.
    /// </summary>
    public static Type? ItemType(PropertyInfo collection, string elementName)
    {
        foreach (var item in collection.GetCustomAttributes<XmlArrayItemAttribute>())
        {
            var name = !string.IsNullOrEmpty(item.ElementName) ? item.ElementName : item.Type?.Name;
            if (name == elementName && item.Type is not null) return item.Type;
        }
        return ItemType(collection.PropertyType);
    }

    /// <summary>The item type of a collection type, from its <see cref="IEnumerable{T}"/>.</summary>
    public static Type? ItemType(Type collection) =>
        collection.GetInterfaces().Append(collection)
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();

    /// <summary>Whether a type is written as a value rather than as an element with members.</summary>
    public static bool IsLeaf(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum
            || underlying == typeof(string) || underlying == typeof(decimal) || underlying == typeof(byte[])
            || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(TimeSpan)
            || underlying == typeof(DateOnly) || underlying == typeof(TimeOnly) || underlying == typeof(Guid);
    }
}
