using System.Text.RegularExpressions;

namespace Polhem.DefineEditor.Services;

/// <summary>Reads an XML text up to the caret for <see cref="XmlCaretContext.At"/>.</summary>
internal sealed class XmlCaretScanner(string text, int caret)
{
    private readonly List<XmlElementStep> _open = [];

    // How many child elements, and how many of each name, each open element has had so far; the first entry counts
    // the top level.
    private readonly List<ChildCount> _counts = [new()];
    private int _index;

    public XmlCaretContext Scan()
    {
        string? justClosed = null;
        while (_index < caret)
        {
            if (text[_index] != '<')
            {
                _index++;
                justClosed = null;
                continue;
            }

            if (SkipDelimited("<!--", "-->") is { } comment)
            {
                if (!comment) return Content();
                justClosed = null;
                continue;
            }
            if (SkipDelimited("<![CDATA[", "]]>") is { } cdata)
            {
                if (!cdata) return Content();
                justClosed = null;
                continue;
            }
            if (SkipDelimited("<?", "?>") is { } instruction)
            {
                if (!instruction) return Content();
                justClosed = null;
                continue;
            }

            if (Starts("</"))
            {
                var context = ScanEndTag();
                if (context is not null) return context;
                justClosed = null;
                continue;
            }

            var result = ScanStartTag(out justClosed);
            if (result is not null) return result;
        }
        return Context(XmlCaretContextKind.Content, _open, string.Empty, null, [], justClosed);
    }

    private XmlCaretContext Content() => Context(XmlCaretContextKind.Content, _open, string.Empty, null, [], null);

    private static XmlCaretContext Context(
        XmlCaretContextKind kind, IEnumerable<XmlElementStep> steps, string prefix, string? attribute,
        IReadOnlyList<string> present, string? justClosed)
    {
        List<XmlElementStep> list = [.. steps];
        return new XmlCaretContext(kind, list.Select(s => s.Name).ToList(), list, prefix, attribute, present, justClosed);
    }

    private bool Starts(string value) => string.CompareOrdinal(text, _index, value, 0, value.Length) == 0;

    // Skips a construct that ends at a fixed delimiter: null when one does not start here, false when the caret is in
    // it, true when it was skipped.
    private bool? SkipDelimited(string start, string end)
    {
        if (!Starts(start)) return null;
        var close = text.IndexOf(end, _index + start.Length, StringComparison.Ordinal);
        if (close < 0 || close + end.Length > caret) return false;
        _index = close + end.Length;
        return true;
    }

    private XmlCaretContext? ScanEndTag()
    {
        var nameStart = _index + 2;
        var nameEnd = NameEnd(nameStart);
        if (nameEnd >= caret)
            return Context(XmlCaretContextKind.EndTagName, _open, text[nameStart..caret], null, [], null);

        var name = text[nameStart..nameEnd];
        var close = text.IndexOf('>', nameEnd);
        if (close < 0 || close >= caret) return Content();

        var last = _open.FindLastIndex(s => s.Name == name);
        if (last >= 0)
        {
            _open.RemoveRange(last, _open.Count - last);
            _counts.RemoveRange(last + 1, _counts.Count - last - 1);
        }
        _index = close + 1;
        return null;
    }

    private XmlCaretContext? ScanStartTag(out string? justClosed)
    {
        justClosed = null;
        var nameStart = _index + 1;
        var nameEnd = NameEnd(nameStart);
        if (nameEnd >= caret)
            return Context(XmlCaretContextKind.ElementName, _open, text[nameStart..caret], null, [], null);

        if (nameEnd == nameStart)
        {
            // A `<` that starts no name is text, not a tag.
            _index = nameStart;
            return null;
        }

        var name = text[nameStart..nameEnd];
        var step = _counts[^1].Add(name);
        var attributes = new List<string>();
        var i = nameEnd;
        while (true)
        {
            while (i < caret && char.IsWhiteSpace(text[i])) i++;
            if (i >= caret)
                return Attribute(XmlCaretContextKind.AttributeName, step, string.Empty, null, attributes,
                    whitespaceBefore: i > nameEnd && char.IsWhiteSpace(text[i - 1]));

            if (text[i] == '>')
            {
                _open.Add(step);
                _counts.Add(new ChildCount());
                _index = i + 1;
                if (_index == caret) justClosed = name;
                return null;
            }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>')
            {
                if (i + 1 >= caret) return Content();
                _index = i + 2;
                return null;
            }

            var attributeEnd = NameEnd(i);
            if (attributeEnd == i)
            {
                // Something a tag cannot hold: give up on this tag and read on as content.
                _index = i + 1;
                return null;
            }
            if (attributeEnd >= caret)
                return Attribute(XmlCaretContextKind.AttributeName, step, text[i..caret], null, attributes, true);

            var attribute = text[i..attributeEnd];
            attributes.Add(attribute);
            i = attributeEnd;
            while (i < caret && char.IsWhiteSpace(text[i])) i++;
            if (i >= caret || text[i] != '=') return Content();
            i++;
            while (i < caret && char.IsWhiteSpace(text[i])) i++;
            if (i >= caret || text[i] is not ('"' or '\'')) return Content();

            var quote = text[i];
            var valueEnd = text.IndexOf(quote, i + 1);
            if (valueEnd < 0 || valueEnd >= caret)
                return Attribute(XmlCaretContextKind.AttributeValue, step, text[(i + 1)..caret], attribute, attributes, true);
            i = valueEnd + 1;
        }
    }

    private XmlCaretContext Attribute(
        XmlCaretContextKind kind, XmlElementStep element, string prefix, string? attribute, List<string> present, bool whitespaceBefore)
    {
        if (kind == XmlCaretContextKind.AttributeName && !whitespaceBefore) return Content();
        present.AddRange(AttributesAfterCaret());
        return Context(kind, [.. _open, element], prefix, attribute, present, null);
    }

    // The attributes the tag goes on with after the caret, which a suggestion must not repeat: the names followed by
    // `="` up to the tag's end, after the rest of the name the caret may be in.
    private IEnumerable<string> AttributesAfterCaret()
    {
        var end = text.IndexOfAny(['<', '>'], caret);
        var rest = text[caret..(end < 0 ? text.Length : end)];
        var nameTail = rest.TakeWhile(IsNameChar).Count();
        return s_attributeAfter.Matches(rest[nameTail..]).Select(m => m.Groups[1].Value);
    }

    private static readonly Regex s_attributeAfter = new(
        @"(?<=[\s""'])([A-Za-z_][\w:.\-]*)\s*=\s*[""']", RegexOptions.None, TimeSpan.FromSeconds(1));

    private int NameEnd(int start)
    {
        var i = start;
        while (i < text.Length && i < caret && IsNameChar(text[i])) i++;
        return i;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is ':' or '_' or '-' or '.';

    // The child elements an open element has had so far.
    private sealed class ChildCount
    {
        private readonly Dictionary<string, int> _byName = new(StringComparer.Ordinal);
        private int _count;

        // Counts a child element and returns where it stands among its siblings.
        public XmlElementStep Add(string name)
        {
            var step = new XmlElementStep(name, _count, _byName.GetValueOrDefault(name));
            _count++;
            _byName[name] = step.NameIndex + 1;
            return step;
        }
    }
}
