using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Adds entries to the shared settings and language files at the XML level.
/// </summary>
/// <remarks>
/// NOTE: these files are edited, not round-tripped through their definition types. Deserializing and
/// serializing <c>MenuSettings.xml</c> would drop every comment in it, and the files people maintain by hand
/// carry comments that explain the entries. Every method only adds what is missing and leaves the rest of
/// the document, comments included, as it was.
/// </remarks>
internal static class SettingsXmlEditor
{
    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";

    public static XDocument LoadOrCreate(string path, string rootName)
    {
        if (!File.Exists(path))
        {
            return new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement(rootName,
                    new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                    new XAttribute(XNamespace.Xmlns + "xsd", XsdNamespace)));
        }
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static XDocument Load(Stream stream)
    {
        // Dropping the whitespace lets the writer indent added elements like the rest of the file; with the
        // original whitespace kept, an added element lands on the line of its last sibling.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
        };
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    public static string Serialize(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false),
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
            document.Save(writer);
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Adds a <c>TableItem</c> under the <c>DbCategory</c> with the given id, creating the category if needed.</summary>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public static bool AddTableItem(XDocument document, string categoryId, string tableName, string displayName)
    {
        var categories = Child(document.Root!, "Categories");
        var category = categories.Elements("DbCategory")
            .FirstOrDefault(e => Is(e, "Id", categoryId, StringComparison.Ordinal));
        if (category is null)
        {
            category = new XElement("DbCategory", new XAttribute("Id", categoryId), new XAttribute("DisplayName", categoryId));
            categories.Add(category);
        }
        var tables = Child(category, "Tables");
        if (tables.Elements("TableItem").Any(e => Is(e, "TableName", tableName, StringComparison.OrdinalIgnoreCase)))
            return false;
        tables.Add(new XElement("TableItem", new XAttribute("TableName", tableName), new XAttribute("DisplayName", displayName)));
        return true;
    }

    /// <summary>Adds a <c>ProgramItem</c> for the ProgId when none exists.</summary>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public static bool AddProgramItem(XDocument document, string progId, string displayName)
    {
        var items = Child(document.Root!, "Items");
        if (items.Elements("ProgramItem").Any(e => Is(e, "ProgId", progId, StringComparison.OrdinalIgnoreCase)))
            return false;
        items.Add(new XElement("ProgramItem", new XAttribute("ProgId", progId), new XAttribute("DisplayName", displayName)));
        return true;
    }

