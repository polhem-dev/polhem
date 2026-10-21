using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Polhem.Tests.Shared;
using Polhem.UI.Avalonia.Shell.Localization;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class ShellStringsGateTests
{
    private static readonly ResourceManager s_resources = new(
        "Polhem.UI.Avalonia.Shell.Resources.ShellStrings", typeof(ShellLanguage).Assembly);

    [Fact]
    [DisplayName("Every offered language translates exactly the keys of the English resources")]
    public void Translations_HaveTheSameKeysAsEnglish()
    {
        var english = Keys(CultureInfo.InvariantCulture);
        Assert.NotEmpty(english);

        foreach (var option in ShellLanguage.Options.Where(o => o.Code != ShellLanguage.English))
        {
            var translated = Keys(CultureInfo.GetCultureInfo(option.Code));
            Assert.Equal(english, translated);
        }
    }

    [Fact]
    [DisplayName("Every key the shell's XAML and code names exists in the English resources")]
    public void SourceKeys_ExistInEnglish()
    {
        var english = Keys(CultureInfo.InvariantCulture);
        var sourceRoot = Path.Combine(RepoRoot.Find(), "src", "Polhem.UI.Avalonia.Shell");
        var used = Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".axaml", StringComparison.Ordinal) || p.EndsWith(".cs", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(p => System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(p),
                @"ShellText (\w+)\}|""((?:Shell|Connection|Login|Forms|Status)_\w+)""",
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(1)))
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(english));
    }

    private static SortedSet<string> Keys(CultureInfo culture)
    {
        var set = s_resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"No resources for '{culture.Name}'.");
        return new SortedSet<string>(
            set.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key),
            StringComparer.Ordinal);
    }
}
