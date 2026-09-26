using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Asserts that the Blazor demo's stylesheet only targets class names that some component
    /// actually renders.
    /// </summary>
    /// <remarks>
    /// The components ship no CSS of their own; the class names on their markup are the contract a
    /// consuming application styles against, and the demo stylesheet is the one consumer in this
    /// repository. A selector that no markup carries matches nothing and fails silently, which is
    /// what a class renamed on one side only looks like.
    /// </remarks>
    public class CssClassNameGateTests
    {
        private static readonly Regex s_selector = new(@"\.(polhem-[A-Za-z0-9_-]+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex s_markupClass = new(@"polhem-[A-Za-z0-9_-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        [Fact]
        [DisplayName("Blazor Demo 樣式表的 polhem- 選擇器都對應到元件標記中的 class")]
        public void DemoStylesheet_SelectorsMatchRenderedClasses()
        {
            var root = FindRepositoryRoot();
            var stylesheet = File.ReadAllText(Path.Combine(root, "samples", "Blazor.Server.Demo", "wwwroot", "css", "site.css"));
            var selectors = s_selector.Matches(stylesheet).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

            var rendered = new[]
                {
                    Path.Combine(root, "src", "Polhem.Web.Blazor.Server"),
                    Path.Combine(root, "samples", "Blazor.Server.Demo"),
                }
                .SelectMany(dir => Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories))
                .SelectMany(file => s_markupClass.Matches(File.ReadAllText(file)).Select(m => m.Value))
                .ToHashSet(StringComparer.Ordinal);

            // Guards against a stylesheet or a search that yields nothing, which would pass vacuously.
            Assert.NotEmpty(selectors);

            var unmatched = selectors.Where(s => !rendered.Contains(s)).OrderBy(s => s, StringComparer.Ordinal).ToList();
            Assert.True(unmatched.Count == 0, "Selectors with no matching class in any component: " + string.Join(", ", unmatched));
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
