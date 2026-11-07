using System.ComponentModel;
using System.Text.Json;
using System.Xml;
using Polhem.Definition;
using Polhem.DefineEditor.Services;
using Polhem.Tests.Shared;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// <see cref="XmlSchemaExport"/> writes an XML schema for each kind of definition file and the VS Code associations
    /// that tie the files to them; the schemas accept the repository's own definition files.
    /// </summary>
    public sealed class XmlSchemaExportTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "polhem-xsd-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        private static bool Matches(string fileName, string pattern) =>
            pattern.StartsWith('*')
                ? fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
                : string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);

        [Fact]
        [DisplayName("Export writes a schema for every kind of definition file and associations that point at them")]
        public void Export_WritesSchemasAndAssociations()
        {
            var written = XmlSchemaExport.Export(_folder);

            var schemas = written.Where(p => p.EndsWith(".xsd", StringComparison.Ordinal)).ToList();
            Assert.Contains(Path.Combine(_folder, "FormSchema.xsd"), schemas);
            Assert.Contains(Path.Combine(_folder, "SystemSettings.xsd"), schemas);
            Assert.Equal(DefinePathScanner.FilePatterns.Select(p => p.Type).Distinct().Count(), schemas.Count);

            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_folder, XmlSchemaExport.AssociationsFileName)));
            var associations = json.RootElement.EnumerateArray().ToList();
            Assert.Equal(DefinePathScanner.FilePatterns.Count, associations.Count);
            Assert.All(associations, a => Assert.True(File.Exists(a.GetProperty("systemId").GetString())));
            Assert.Contains(associations, a => a.GetProperty("pattern").GetString() == "**/*.FormSchema.xml");
        }

        [Fact]
        [DisplayName("Every definition file of tests/Define, the framework defaults and the Northwind sample is valid against its exported schema")]
        public void ExportedSchemas_AcceptRepositoryDefinitionFiles()
        {
            XmlSchemaExport.Export(_folder);
            var root = RepoRoot.Find();
            var files = new[]
                {
                    Path.Combine(root, "tests", "Define"),
                    Path.Combine(root, "src", "Polhem.Definition", "Defaults"),
                    Path.Combine(root, "apps", "Polhem.Northwind", "Define"),
                }
                .SelectMany(d => Directory.EnumerateFiles(d, "*.xml", SearchOption.AllDirectories))
                .Select(f => (File: f, Pattern: DefinePathScanner.FilePatterns.FirstOrDefault(p => Matches(Path.GetFileName(f), p.FileName))))
                .Where(f => f.Pattern != default)
                .ToList();
            Assert.Contains(files, f => f.File.EndsWith(".FormSchema.xml", StringComparison.Ordinal));

            var problems = new List<string>();
            foreach (var (file, pattern) in files)
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, ValidationType = ValidationType.Schema };
                settings.Schemas.Add(null, Path.Combine(_folder, pattern.Type.ToClrType().Name + ".xsd"));
                settings.ValidationEventHandler += (_, e) => problems.Add($"{Path.GetFileName(file)} ({e.Exception.LineNumber}): {e.Message}");
                using var reader = XmlReader.Create(file, settings);
                while (reader.Read())
                {
                }
            }

            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }
    }

}