    /// <summary>
    /// Adds a <c>MenuEntry</c> for the ProgId when no entry anywhere in the menu points to it. The entry goes
    /// into the folder with <paramref name="folderId"/>, which is created at the top level when missing.
    /// </summary>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public static bool AddMenuEntry(
        XDocument document, string folderId, string folderCaption, string entryId, string progId, string caption)
    {
        var root = document.Root!;
        if (root.Descendants("MenuEntry").Any(e => Is(e, "ProgId", progId, StringComparison.OrdinalIgnoreCase)))
            return false;

        var parent = Child(root, "Items");
        if (folderId.Length > 0)
        {
            var folder = root.Descendants("MenuFolder").FirstOrDefault(e => Is(e, "Id", folderId, StringComparison.Ordinal));
            if (folder is null)
            {
                folder = new XElement("MenuFolder",
                    new XAttribute("Id", folderId),
                    new XAttribute("Caption", folderCaption.Length > 0 ? folderCaption : folderId),
                    new XAttribute("Order", NextOrder(parent)));
                parent.Add(folder);
            }
            parent = Child(folder, "Items");
        }
        parent.Add(new XElement("MenuEntry",
            new XAttribute("Id", entryId),
            new XAttribute("Caption", caption),
            new XAttribute("Order", NextOrder(parent)),
            new XAttribute("ProgId", progId)));
        return true;
    }

    /// <summary>
    /// Sets each key of <paramref name="items"/> in a <c>LanguageResource</c> document: an existing item takes
    /// the new value, a missing one is added. Keys not in <paramref name="items"/> are left alone.
    /// </summary>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public static bool MergeLanguageItems(XDocument document, string ns, string lang, IEnumerable<KeyValuePair<string, string>> items)
    {
        var root = document.Root!;
        root.SetAttributeValue("Namespace", ns);
        root.SetAttributeValue("Lang", lang);
        var container = Child(root, "Items");
        bool changed = false;
        foreach (var (key, value) in items)
        {
            var item = container.Elements("LanguageItem").FirstOrDefault(e => Is(e, "Key", key, StringComparison.Ordinal));
            if (item is null)
            {
                container.Add(new XElement("LanguageItem", new XAttribute("Key", key), new XAttribute("Value", value)));
                changed = true;
            }
            else if (!Is(item, "Value", value, StringComparison.Ordinal))
            {
                item.SetAttributeValue("Value", value);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Adds to a TableSchema document the <c>DbField</c> and <c>DbTableIndex</c> elements of
    /// <paramref name="generatedXml"/> it lacks, matched by field name and index name. Existing elements are not
    /// changed, so lengths, captions, columns that are not on the form and hand-made indexes stay. The one exception
    /// is a <c>String</c> column without a length, which no database can build: it takes the form's length.
    /// A missing column goes where the form has it: after the nearest earlier column the document already has, or
    /// else before the nearest later one, and at the end only when it has neither. Missing indexes go at the end.
    /// </summary>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public static bool MergeTableSchema(XDocument document, string generatedXml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true };
        using var reader = XmlReader.Create(new StringReader(generatedXml), settings);
        var generated = XDocument.Load(reader).Root!;
        var root = document.Root!;
        bool changed = AddMissingInOrder(root, generated, "Fields", "DbField", "FieldName");
        changed |= FillMissingLengths(root, generated);
        changed |= AddMissing(root, generated, "Indexes", "DbTableIndex", "Name");
        return changed;
    }

    private static bool FillMissingLengths(XElement root, XElement generated)
    {
        var lengths = (generated.Element("Fields")?.Elements("DbField") ?? [])
            .Where(f => ((int?)f.Attribute("Length") ?? 0) > 0)
            .ToDictionary(f => (string?)f.Attribute("FieldName") ?? string.Empty, f => (string)f.Attribute("Length")!,
                StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        foreach (var field in root.Element("Fields")?.Elements("DbField") ?? [])
        {
            if ((string?)field.Attribute("DbType") != "String" || ((int?)field.Attribute("Length") ?? 0) > 0) continue;
            if (!lengths.TryGetValue((string?)field.Attribute("FieldName") ?? string.Empty, out var length)) continue;
            field.SetAttributeValue("Length", length);
            changed = true;
        }
        return changed;
    }

    private static bool AddMissingInOrder(XElement root, XElement generated, string container, string element, string key)
    {
        var source = generated.Element(container)?.Elements(element).ToList();
        if (source is null) return false;
        var target = Child(root, container);
        XElement? Find(XElement item) => target.Elements(element)
            .FirstOrDefault(e => Is(e, key, (string?)item.Attribute(key) ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        bool changed = false;
        for (int i = 0; i < source.Count; i++)
        {
            if (Find(source[i]) is not null) continue;
            var copy = new XElement(source[i]);
            var before = source.Take(i).Reverse().Select(Find).FirstOrDefault(e => e is not null);
            var after = before is null ? source.Skip(i + 1).Select(Find).FirstOrDefault(e => e is not null) : null;
            if (before is not null) before.AddAfterSelf(copy);
            else if (after is not null) after.AddBeforeSelf(copy);
            else target.Add(copy);
            changed = true;
        }
        return changed;
    }

    private static bool AddMissing(XElement root, XElement generated, string container, string element, string key)
    {
        var source = generated.Element(container);
        if (source is null) return false;
        var target = Child(root, container);
        bool changed = false;
        foreach (var item in source.Elements(element))
        {
            var name = (string?)item.Attribute(key) ?? string.Empty;
            if (target.Elements(element).Any(e => Is(e, key, name, StringComparison.OrdinalIgnoreCase))) continue;
            target.Add(new XElement(item));
            changed = true;
        }
        return changed;
    }

    private static int NextOrder(XElement items)
        => (items.Elements().Select(e => (int?)e.Attribute("Order") ?? 0).DefaultIfEmpty(0).Max() / 10 + 1) * 10;

    private static XElement Child(XElement parent, string name)
    {
        var child = parent.Element(name);
        if (child is null)
        {
            child = new XElement(name);
            parent.Add(child);
        }
        return child;
    }

    private static bool Is(XElement element, string attribute, string value, StringComparison comparison)
        => string.Equals((string?)element.Attribute(attribute), value, comparison);
}
