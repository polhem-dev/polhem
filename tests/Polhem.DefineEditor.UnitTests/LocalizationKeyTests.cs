using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Polhem.DefineEditor.Services;
using Polhem.Tests.Shared;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The keys the views look up through <see cref="Polhem.DefineEditor.Markup.LocExtension"/> exist in the string
    /// resources. A missing key is not a build error: <see cref="LocalizationService"/> shows the key itself.
    /// </summary>
    public class LocalizationKeyTests
    {
        private static readonly Regex s_locKey = new(@"\{loc:Loc\s+(?:Key=)?([A-Za-z0-9_]+)\s*\}",
            RegexOptions.None, TimeSpan.FromSeconds(1));

        [Fact]
        [DisplayName("Every {loc:Loc} key in the DefineEditor views has an English string")]
        public void ViewLocKeys_ExistInStrings()
        {
            var editor = Path.Combine(RepoRoot.Find(), "tools", "DefineEditor");
            var keys = Directory.EnumerateFiles(editor, "*.axaml", SearchOption.AllDirectories)
                .Where(f => !IsBuildOutput(editor, f))
                .SelectMany(f => s_locKey.Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Key: m.Groups[1].Value)))
                .ToList();
            var resources = new ResourceManager("Polhem.DefineEditor.Resources.Strings", typeof(LocalizationService).Assembly);

            var missing = keys
                .Where(k => resources.GetString(k.Key, CultureInfo.InvariantCulture) is null)
                .Select(k => $"{k.File}: {k.Key}")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Anti-vacuous: the scan must still find the views' keys.
            Assert.True(keys.Count > 100, $"Only {keys.Count} keys were found.");
            Assert.True(missing.Count == 0,
                "Add these to tools/DefineEditor/Resources/Strings.resx and Strings.zh-TW.resx:"
                + Environment.NewLine + string.Join(Environment.NewLine, missing));
        }

        private static bool IsBuildOutput(string editor, string file)
        {
            var first = Path.GetRelativePath(editor, file).Split(Path.DirectorySeparatorChar)[0];
            return first is "bin" or "obj";
        }
    }
}
