namespace Polhem.DefineEditor.Services;

/// <summary>Reads an XML text up to the caret for <see cref="XmlCaretContext.At"/>.</summary>
internal sealed class XmlCaretScanner(string text, int caret)
{
    private readonly List<string> _open = [];
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
        return new XmlCaretContext(XmlCaretContextKind.Content, [.. _open], string.Empty, null, [], justClosed);
    }

    private XmlCaretContext Content() =>
        new(XmlCaretContextKind.Content, [.. _open], string.Empty, null, [], null);

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
            return new XmlCaretContext(XmlCaretContextKind.EndTagName, [.. _open],
                text[nameStart..caret], null, [], null);

        var name = text[nameStart..nameEnd];
        var close = text.IndexOf('>', nameEnd);
        if (close < 0 || close >= caret) return Content();

        var last = _open.FindLastIndex(n => n == name);
        if (last >= 0) _open.RemoveRange(last, _open.Count - last);
        _index = close + 1;
        return null;
    }

    private XmlCaretContext? ScanStartTag(out string? justClosed)
    {
        justClosed = null;
        var nameStart = _index + 1;
        var nameEnd = NameEnd(nameStart);
        if (nameEnd >= caret)
            return new XmlCaretContext(XmlCaretContextKind.ElementName, [.. _open],
                text[nameStart..caret], null, [], null);

        if (nameEnd == nameStart)
        {
            // A `<` that starts no name is text, not a tag.
            _index = nameStart;
            return null;
        }

        var name = text[nameStart..nameEnd];
        var attributes = new List<string>();
        var i = nameEnd;
        while (true)
        {
            while (i < caret && char.IsWhiteSpace(text[i])) i++;
            if (i >= caret)
                return Attribute(XmlCaretContextKind.AttributeName, name, string.Empty, null, attributes,
                    whitespaceBefore: i > nameEnd && char.IsWhiteSpace(text[i - 1]));

            if (text[i] == '>')
            {
                _open.Add(name);
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
                return Attribute(XmlCaretContextKind.AttributeName, name, text[i..caret], null, attributes, true);

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
                return Attribute(XmlCaretContextKind.AttributeValue, name, text[(i + 1)..caret], attribute, attributes, true);
            i = valueEnd + 1;
        }
    }

    private XmlCaretContext Attribute(
        XmlCaretContextKind kind, string element, string prefix, string? attribute, List<string> present, bool whitespaceBefore) =>
        kind == XmlCaretContextKind.AttributeName && !whitespaceBefore
            ? Content()
            : new XmlCaretContext(kind, [.. _open, element], prefix, attribute, present, null);

    private int NameEnd(int start)
    {
        var i = start;
        while (i < text.Length && i < caret && IsNameChar(text[i])) i++;
        return i;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is ':' or '_' or '-' or '.';
}
