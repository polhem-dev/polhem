namespace Polhem.DefineEditor.Services;

/// <summary>The kinds of problem <see cref="DefineXmlParser"/> reports.</summary>
public enum XmlParseIssueKind
{
    /// <summary>The XML is not well formed, or a value cannot be read as its property's type.</summary>
    Syntax,

    /// <summary>An element the definition type does not have.</summary>
    UnknownElement,

    /// <summary>An attribute the definition type does not have.</summary>
    UnknownAttribute,

    /// <summary>There is no XML at all.</summary>
    Empty,
}
