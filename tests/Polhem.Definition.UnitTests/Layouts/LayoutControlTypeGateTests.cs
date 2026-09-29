using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Keeps every FormLayout definition file in the repository from dropping the editor its FormSchema
    /// asks for.
    /// </summary>
    /// <remarks>
    /// A layout field carries its own <see cref="LayoutFieldBase.ControlType"/>, and an omitted attribute
    /// means <see cref="ControlType.TextEdit"/>, not "whatever the schema says": the runtime renders the
    /// stored layout and never consults the schema's control type. <see cref="FormLayoutGenerator"/> bakes
    /// the schema's editor into each field, so a hand-written layout that leaves the attribute out
    /// silently turns a dropdown, a check box, a date or a number into a plain text box. That is how the
    /// shipped AuditRule layout came to show its mode codes as numbers and its flag as the text "False".
    /// A layout may still choose a different editor on purpose; this gate only rejects the plain text box
    /// where the schema resolves to something else, and the date-only editor where the schema holds an
    /// instant: <see cref="ControlType.DateEdit"/> writes the date alone back, so it would erase the time
    /// part of a <see cref="ControlType.DateTimeEdit"/> field on the first edit.
    /// </remarks>
    public class LayoutControlTypeGateTests
    {
        private static readonly HashSet<string> s_skippedDirectories = new(StringComparer.Ordinal)
        {
            ".git", "bin", "obj", "local", "node_modules",
        };

        [Fact]
        [DisplayName("No FormLayout file in the repository shows a plain text box, or a date-only editor for an instant, where its FormSchema resolves to another editor")]
        public void LayoutFiles_TextEditFields_MatchSchemaControlType()
        {
            string root = RepoRoot.Find();
            var layoutPaths = EnumerateLayoutFiles(root).ToList();
            Assert.NotEmpty(layoutPaths);

            var violations = new List<string>();
            foreach (string layoutPath in layoutPaths)
            {
                string relative = Path.GetRelativePath(root, layoutPath);
                string? schemaPath = FindSchemaPath(layoutPath);
                if (schemaPath is null)
                {
                    violations.Add($"{relative}: no FormSchema found next to it or in a sibling Define folder");
                    continue;
                }

                var layout = XmlCodec.DeserializeFromFile<FormLayout>(layoutPath)!;
                var schema = XmlCodec.DeserializeFromFile<FormSchema>(schemaPath)!;
                var master = schema.MasterTable!;
                foreach (var field in layout.Sections!.SelectMany(section => section.Fields!))
                    Check(relative, master, field, violations);
                foreach (var grid in layout.Details!)
                {
                    if (!schema.Tables!.Contains(grid.TableName)) { continue; }
                    var table = schema.Tables[grid.TableName];
                    foreach (var column in grid.Columns!)
                        Check(relative, table, column, violations);
                }
            }

            Assert.True(violations.Count == 0,
                "These layout fields use TextEdit, or DateEdit for a DateTime field, although the schema resolves "
                + "to another editor. Set ControlType (and DisplayFields for a lookup) as FormLayoutGenerator would: "
                + string.Join("; ", violations));
        }

        [Fact]
        [DisplayName("The shipped AuditRule layout renders the mode fields as dropdowns and the sensitive flag as a check box")]
        public void ShippedAuditRuleLayout_NonTextFields_UseSchemaEditors()
        {
            using var stream = Defaults.OpenEmbedded("FormLayout/AuditRule.FormLayout.xml");
            using var reader = new StreamReader(stream);
            var layout = XmlCodec.Deserialize<FormLayout>(reader.ReadToEnd())!;
            var fields = layout.Sections!.SelectMany(section => section.Fields!)
                .ToDictionary(field => field.FieldName, StringComparer.Ordinal);

            Assert.Equal(ControlType.DropDownEdit, fields["change_mode"].ControlType);
            Assert.Equal(ControlType.DropDownEdit, fields["access_mode"].ControlType);
            Assert.Equal(ControlType.CheckEdit, fields["is_sensitive"].ControlType);
        }

        [Theory]
        [InlineData(ControlType.TextEdit, FieldDbType.DateTime, true)]
        [InlineData(ControlType.DateEdit, FieldDbType.DateTime, true)]
        [InlineData(ControlType.DateTimeEdit, FieldDbType.DateTime, false)]
        [InlineData(ControlType.DateEdit, FieldDbType.Date, false)]
        [InlineData(ControlType.TextEdit, FieldDbType.String, false)]
        [InlineData(ControlType.MemoEdit, FieldDbType.DateTime, false)]
        [DisplayName("The gate rejects a plain text box or a date-only editor over an instant, and accepts other deliberate choices")]
        public void Check_ControlTypeAgainstSchema_ReportsOnlyTheRejectedFallbacks(
            ControlType layoutType, FieldDbType dbType, bool rejected)
        {
            var table = new FormTable("T", "T");
            table.Fields!.Add(new FormField("f", "F", dbType));
            var violations = new List<string>();

            Check("layout.xml", table, new LayoutField { FieldName = "f", ControlType = layoutType }, violations);

            Assert.Equal(rejected, violations.Count == 1);
        }

        private static void Check(string relative, FormTable table, LayoutFieldBase field, List<string> violations)
        {
            if (!table.Fields!.Contains(field.FieldName)) { return; }
            var expected = LayoutColumnFactory.ResolveControlType(table.Fields[field.FieldName]);
            if (field.ControlType == ControlType.TextEdit && expected != ControlType.TextEdit)
                violations.Add($"{relative}: {table.TableName}.{field.FieldName} is TextEdit, schema resolves to {expected}");
            else if (field.ControlType == ControlType.DateEdit && expected == ControlType.DateTimeEdit)
                violations.Add($"{relative}: {table.TableName}.{field.FieldName} is DateEdit, which drops the time part of a DateTime field");
        }

        private static IEnumerable<string> EnumerateLayoutFiles(string directory)
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.FormLayout.xml"))
                yield return file;
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                if (s_skippedDirectories.Contains(Path.GetFileName(child))) { continue; }
                foreach (string file in EnumerateLayoutFiles(child))
                    yield return file;
            }
        }

        /// <summary>
        /// A layout's schema sits in the <c>FormSchema</c> folder beside its own <c>FormLayout</c> folder. A
        /// customization layer (<c>Customize/{code}/FormLayout</c>) carries no schema, so the nearest ancestor's
        /// <c>Define/FormSchema</c> supplies it.
        /// </summary>
        private static string? FindSchemaPath(string layoutPath)
        {
            string fileName = Path.GetFileName(layoutPath).Replace(".FormLayout.xml", ".FormSchema.xml", StringComparison.Ordinal);
            var dir = Directory.GetParent(Path.GetDirectoryName(layoutPath)!);
            while (dir != null)
            {
                foreach (string candidate in new[]
                {
                    Path.Combine(dir.FullName, "FormSchema", fileName),
                    Path.Combine(dir.FullName, "Define", "FormSchema", fileName),
                })
                {
                    if (File.Exists(candidate)) { return candidate; }
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
