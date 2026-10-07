using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Services;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The property grid translation: what <see cref="PropertyLabels"/> returns, and that the zh-TW resources cover
    /// every category and description of the definition types and still match their English text.
    /// </summary>
    public class PropertyLabelsTests
    {
        private static readonly CultureInfo s_zhTw = CultureInfo.GetCultureInfo("zh-TW");
        private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en");

        private static PropertyGridText Text(PropertyGridTextKind kind, Type type, string? property, string text) =>
            new(kind, type, property, text);

        [Fact]
        [DisplayName("Categories and descriptions are translated into zh-TW, and an inherited property uses its base type's entry")]
        public void Translate_ZhTw_TranslatesCategoryAndDescription()
        {
            Assert.Equal("資料", PropertyLabels.Translate(Text(PropertyGridTextKind.Category, typeof(ProgramItem), null, "Data"), s_zhTw));
            Assert.Equal("程式代碼。", PropertyLabels.Translate(Text(PropertyGridTextKind.Description, typeof(ProgramItem), nameof(ProgramItem.ProgId), "Program ID."), s_zhTw));
            Assert.StartsWith("節點代碼", PropertyLabels.Translate(Text(PropertyGridTextKind.Description, typeof(MenuEntry), nameof(MenuEntry.Id), "x"), s_zhTw));
        }

        [Fact]
        [DisplayName("Property names, English and unknown texts are left as written")]
        public void Translate_NoTranslation_ReturnsNull()
        {
            Assert.Null(PropertyLabels.Translate(Text(PropertyGridTextKind.DisplayName, typeof(ProgramItem), nameof(ProgramItem.ProgId), "ProgId"), s_zhTw));
            Assert.Null(PropertyLabels.Translate(Text(PropertyGridTextKind.Category, typeof(ProgramItem), null, "Data"), s_en));
            Assert.Null(PropertyLabels.Translate(Text(PropertyGridTextKind.Category, typeof(ProgramItem), null, "No Such Category"), s_zhTw));
            Assert.Null(PropertyLabels.Translate(Text(PropertyGridTextKind.Description, typeof(string), "Length", "x"), s_zhTw));
        }

        [Fact]
        [DisplayName("Every category and description of the definition types has a zh-TW translation made from its current English text")]
        public void ZhTwResources_CoverEveryCategoryAndDescription()
        {
            var entries = ZhTwEntries();
            var (categories, descriptions) = DefinitionTexts();
            var problems = new List<string>();

            foreach (var category in categories)
            {
                if (!entries.ContainsKey("PropCategory_" + category))
                    problems.Add($"PropCategory_{category}: missing");
            }
            foreach (var (key, english) in descriptions)
            {
                if (!entries.TryGetValue(key, out var entry))
                    problems.Add($"{key}: missing");
                else if (!string.Equals(entry.Comment, english, StringComparison.Ordinal))
                    problems.Add($"{key}: translated from \"{entry.Comment}\", but the description is now \"{english}\"");
            }

            // Anti-vacuous: the scan must still reach the definition types.
            Assert.True(descriptions.Count > 150, $"Only {descriptions.Count} descriptions were found.");
            Assert.True(problems.Count == 0,
                "Translate these in tools/DefineEditor/Resources/PropertyText.zh-TW.resx, with the English text as the "
                + "<comment>:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }

        [Fact]
        [DisplayName("Every zh-TW property grid entry belongs to a category or description that still exists")]
        public void ZhTwResources_HaveNoOrphans()
        {
            var (categories, descriptions) = DefinitionTexts();
            var orphans = ZhTwEntries().Keys
                .Where(k => k.StartsWith("PropCategory_", StringComparison.Ordinal)
                    ? !categories.Contains(k["PropCategory_".Length..])
                    : !descriptions.ContainsKey(k))
                .ToList();

            Assert.True(orphans.Count == 0, "Remove these entries: " + string.Join(", ", orphans));
        }

        /// <summary>
        /// The categories in use and the descriptions, keyed as <see cref="PropertyLabels"/> looks them up, of every
        /// property a property grid shows on the public types of Polhem.Definition.
        /// </summary>
        private static (HashSet<string> Categories, Dictionary<string, string> Descriptions) DefinitionTexts()
        {
            var categories = new HashSet<string>(StringComparer.Ordinal);
            var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var type in typeof(PropertyCategories).Assembly.GetExportedTypes())
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (property.GetIndexParameters().Length > 0) continue;
                    if (Attribute.GetCustomAttribute(property, typeof(BrowsableAttribute), inherit: true) is BrowsableAttribute { Browsable: false }) continue;
                    if (Attribute.GetCustomAttribute(property, typeof(CategoryAttribute), inherit: true) is CategoryAttribute category)
                        categories.Add(category.Category);
                    if (Attribute.GetCustomAttribute(property, typeof(DescriptionAttribute), inherit: true) is DescriptionAttribute { Description.Length: > 0 } description)
                        descriptions[$"PropDesc_{type.Name}_{property.Name}"] = description.Description;
                }
            }
            return (categories, descriptions);
        }

        private static Dictionary<string, (string Value, string? Comment)> ZhTwEntries()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Polhem.slnx")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var path = Path.Combine(directory.FullName, "tools", "DefineEditor", "Resources", "PropertyText.zh-TW.resx");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(path, settings);
            return XDocument.Load(reader).Root!.Elements("data").ToDictionary(
                d => (string)d.Attribute("name")!,
                d => ((string)d.Element("value")!, (string?)d.Element("comment")),
                StringComparer.Ordinal);
        }
    }
}
