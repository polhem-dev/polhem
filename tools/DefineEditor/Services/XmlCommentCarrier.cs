using System.Xml;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Puts the comments of a definition file back into the XML <c>XmlSerializer</c> writes for it, which has none: the
/// define objects hold no comments, so a save from the tree would otherwise drop every one.
/// </summary>
/// <remarks>
/// <para>
/// Each comment of the old file is tied to the element it stands before, or, when nothing follows it, to the end of
/// the element it is in. An element is recognised in the new XML by its name and its first attribute, which for a
/// definition item is its key (<c>FieldName</c>, <c>TableName</c>, <c>ProgId</c>), so a comment follows its field when
/// fields are moved or added around it; an element with no attribute is recognised by its place among the elements of
/// its name. A comment whose element is gone moves to the end of the nearest element still there.
/// </para>
/// <para>
/// The comments are inserted as whole lines, and the rest of the generated text is kept as it is.
/// </para>
/// </remarks>
public static class XmlCommentCarrier
{
    /// <summary>
    /// Returns <paramref name="generated"/> with the comments of <paramref name="original"/> inserted; returns
    /// <paramref name="generated"/> unchanged when <paramref name="original"/> has no comment or cannot be read.
    /// </summary>
    /// <param name="original">The file as it is on disk.</param>
    /// <param name="generated">The XML written from the define object.</param>
    public static string Carry(string original, string generated)
    {
        if (!original.Contains("<!--", StringComparison.Ordinal)) return generated;
        if (Read(original) is not { } source || Read(generated) is not { } target) return generated;

        var lines = generated.Split('\n');
        var lineEnd = generated.Contains("\r\n", StringComparison.Ordinal) ? "\r" : string.Empty;
        var insertions = new List<(int Line, int Order, string Text)>();
        var order = 0;
        Collect(source, [], target, insertions, ref order, lineEnd);
        if (insertions.Count == 0) return generated;

        var result = lines.ToList();
        foreach (var insertion in insertions.OrderByDescending(i => i.Line).ThenByDescending(i => i.Order))
            result.Insert(Math.Clamp(insertion.Line, 0, result.Count), insertion.Text);
        return string.Join('\n', result);
    }

    // Walks the old file's tree and places each comment found in it.
    private static void Collect(
        XmlNodeInfo node, List<XmlNodeInfo> path, XmlNodeInfo target,
        List<(int, int, string)> insertions, ref int order, string lineEnd)
    {
        for (var i = 0; i < node.Items.Count; i++)
        {
            switch (node.Items[i])
            {
                case XmlCommentInfo comment:
                    var next = node.Items.Skip(i + 1).OfType<XmlNodeInfo>().FirstOrDefault();
                    insertions.Add(Place(comment, next is null ? path.Append(node).ToList() : [.. path, node, next],
                        before: next is not null, target, order++, lineEnd));
                    break;
                case XmlNodeInfo child:
                    Collect(child, [.. path, node], target, insertions, ref order, lineEnd);
                    break;
            }
        }
    }

    // Where a comment goes in the new XML: before the element it stood before, or at the end of the element it ended.
    // The path starts with the document node.
    private static (int, int, string) Place(
        XmlCommentInfo comment, List<XmlNodeInfo> path, bool before, XmlNodeInfo target, int order, string lineEnd)
    {
        var found = new List<XmlNodeInfo> { target };
        for (var i = 1; i < path.Count; i++)
        {
            if (Match(found[^1], path[i], path[i - 1]) is not { } match) break;
            found.Add(match);
        }

        var complete = found.Count == path.Count;
        var anchor = found[^1];
        if (complete && before)
            return (anchor.Line - 1, order, Indent(anchor.Column) + comment.Text + lineEnd);

        // The end of an element: before its end tag, indented one step further. A document has no end tag, and an
        // element written as `<X />` has no inside, so the comment goes after it.
        if (anchor.IsDocument || anchor.IsEmpty)
            return (anchor.IsDocument ? int.MaxValue : anchor.EndLine, order, Indent(anchor.Column) + comment.Text + lineEnd);
        return (anchor.EndLine - 1, order, Indent(anchor.Column + 2) + comment.Text + lineEnd);
    }

    // The element of the new XML that stands for `wanted`, a child of the old XML's `oldParent`, among the children of
    // `parent`: the one with the same name and first attribute, or, with no attribute to go by, the one at the same
    // place among the elements of its name.
    private static XmlNodeInfo? Match(XmlNodeInfo parent, XmlNodeInfo wanted, XmlNodeInfo oldParent)
    {
        var candidates = parent.Items.OfType<XmlNodeInfo>().Where(c => c.Name == wanted.Name).ToList();
        if (wanted.Key is not null)
            return candidates.FirstOrDefault(c => c.Key == wanted.Key);

        var index = oldParent.Items.OfType<XmlNodeInfo>().Where(c => c.Name == wanted.Name).ToList().IndexOf(wanted);
        return index >= 0 && index < candidates.Count ? candidates[index] : null;
    }

    private static string Indent(int column) => new(' ', Math.Max(column - 1, 0));

    private static XmlNodeInfo? Read(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var document = new XmlNodeInfo(string.Empty, null, 0, 1, isDocument: true);
        var open = new Stack<XmlNodeInfo>();
        open.Push(document);
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            var info = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        // The position is the name's; the tag starts one column before it.
                        var element = new XmlNodeInfo(reader.LocalName, FirstAttribute(reader), info.LineNumber,
                            info.LinePosition - 1, isDocument: false);
                        open.Peek().Items.Add(element);
                        if (reader.IsEmptyElement)
                        {
                            element.IsEmpty = true;
                            element.EndLine = element.Line;
                        }
                        else
                        {
                            open.Push(element);
                        }
                        break;
                    case XmlNodeType.EndElement:
                        open.Pop().EndLine = info.LineNumber;
                        break;
                    case XmlNodeType.Comment:
                        open.Peek().Items.Add(new XmlCommentInfo("<!--" + reader.Value + "-->"));
                        break;
                }
            }
        }
        catch (XmlException)
        {
            return null;
        }
        return document;
    }

    // The element's first attribute other than a namespace declaration, as `name=value`; null when it has none.
    private static string? FirstAttribute(XmlReader reader)
    {
        if (!reader.MoveToFirstAttribute()) return null;
        try
        {
            do
            {
                if (reader.Prefix != "xmlns" && reader.Name != "xmlns")
                    return reader.Name + "=" + reader.Value;
            }
            while (reader.MoveToNextAttribute());
            return null;
        }
        finally
        {
            reader.MoveToElement();
        }
    }

    // An element of the tree Carry reads, with what it holds in order: elements and comments.
    private sealed class XmlNodeInfo(string name, string? key, int line, int column, bool isDocument)
    {
        public string Name { get; } = name;

        public string? Key { get; } = key;

        public int Line { get; } = line;

        public int Column { get; } = column;

        public bool IsDocument { get; } = isDocument;

        public bool IsEmpty { get; set; }

        public int EndLine { get; set; }

        public List<object> Items { get; } = [];
    }

    private sealed record XmlCommentInfo(string Text);
}
