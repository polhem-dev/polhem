using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Reads the XML typed in a document's XML view back into a define object, and reports where it is wrong.
/// </summary>
/// <remarks>
/// <c>XmlSerializer</c> on its own ignores an element or attribute it does not know, so a misspelt attribute would
/// silently disappear from the object, and from the file at the next save from the tree. Both are reported here as
/// problems, and the XML counts as wrong until they are fixed.
/// </remarks>
public static class DefineXmlParser
{
    private static readonly ConcurrentDictionary<Type, XmlSerializer> s_serializers = new();

    private static readonly Regex s_position = new(@"\((\d+), (\d+)\)", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>Reads <paramref name="xml"/> as a <paramref name="rootType"/>.</summary>
    /// <param name="xml">The XML of the whole definition file.</param>
    /// <param name="rootType">The type of the definition's root object.</param>
    public static DefineXmlParseResult Parse(string xml, Type rootType)
    {
        var issues = new List<XmlParseIssue>();
        var events = new XmlDeserializationEvents
        {
            OnUnknownElement = (_, e) => issues.Add(new XmlParseIssue(
                XmlParseIssueKind.UnknownElement, e.LineNumber, e.LinePosition, e.Element.Name)),
            OnUnknownAttribute = (_, e) => issues.Add(new XmlParseIssue(
                XmlParseIssueKind.UnknownAttribute, e.LineNumber, e.LinePosition, e.Attr.Name)),
        };

        object? value = null;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            value = s_serializers.GetOrAdd(rootType, t => new XmlSerializer(t)).Deserialize(reader, events);
        }
        catch (InvalidOperationException ex)
        {
            // XmlSerializer wraps every failure, malformed XML and a value of the wrong type alike, in an
            // InvalidOperationException whose message carries the position; the inner exception says what is wrong.
            issues.Add(FromException(ex));
        }
        catch (XmlException ex)
        {
            issues.Add(new XmlParseIssue(XmlParseIssueKind.Syntax, ex.LineNumber, ex.LinePosition, ex.Message));
        }

        if (value is null && issues.Count == 0)
            issues.Add(new XmlParseIssue(XmlParseIssueKind.Empty, 1, 1, string.Empty));
        return new DefineXmlParseResult(issues.Count == 0 ? value : null, issues.Select(i => AtName(i, xml)).ToList());
    }

    // XmlSerializer reports an unknown attribute at its value; the name, which is what is wrong, is the last
    // occurrence of it before that on the same line.
    private static XmlParseIssue AtName(XmlParseIssue issue, string xml)
    {
        if (issue.Kind != XmlParseIssueKind.UnknownAttribute || issue.Line < 1) return issue;

        var lines = xml.Split('\n');
        if (issue.Line > lines.Length) return issue;
        var line = lines[issue.Line - 1];
        var before = Math.Clamp(issue.Column - 1, 0, line.Length);
        var index = line.LastIndexOf(issue.Detail + "=", before, before, StringComparison.Ordinal);
        if (index < 0)
            index = line.LastIndexOf(issue.Detail, before, before, StringComparison.Ordinal);
        return index < 0 ? issue : issue with { Column = index + 1 };
    }

    private static XmlParseIssue FromException(InvalidOperationException ex)
    {
        var inner = ex.InnerException;
        while (inner?.InnerException is not null) inner = inner.InnerException;

        var (line, column) = inner is XmlException xml && xml.LineNumber > 0
            ? (xml.LineNumber, xml.LinePosition)
            : Position(ex.Message);
        return new XmlParseIssue(XmlParseIssueKind.Syntax, line, column, (inner ?? ex).Message);
    }

    private static (int Line, int Column) Position(string message)
    {
        var match = s_position.Match(message);
        return match.Success
            ? (int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
               int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture))
            : (1, 1);
    }
}

/// <summary>What <see cref="DefineXmlParser.Parse"/> read: the define object, or the problems that kept it from reading one.</summary>
/// <param name="Value">The define object; <c>null</c> when <paramref name="Issues"/> is not empty.</param>
/// <param name="Issues">The problems found, in the order they were met.</param>
public sealed record DefineXmlParseResult(object? Value, IReadOnlyList<XmlParseIssue> Issues)
{
    /// <summary>Whether the XML was read without a problem.</summary>
    public bool Succeeded => Value is not null && Issues.Count == 0;
}
