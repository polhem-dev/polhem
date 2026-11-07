namespace Polhem.DefineEditor.Services;

/// <summary>What <see cref="DefineXmlParser.Parse"/> read: the define object, or the problems that kept it from reading one.</summary>
/// <param name="Value">The define object; <c>null</c> when <paramref name="Issues"/> is not empty.</param>
/// <param name="Issues">The problems found, in the order they were met.</param>
public sealed record DefineXmlParseResult(object? Value, IReadOnlyList<XmlParseIssue> Issues)
{
    /// <summary>Whether the XML was read without a problem.</summary>
    public bool Succeeded => Value is not null && Issues.Count == 0;
}
