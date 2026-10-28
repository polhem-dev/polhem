using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Polhem.Templates.UnitTests;

public class TemplateLocalizationTests
{
    private static readonly JsonDocumentOptions s_jsonOptions = new() { CommentHandling = JsonCommentHandling.Skip };

    private static string SourcesRoot => Path.Combine(TemplatePaths.RepositoryRoot, "tests", "Polhem.Templates.UnitTests", "TranslationSources");

    [Fact]
    [DisplayName("Every template has at least one translation of its strings")]
    public void Templates_HaveTranslations()
    {
        var templates = TemplatePaths.Templates.Select(TemplatePaths.TemplateDirectory).ToList();

        Assert.NotEmpty(templates);
        Assert.All(templates, t => Assert.NotEmpty(TranslationFiles(t)));
    }

    // Microsoft.TemplateEngine.Authoring.Tasks adds and removes keys when template.json changes, but it keeps the
    // value of a key it already has. So when the English of a string changes, the translation silently stays the
    // old one. The sources record the English each translation was made from, and this test catches the change.
    [Fact]
    [DisplayName("Each translated template string was made from the English template.json has now")]
    public void Translations_MatchTheEnglishTheyWereMadeFrom()
    {
        var problems = new StringBuilder();
        foreach (var template in TemplatePaths.Templates.Select(TemplatePaths.TemplateDirectory))
        {
            var templateJson = Path.Combine(template, ".template.config", "template.json");
            using var english = JsonDocument.Parse(File.ReadAllText(templateJson), s_jsonOptions);

            foreach (var translationFile in TranslationFiles(template))
            {
                // templatestrings.<language>.json
                var language = Path.GetFileNameWithoutExtension(translationFile)["templatestrings.".Length..];
                var sourcesFile = Path.Combine(SourcesRoot, $"{Path.GetFileName(template)}.{language}.json");
                var translation = ReadStrings(translationFile);
                var sources = File.Exists(sourcesFile) ? ReadStrings(sourcesFile) : [];

                foreach (var key in translation.Keys)
                {
                    var current = ResolveString(english.RootElement, key);
                    if (!sources.TryGetValue(key, out var source) || !string.Equals(source, current, StringComparison.Ordinal))
                        problems.AppendLine(CultureInfo.InvariantCulture, $"  {Relative(translationFile)}: \"{key}\" — English now: \"{current}\", translated from: \"{source}\"");
                }
                foreach (var key in sources.Keys.Except(translation.Keys, StringComparer.Ordinal))
                    problems.AppendLine(CultureInfo.InvariantCulture, $"  {Relative(sourcesFile)}: \"{key}\" is no longer a template string");
            }
        }

        Assert.True(problems.Length == 0,
            "These template strings have no translation made from their current English. Update the translation, " +
            "then record the current English as its source in tests/Polhem.Templates.UnitTests/TranslationSources/" +
            "<template>.<language>.json (remove the keys reported as no longer a template string):"
            + Environment.NewLine + problems);
    }

    private static IEnumerable<string> TranslationFiles(string template)
    {
        var localize = Path.Combine(template, ".template.config", "localize");
        return Directory.Exists(localize) ? Directory.GetFiles(localize, "templatestrings.*.json") : [];
    }

    private static Dictionary<string, string> ReadStrings(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;

    private static string Relative(string path) => Path.GetRelativePath(TemplatePaths.RepositoryRoot, path);

    // A key is a path into template.json. In an array, a segment names the element whose `choice` or `id` equals it,
    // and `default` names the element without an `id` (the form manualInstructions takes).
    private static string? ResolveString(JsonElement root, string key)
    {
        var current = root;
        foreach (var segment in key.Split('/'))
        {
            JsonElement? next = current.ValueKind switch
            {
                JsonValueKind.Object => current.TryGetProperty(segment, out var property) ? property : null,
                JsonValueKind.Array => current.EnumerateArray()
                    .Where(e => NamesElement(e, segment))
                    .Select(e => (JsonElement?)e)
                    .FirstOrDefault(),
                _ => null,
            };
            if (next is null) return null;
            current = next.Value;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static bool NamesElement(JsonElement element, string segment)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (element.TryGetProperty("choice", out var choice)) return choice.GetString() == segment;
        if (element.TryGetProperty("id", out var id)) return id.GetString() == segment;
        return segment == "default";
    }
}
