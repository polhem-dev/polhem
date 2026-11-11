using System.ComponentModel;
using System.Text.RegularExpressions;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Every catch that takes any exception in <c>src/</c> has one of the shapes <c>.claude/rules/scanning.md</c> allows: it
    /// ends with <c>throw;</c>, or its first line is a <c>// Boundary:</c> comment that says why nothing above it can take
    /// the exception. An empty catch-all always fails.
    /// </summary>
    /// <remarks>
    /// A catch-all hides the exceptions nobody expected. The rule once said "never", while the code held dozens that were
    /// needed, and the two stopped constraining each other: new ones were copied without a reason and without passing a
    /// cancellation on. A catch naming a type, or filtered with <c>when</c> on the types it takes, is not a catch-all and
    /// is not checked. The test checks the shape only: whether a boundary passes its own cancellation on is reviewed by hand.
    /// </remarks>
    public class CatchAllBoundaryGateTests
    {
        // `catch`, `catch (Exception)`, `catch (System.SystemException ex)` and the like, a comment before the brace, and a
        // `when` filter that begins by excluding (`ex is not`, `!`), which still takes every other exception.
        private static readonly Regex s_catchAll = new(
            @"\bcatch\s*(\(\s*(global::)?(System\.)?(System)?Exception(\s+\w+)?\s*\))?\s*(//[^\n]*\s*)*(when\s*\(\s*(\w+\s+is\s+not\b|!)[^{]*)?\{",
            RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex s_lineComment = new(@"//[^\n]*", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex s_blockComment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromSeconds(1));

        // Files whose catch-alls another change owns, with the reason. Remove an entry when that change lands.
        private static readonly string[] s_exempt =
        [
            // The batch runners wrap every failure, a cancellation included, in an InvalidOperationException; the health
            // plan's P2-6 changes what they catch.
            "src/Polhem.Db/DbAccess.cs",
            "src/Polhem.Db/DbAccess.Async.cs",
        ];

        [Fact]
        [DisplayName("Every catch-all in src ends with throw; or starts with a // Boundary: reason, and none is empty")]
        public void CatchAlls_HaveAnAllowedShape()
        {
            var root = RepoRoot.Find();
            var found = 0;
            var knownSiteFound = false;
            var violations = new List<string>();
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal)) continue;
                if (s_exempt.Contains(relative, StringComparer.Ordinal)) continue;

                var text = File.ReadAllText(path);
                foreach (Match match in s_catchAll.Matches(text))
                {
                    found++;
                    if (relative.EndsWith("Schema/TableUpgradeOrchestrator.cs", StringComparison.Ordinal)) knownSiteFound = true;
                    var body = BlockBody(text, match.Index + match.Length - 1);
                    var line = text.AsSpan(0, match.Index).Count('\n') + 1;
                    var problem = Check(body);
                    if (problem is not null) violations.Add($"{relative}:{line}: {problem}");
                }
            }

            Assert.True(found > 0, "No catch-all was found, so the scan is not reading the sources.");
            Assert.True(knownSiteFound, "The catch-all in TableUpgradeOrchestrator was not found, so the pattern no longer matches.");
            Assert.True(violations.Count == 0,
                "Catch-alls without an allowed shape (see .claude/rules/scanning.md):" + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }

        private static string? Check(string body)
        {
            var code = s_lineComment.Replace(s_blockComment.Replace(body, string.Empty), string.Empty).Trim();
            if (code.Length == 0) return "empty catch-all";
            if (code.EndsWith("throw;", StringComparison.Ordinal)) return null;
            return body.TrimStart().StartsWith("// Boundary:", StringComparison.Ordinal)
                ? null
                : "neither ends with throw; nor starts with a // Boundary: reason";
        }

        // The text between the brace at `open` and its matching closing brace. Braces inside strings and comments are rare
        // in a catch block, and a miscount usually shows as a violation that names the line.
        private static string BlockBody(string text, int open)
        {
            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) return text.Substring(open + 1, i - open - 1);
            }
            return text[(open + 1)..];
        }
    }
}
