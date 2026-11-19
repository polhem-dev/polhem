using System.Globalization;
using Polhem.Core;
using Polhem.Core.Serialization;
using Polhem.Definition.Language;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The packaged Language files a form's rule messages are translated in: one per language the solution already has
/// for the form, under the definition folder's <c>Language</c> folder. A rule's message is the English text of the
/// FormSchema, so an English file is left out, and no file is ever created.
/// </summary>
/// <remarks>
/// A message is written under <see cref="FormSchemaLocalizer.RuleMessageKeyFormat"/>, the key the server resolves when
/// the rule fails. A customization folder is never written: it overrides the packaged text per customer.
/// </remarks>
public static class RuleMessageFiles
{
    /// <summary>
    /// The Language files of <paramref name="progId"/> under <see cref="SolutionContext.DefinePath"/>, one per language
    /// other than English, ordered by language; empty when the definition folder is unknown.
    /// </summary>
    /// <param name="solution">The open solution.</param>
    /// <param name="progId">The form's ProgId, which is the Language file's namespace.</param>
    /// <returns>The files.</returns>
    public static IReadOnlyList<RuleMessageFile> Find(SolutionContext solution, string progId)
    {
        ArgumentNullException.ThrowIfNull(solution);
        if (string.IsNullOrEmpty(solution.DefinePath) || string.IsNullOrEmpty(progId)) return [];
        var root = Path.GetFullPath(Path.Combine(solution.DefinePath, "Language")) + Path.DirectorySeparatorChar;
        var files = new Dictionary<string, RuleMessageFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in solution.LanguagePaths)
        {
            if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (XmlCodec.DeserializeFromFile<LanguageResource>(path) is not { } resource
                    || !string.Equals(resource.Namespace, progId, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(resource.Lang)
                    || IsEnglish(resource.Lang))
                {
                    continue;
                }
                files.TryAdd(resource.Lang, new RuleMessageFile(resource.Lang, path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A file that cannot be read gets no translation; opening it reports the problem.
            }
        }
        return files.Values.OrderBy(f => f.Language, StringComparer.Ordinal).ToList();
    }

    /// <summary>The message of rule <paramref name="ruleId"/> in <paramref name="file"/>; empty when it has none.</summary>
    /// <param name="file">The Language file.</param>
    /// <param name="ruleId">The rule's id.</param>
    /// <returns>The translated message.</returns>
    public static string Read(RuleMessageFile file, string ruleId)
    {
        ArgumentNullException.ThrowIfNull(file);
        return XmlCodec.DeserializeFromFile<LanguageResource>(file.Path)?.GetText(Key(ruleId)) ?? string.Empty;
    }

    /// <summary>
    /// Writes the message of rule <paramref name="ruleId"/> into <paramref name="file"/>, replacing the one it has, and
    /// keeps the file's comments. An empty <paramref name="text"/> removes the message.
    /// </summary>
    /// <param name="file">The Language file.</param>
    /// <param name="ruleId">The rule's id.</param>
    /// <param name="text">The translated message.</param>
    /// <exception cref="InvalidOperationException">The file is not a Language file.</exception>
    public static void Write(RuleMessageFile file, string ruleId, string text)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        var original = FileUtilities.FileReadText(file.Path);
        var resource = XmlCodec.Deserialize<LanguageResource>(original)
            ?? throw new InvalidOperationException($"Not a Language file: {file.Path}");
        var key = Key(ruleId);
        if (resource.Items.GetOrDefault(key) is { } existing) resource.Items.Remove(existing);
        if (text.Length > 0) resource.Items.Add(key, text);
        var xml = XmlCommentCarrier.Carry(original, XmlCodec.Serialize(resource));
        if (original.EndsWith('\n') && !xml.EndsWith('\n')) xml += "\n";
        FileUtilities.FileWriteText(file.Path, xml);
    }

    private static string Key(string ruleId) =>
        string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.RuleMessageKeyFormat, ruleId);

    private static bool IsEnglish(string language) =>
        language.Equals("en", StringComparison.OrdinalIgnoreCase)
        || language.StartsWith("en-", StringComparison.OrdinalIgnoreCase);
}
