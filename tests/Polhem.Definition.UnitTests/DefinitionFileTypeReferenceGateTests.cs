using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Asserts that every assembly-qualified type name written in the repository's definition files
    /// names a project that exists and a type that project declares.
    /// </summary>
    /// <remarks>
    /// These names are plain attribute values, such as <c>BusinessObject="Ns.Type, Assembly"</c> in
    /// <c>ProgramSettings.xml</c>. The compiler does not see them and the unit tests do not load the
    /// sample or app definitions, so a wrong name only surfaces when that program is run and the
    /// entry is used. This test checks the source tree rather than loading the assemblies, because
    /// the test project does not reference the samples and apps that declare some of the types.
    /// </remarks>
    public class DefinitionFileTypeReferenceGateTests
    {
        private static readonly Regex s_typeReference = new(
            "=\"(?<type>[A-Za-z_][A-Za-z0-9_.]*), (?<assembly>[A-Za-z_][A-Za-z0-9_.]*)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        [Fact]
        [DisplayName("Assembly-qualified type names in definition files resolve to existing projects and types")]
        public void DefinitionFiles_TypeReferencesResolveToDeclaredTypes()
        {
            var root = FindRepositoryRoot();
            var references = EnumerateDefinitionFiles(root)
                .SelectMany(file => s_typeReference.Matches(File.ReadAllText(file))
                    .Select(m => (File: Path.GetRelativePath(root, file),
                                  Type: m.Groups["type"].Value,
                                  Assembly: m.Groups["assembly"].Value)))
                .ToList();

            // Guards against a search that silently finds nothing (a moved folder, a changed regex),
            // which would let the loop below pass without checking anything.
            Assert.NotEmpty(references);

            var failures = references
                .Where(r => !IsDeclared(root, r.Type, r.Assembly))
                .Select(r => $"{r.File}: {r.Type}, {r.Assembly}")
                .ToList();

            Assert.True(failures.Count == 0,
                "Type references with no matching project or declaration:" + Environment.NewLine
                + string.Join(Environment.NewLine, failures));
        }

        private static IEnumerable<string> EnumerateDefinitionFiles(string root)
            => Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
                    return parts.Contains("Define", StringComparer.Ordinal)
                        && !parts.Contains("bin", StringComparer.Ordinal)
                        && !parts.Contains("obj", StringComparer.Ordinal);
                });

        private static bool IsDeclared(string root, string fullTypeName, string assemblyName)
        {
            var project = Directory.EnumerateFiles(root, assemblyName + ".csproj", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (project == null) { return false; }

            var lastDot = fullTypeName.LastIndexOf('.');
            var ns = fullTypeName[..lastDot];
            var typeName = fullTypeName[(lastDot + 1)..];
            var namespacePattern = new Regex(
                @"^\s*namespace\s+" + Regex.Escape(ns) + @"\s*[;{\r\n]",
                RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var declarationPattern = new Regex(
                @"\b(class|record|struct)\s+" + Regex.Escape(typeName) + @"\b",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

            return Directory.EnumerateFiles(Path.GetDirectoryName(project)!, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Any(text => namespacePattern.IsMatch(text) && declarationPattern.IsMatch(text));
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && dir.GetDirectories(".git").Length == 0)
            {
                dir = dir.Parent;
            }
            Assert.True(dir != null, "No repository root (.git) above the test output directory.");
            return dir!.FullName;
        }
    }
}
