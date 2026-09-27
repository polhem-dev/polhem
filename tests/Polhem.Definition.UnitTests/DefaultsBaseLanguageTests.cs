using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Keeps the framework's shipped definitions English at base, their translations complete, and
    /// the copies of them elsewhere in the repository identical to them.
    /// </summary>
    /// <remarks>
    /// Base text is what every path without a translation shows — DB column comments, the permission
    /// model name, a view with no definition loader — so it is English, and other languages live only
    /// in <c>Language/{culture}/</c>. The copies under <c>tests/Define</c>, <c>samples/Define</c> and
    /// the Northwind demo are what tests and demos actually load, so a copy that drifts from the
    /// shipped file tests and demonstrates something the framework does not ship.
    /// </remarks>
    public class DefaultsBaseLanguageTests
    {
        private static readonly Regex s_cjk = new(@"[㐀-鿿豈-﫿]", RegexOptions.None, TimeSpan.FromSeconds(1));

        private static readonly string[] s_copyRoots = ["tests/Define", "samples/Define", "apps/Polhem.Northwind/Define"];

        /// <summary>
        /// Copies that differ from the shipped file on purpose: deployment settings, and forms or tables
        /// a test or demo extends. Every other copy must match byte for byte.
        /// </summary>
        private static readonly HashSet<string> s_intentionalDivergences = new(StringComparer.Ordinal)
        {
            "tests/Define/DatabaseSettings.xml",
            "tests/Define/DbCategorySettings.xml",
            "tests/Define/SystemSettings.xml",
            "tests/Define/FormSchema/Department.FormSchema.xml",
            "tests/Define/FormSchema/Employee.FormSchema.xml",
            "tests/Define/FormLayout/Department.FormLayout.xml",
            "tests/Define/FormLayout/Employee.FormLayout.xml",
            "tests/Define/TableSchema/company/st_department.TableSchema.xml",
            "tests/Define/TableSchema/company/st_employee.TableSchema.xml",
            "samples/Define/DatabaseSettings.xml",
            "samples/Define/DbCategorySettings.xml",
            "samples/Define/SystemSettings.xml",
            "samples/Define/FormSchema/Department.FormSchema.xml",
            "samples/Define/FormSchema/Employee.FormSchema.xml",
            "samples/Define/FormLayout/Department.FormLayout.xml",
            "samples/Define/FormLayout/Employee.FormLayout.xml",
            "apps/Polhem.Northwind/Define/DatabaseSettings.xml",
            "apps/Polhem.Northwind/Define/DbCategorySettings.xml",
            "apps/Polhem.Northwind/Define/SystemSettings.xml",
            "apps/Polhem.Northwind/Define/FormSchema/AuditRule.FormSchema.xml",
            "apps/Polhem.Northwind/Define/FormLayout/AuditRule.FormLayout.xml",
            "apps/Polhem.Northwind/Define/FormSchema/Department.FormSchema.xml",
            "apps/Polhem.Northwind/Define/FormSchema/Employee.FormSchema.xml",
            "apps/Polhem.Northwind/Define/FormLayout/Department.FormLayout.xml",
            "apps/Polhem.Northwind/Define/FormLayout/Employee.FormLayout.xml",
            "apps/Polhem.Northwind/Define/TableSchema/company/st_department.TableSchema.xml",
            "apps/Polhem.Northwind/Define/TableSchema/company/st_employee.TableSchema.xml",
        };

        [Fact]
        [DisplayName("Shipped definitions carry no CJK text outside the non-English Language folders")]
        public void Defaults_BaseText_IsEnglish()
        {
            var offenders = Defaults.ListEmbedded()
                .Where(path => !IsTranslation(path))
                .Where(path => s_cjk.IsMatch(ReadEmbedded(path)))
                .ToList();

            Assert.Empty(offenders);
        }

        [Fact]
        [DisplayName("Every shipped zh-TW language file translates every caption and display name of its form")]
        public void Defaults_ZhTwTranslations_CoverEveryCaption()
        {
            var schemas = Defaults.ListEmbedded().Where(p => p.StartsWith("FormSchema/", StringComparison.Ordinal));
            foreach (string path in schemas)
            {
                var schema = XmlCodec.Deserialize<FormSchema>(ReadEmbedded(path))!;
                var language = XmlCodec.Deserialize<LanguageResource>(
                    ReadEmbedded($"Language/zh-TW/{schema.ProgId}.Language.xml"))!;

                var expected = new List<string> { FormSchemaLocalizer.SchemaDisplayNameKey };
                foreach (var table in schema.Tables!)
                {
                    expected.Add(string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.TableDisplayNameKeyFormat, table.TableName));
                    expected.AddRange(table.Fields!.Select(f =>
                        string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.FieldCaptionKeyFormat, f.FieldName)));
                }

                var missing = expected.Where(key => !language.Items.Contains(key)).ToList();
                Assert.True(missing.Count == 0, $"{path}: no zh-TW entry for {string.Join(", ", missing)}");
            }
        }

        [Fact]
        [DisplayName("Copies of shipped definitions in tests, samples and Northwind match the shipped file byte for byte")]
        public void Copies_OfShippedDefinitions_MatchDefaults()
        {
            string root = FindRepositoryRoot();
            var drifted = new List<string>();
            foreach (string relative in Defaults.ListEmbedded())
            {
                byte[] shipped = ReadEmbeddedBytes(relative);
                foreach (string copyRoot in s_copyRoots)
                {
                    string copyPath = $"{copyRoot}/{relative}";
                    string full = Path.Combine(root, copyPath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(full) || s_intentionalDivergences.Contains(copyPath)) { continue; }
                    if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(shipped)) { drifted.Add(copyPath); }
                }
            }

            Assert.True(drifted.Count == 0,
                "These copies differ from src/Polhem.Definition/Defaults. Copy the shipped file over them, or list a deliberate "
                + "difference in s_intentionalDivergences: " + string.Join(", ", drifted));
        }

        [Fact]
        [DisplayName("Every listed intentional divergence still exists, so the list cannot hide a stale entry")]
        public void IntentionalDivergences_AllExist()
        {
            string root = FindRepositoryRoot();
            var missing = s_intentionalDivergences
                .Where(path => !File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();

            Assert.Empty(missing);
        }

        private static bool IsTranslation(string path)
            => path.StartsWith("Language/", StringComparison.Ordinal)
               && !path.StartsWith("Language/en", StringComparison.Ordinal);

        private static string ReadEmbedded(string path)
        {
            using var reader = new StreamReader(Defaults.OpenEmbedded(path));
            return reader.ReadToEnd();
        }

        private static byte[] ReadEmbeddedBytes(string path)
        {
            using var stream = Defaults.OpenEmbedded(path);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Polhem.slnx")))
            {
                dir = dir.Parent;
            }
            Assert.True(dir != null, "No repository root (Polhem.slnx) above the test output directory.");
            return dir!.FullName;
        }
    }
}
