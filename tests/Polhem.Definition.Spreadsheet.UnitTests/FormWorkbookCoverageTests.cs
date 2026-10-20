using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Xml.Serialization;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Spreadsheet.UnitTests
{
    /// <summary>
    /// Every property a definition type serializes must have a column in the workbook, so a form that goes
    /// through Excel and back keeps all of it. When a property is added to <see cref="FormField"/> and nobody adds
    /// a column, this fails instead of the property silently disappearing on the next import.
    /// </summary>
    public class FormWorkbookCoverageTests
    {
        /// <summary>Collections the workbook represents as a sheet of their own rather than as a column.</summary>
        private static readonly string[] s_structural =
        [
            $"{nameof(FormSchema)}.{nameof(FormSchema.Tables)}",
            $"{nameof(FormSchema)}.{nameof(FormSchema.Rules)}",
            $"{nameof(FormTable)}.{nameof(FormTable.Fields)}",
        ];

        public static TheoryData<string> DefinitionTypes =>
        [
            nameof(FormSchema),
            nameof(FormTable),
            nameof(FormField),
            nameof(FormRule),
        ];

        [Theory]
        [MemberData(nameof(DefinitionTypes))]
        [DisplayName("Every serialized property of a form definition type has a workbook column")]
        public void SerializedProperties_HaveColumns(string typeName)
        {
            var (type, columns) = typeName switch
            {
                nameof(FormSchema) => (typeof(FormSchema), Keys(FormWorkbookCatalog.FormItems)),
                nameof(FormTable) => (typeof(FormTable), Keys(FormWorkbookCatalog.TableColumns)),
                nameof(FormField) => (typeof(FormField), Keys(FormWorkbookCatalog.FieldColumns)),
                _ => (typeof(FormRule), Keys(FormWorkbookCatalog.RuleColumns)),
            };

            var properties = SerializedProperties(type).ToList();
            Assert.NotEmpty(properties);
            var missing = properties
                .Where(p => !columns.Contains(p) && !s_structural.Contains($"{type.Name}.{p}"))
                .ToList();
            Assert.True(missing.Count == 0,
                $"{type.Name} serializes {string.Join(", ", missing)} but the workbook has no column for it. "
                + "Add a column to FormWorkbookCatalog and regenerate the template.");
        }

        [Theory]
        [MemberData(nameof(DefinitionTypes))]
        [DisplayName("Every property column of the workbook names a serialized property of its definition type")]
        public void PropertyColumns_NameSerializedProperties(string typeName)
        {
            var (type, columns) = typeName switch
            {
                nameof(FormSchema) => (typeof(FormSchema), PropertyKeys(FormWorkbookCatalog.FormItems)),
                nameof(FormTable) => (typeof(FormTable), PropertyKeys(FormWorkbookCatalog.TableColumns)),
                nameof(FormField) => (typeof(FormField), PropertyKeys(FormWorkbookCatalog.FieldColumns)),
                _ => (typeof(FormRule), PropertyKeys(FormWorkbookCatalog.RuleColumns)),
            };

            var properties = SerializedProperties(type).ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(columns);
            Assert.All(columns, key => Assert.Contains(key, properties));
        }

        [Fact]
        [DisplayName("Column keys are unique within each sheet")]
        public void ColumnKeys_AreUnique()
        {
            Assert.Equal(FormWorkbookCatalog.FormItems.Count, Keys(FormWorkbookCatalog.FormItems).Count);
            foreach (var (_, columns) in FormWorkbookCatalog.TabularSheets)
                Assert.Equal(columns.Count, columns.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        [DisplayName("Every list a column refers to exists on the Lists sheet")]
        public void ListNames_Exist()
        {
            var lists = FormWorkbookCatalog.Lists.Select(l => l.Name).ToHashSet(StringComparer.Ordinal);
            var used = FormWorkbookCatalog.FormItems.Cast<ISheetColumn>()
                .Concat(FormWorkbookCatalog.TabularSheets.SelectMany(s => s.Columns))
                .Select(c => c.ListName)
                .OfType<string>()
                .ToList();
            Assert.NotEmpty(used);
            Assert.All(used, name => Assert.Contains(name, lists));
        }

        /// <summary>
        /// The properties <c>XmlSerializer</c> writes: public, not <see cref="XmlIgnoreAttribute"/>, and either
        /// settable or a collection it fills in place.
        /// </summary>
        private static IEnumerable<string> SerializedProperties(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<XmlIgnoreAttribute>() is null)
                .Where(p => p.SetMethod is { IsPublic: true }
                    || (p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)))
                .Select(p => p.Name);

        private static HashSet<string> Keys(IEnumerable<ISheetColumn> columns)
            => columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        private static List<string> PropertyKeys(IEnumerable<ISheetColumn> columns)
            => columns.Where(c => c.MapsProperty).Select(c => c.Key).ToList();
    }
}
