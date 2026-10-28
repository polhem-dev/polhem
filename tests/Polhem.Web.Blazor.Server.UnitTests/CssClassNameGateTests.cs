using System.ComponentModel;
using System.Text.RegularExpressions;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Asserts that the package's stylesheet and the Blazor demo's stylesheet only target class names
    /// that some component actually renders.
    /// </summary>
    /// <remarks>
    /// The class names on the components' markup are the contract a stylesheet styles against: the
    /// package's optional <c>polhem.css</c>, and the demo's own sheet, which does not link it. A selector
    /// that no markup carries matches nothing and fails silently, which is what a class renamed on one
    /// side only looks like. Class names written in C# code-behind count as markup, because the field
    /// editors build their class attribute there.
    /// </remarks>
    public class CssClassNameGateTests
    {
        private static bool IsBuildOutput(string path)
            => path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        private static readonly Regex s_selector = new(@"\.(polhem-[A-Za-z0-9_-]+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex s_markupClass = new(@"polhem-[A-Za-z0-9_-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        [Fact]
        [DisplayName("Every polhem- selector in the Blazor demo stylesheet matches a class in the component markup")]
        public void DemoStylesheet_SelectorsMatchRenderedClasses()
            => AssertSelectorsMatchRenderedClasses("samples", "Blazor.Server.Demo", "wwwroot", "css", "site.css");

        [Fact]
        [DisplayName("Every polhem- selector in the package stylesheet matches a class in the component markup")]
        public void PackageStylesheet_SelectorsMatchRenderedClasses()
            => AssertSelectorsMatchRenderedClasses("src", "Polhem.Web.Blazor.Server", "wwwroot", "polhem.css");

        private static void AssertSelectorsMatchRenderedClasses(params string[] stylesheetPath)
        {
            var root = RepoRoot.Find();
            var stylesheet = File.ReadAllText(Path.Combine([root, .. stylesheetPath]));
            var selectors = s_selector.Matches(stylesheet).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

            var rendered = new[]
                {
                    Path.Combine(root, "src", "Polhem.Web.Blazor.Server"),
                    Path.Combine(root, "samples", "Blazor.Server.Demo"),
                }
                // Code-behind counts too: an editor composes its class attribute in C#
                // (`FieldEditorBase.BuildCssClass`), so the read-only modifier exists only there.
                .SelectMany(dir => Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                        .Where(file => !IsBuildOutput(file))))
                .SelectMany(file => s_markupClass.Matches(File.ReadAllText(file)).Select(m => m.Value))
                .ToHashSet(StringComparer.Ordinal);

            // Guards against a stylesheet or a search that yields nothing, which would pass vacuously.
            Assert.NotEmpty(selectors);

            var unmatched = selectors.Where(s => !rendered.Contains(s)).OrderBy(s => s, StringComparer.Ordinal).ToList();
            Assert.True(unmatched.Count == 0, "Selectors with no matching class in any component: " + string.Join(", ", unmatched));
        }
    }
}
