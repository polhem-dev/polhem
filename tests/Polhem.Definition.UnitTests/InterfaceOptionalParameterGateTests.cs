using System.ComponentModel;
using System.Text.RegularExpressions;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// No member of a public interface in <c>src/</c> takes an optional parameter, except a trailing
    /// <see cref="CancellationToken"/> and the members listed in <see cref="s_allowed"/>, which predate the rule.
    /// </summary>
    /// <remarks>
    /// Adding a parameter to an interface member breaks every implementer, and adding an overload next to optional
    /// parameters is blocked by RS0026 and RS0027, so a member that ends in optional parameters cannot take another input
    /// without a breaking change. A member with more than one optional input takes a parameter object instead
    /// (<c>.claude/rules/code-style.md</c>). The list only shrinks: the test fails when an entry no longer matches a
    /// member, so a member that is fixed leaves the list in the same change.
    /// </remarks>
    public class InterfaceOptionalParameterGateTests
    {
        // Interface.Member, for the members that had optional parameters when the rule was introduced.
        private static readonly string[] s_allowed =
        [
            "IFormApiConnector.GetListAsync",
            "IFormApiConnector.GetLookupAsync",
            "IExpressionEvaluator.Evaluate",
            "IDataFormRepository.GetList",
            "IDataFormRepository.GetNewData",
            "IDataFormRepository.GetData",
            "IDataFormRepository.Delete",
            "IFormRuleProcessor.ApplyNewRowDefaults",
            "IFormRuleProcessor.ApplyBeforeSave",
            "IFormRuleProcessor.ApplyBeforeDelete",
            "IDefineAccess.GetDefine",
            "IDefineAccess.SaveDefine",
            "IRepositoryFactory.Create",
        ];

        private static readonly Regex s_interface = new(
            @"\bpublic\s+(?:partial\s+)?interface\s+(\w+)[^{]*\{", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        // A member's name, optional generic arguments and parameter list (one level of nested parentheses), then `;`.
        // Bodies are replaced by `;` first, so a default interface method counts as much as an abstract one and the
        // calls inside its body are not taken for members.
        private static readonly Regex s_member = new(
            @"(\w+)\s*(?:<[^>()]*>)?\s*\(((?:[^;{}()]|\([^()]*\))*)\)\s*(?:where[^;{=]*)?;",
            RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex s_innermostBlock = new(@"\{[^{}]*\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex s_expressionBody = new(@"=>[^;]*;", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex s_comment = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));

        // An attribute list such as `[SuppressMessage("…", Justification = "…")]`, whose `=` is not a default value.
        private static readonly Regex s_attribute = new(@"\[(?:[^\[\]""]|""[^""]*"")*\]", RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

        [Fact]
        [DisplayName("No public interface member in src takes an optional parameter other than a CancellationToken")]
        public void InterfaceMembers_TakeNoOptionalParameters()
        {
            var withOptional = FindMembersWithOptionalParameters(out var scanned);

            Assert.True(scanned > 0, "No interface member was found, so the scan is not reading the sources.");
            var violations = withOptional.Where(m => !s_allowed.Contains(m.Key, StringComparer.Ordinal))
                .Select(m => $"{m.Value}: {m.Key}").ToList();
            Assert.True(violations.Count == 0,
                "Interface members with optional parameters (take a parameter object instead, see .claude/rules/code-style.md):"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        [Fact]
        [DisplayName("Every member on the optional-parameter allowlist still has an optional parameter, so the list only shrinks")]
        public void Allowlist_HasNoStaleEntries()
        {
            var withOptional = FindMembersWithOptionalParameters(out _);

            var stale = s_allowed.Where(entry => !withOptional.ContainsKey(entry)).ToList();
            Assert.True(stale.Count == 0,
                "Remove these entries from the allowlist; they no longer take an optional parameter:" + Environment.NewLine
                + string.Join(Environment.NewLine, stale));
        }

        [Fact]
        [DisplayName("The scan reports the members this rule was made for and passes the shapes that only look like them")]
        public void Scan_RecognisesOptionalParameters()
        {
            // The two declarations as they were before the rule, with the shapes that must not count around them: an
            // attribute with a named argument, a default interface method body, and a trailing CancellationToken.
            const string source = """
                public interface IDatabaseInitializer
                {
                    TableUpgradeReport UpgradeTables(
                        UpgradeOptions? options = null, bool dryRun = false, Action<TableUpgradeEntry>? tableCompleted = null,
                        IReadOnlyCollection<string>? tableNames = null);
                }

                public interface IFormCommandBuilder
                {
                    DbCommandSpec BuildSelect(string tableName, string selectFields, FilterNode? filter = null,
                        SortFieldCollection? sortFields = null, int? skip = null, int? take = null);

                    [SuppressMessage("Design", "CA1000", Justification = "kept")]
                    DbCommandSpec BuildDelete(string tableName, FilterNode filter);

                    DbCommandSpec BuildCount(string tableName) => BuildCount(tableName, filter: null);

                    Task<int> CountAsync(string tableName, CancellationToken cancellationToken = default);

                    Type Resolve<T>(string progId, Func<int, int> map) where T : class;
                }
                """;

            var found = Scan(source, "sample.cs", out var scanned);

            Assert.Equal(6, scanned);
            Assert.Equal(["IDatabaseInitializer.UpgradeTables", "IFormCommandBuilder.BuildSelect"], found.Keys.Order());
        }

        private static Dictionary<string, string> FindMembersWithOptionalParameters(out int scanned)
        {
            var root = RepoRoot.Find();
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            scanned = 0;
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal)) continue;

                var members = Scan(File.ReadAllText(path), relative, out var count);
                scanned += count;
                foreach (var (member, file) in members)
                    found.TryAdd(member, file);
            }
            return found;
        }

        private static Dictionary<string, string> Scan(string source, string file, out int scanned)
        {
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            scanned = 0;
            var text = s_comment.Replace(source, string.Empty);
            foreach (Match declaration in s_interface.Matches(text))
            {
                var body = WithoutBodies(s_attribute.Replace(
                    BlockBody(text, declaration.Index + declaration.Length - 1), string.Empty));
                foreach (Match member in s_member.Matches(body))
                {
                    scanned++;
                    if (HasOptionalParameter(member.Groups[2].Value))
                        found.TryAdd($"{declaration.Groups[1].Value}.{member.Groups[1].Value}", file);
                }
            }
            return found;
        }

        private static string WithoutBodies(string interfaceBody)
        {
            var text = s_expressionBody.Replace(interfaceBody, ";");
            string previous;
            do
            {
                previous = text;
                text = s_innermostBlock.Replace(text, ";");
            }
            while (text != previous);
            return text;
        }

        private static bool HasOptionalParameter(string parameterList) =>
            SplitTopLevel(parameterList).Any(parameter => parameter.Contains('=')
                && !Regex.IsMatch(parameter, @"^\s*(?:System\.Threading\.)?CancellationToken\s", RegexOptions.None,
                    TimeSpan.FromSeconds(1)));

        private static IEnumerable<string> SplitTopLevel(string list)
        {
            var depth = 0;
            var start = 0;
            for (var i = 0; i < list.Length; i++)
            {
                switch (list[i])
                {
                    case '<' or '(' or '[': depth++; break;
                    case '>' or ')' or ']': depth--; break;
                    case ',' when depth == 0:
                        yield return list[start..i];
                        start = i + 1;
                        break;
                }
            }
            if (list.Trim().Length > 0) yield return list[start..];
        }

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
