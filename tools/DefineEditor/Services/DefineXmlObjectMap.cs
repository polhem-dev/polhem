using System.Collections;
using System.Reflection;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Follows an element path in a definition's XML back to what it is written from: the object in the define object
/// graph, or, for an element the object does not have yet, its type.
/// </summary>
public static class DefineXmlObjectMap
{
    /// <summary>
    /// The object the last element of <paramref name="steps"/> is written from, or <c>null</c> when
    /// <paramref name="root"/> has no such object: the text has an element the object does not have yet, say.
    /// </summary>
    /// <param name="root">The define object.</param>
    /// <param name="steps">The elements from the root element down.</param>
    public static object? ObjectAt(object root, IReadOnlyList<XmlElementStep> steps)
    {
        object? current = root;
        IEnumerable? wrapper = null;
        for (var i = 1; i < steps.Count; i++)
        {
            var step = steps[i];
            if (wrapper is not null)
            {
                current = wrapper.Cast<object?>().ElementAtOrDefault(step.Index);
                wrapper = null;
            }
            else if (Property(current!.GetType(), step.Name) is not { } property
                     || property.GetValue(current) is not { } value)
            {
                return null;
            }
            else if (!XmlMemberRules.IsCollection(value.GetType()))
            {
                current = value;
            }
            else if (XmlMemberRules.IsFlattened(property))
            {
                current = ((IEnumerable)value).Cast<object?>().ElementAtOrDefault(step.NameIndex);
            }
            else
            {
                wrapper = (IEnumerable)value;
            }

            if (current is null) return null;
        }
        return wrapper ?? current;
    }

    /// <summary>
    /// The type the last element of <paramref name="steps"/> is written from, read from the property declarations
    /// alone; <c>null</c> when the definition type has no such element.
    /// </summary>
    /// <param name="rootType">The type of the define object.</param>
    /// <param name="steps">The elements from the root element down.</param>
    public static Type? TypeAt(Type rootType, IReadOnlyList<XmlElementStep> steps)
    {
        var current = rootType;
        PropertyInfo? wrapper = null;
        for (var i = 1; i < steps.Count; i++)
        {
            var step = steps[i];
            if (wrapper is not null)
            {
                current = XmlMemberRules.ItemType(wrapper, step.Name);
                wrapper = null;
            }
            else if (Property(current, step.Name) is not { } property)
            {
                return null;
            }
            else if (!XmlMemberRules.IsCollection(property.PropertyType))
            {
                current = property.PropertyType;
            }
            else if (XmlMemberRules.IsFlattened(property))
            {
                current = XmlMemberRules.ItemType(property.PropertyType);
            }
            else
            {
                wrapper = property;
            }

            if (current is null) return null;
        }
        return wrapper is null ? current : wrapper.PropertyType;
    }

    private static PropertyInfo? Property(Type type, string elementName) =>
        XmlMemberRules.ElementProperties(type).FirstOrDefault(p => XmlMemberRules.ElementName(p) == elementName);
}
