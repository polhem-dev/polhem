using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Polhem.Tests.Shared;

namespace Polhem.Analyzers.UnitTests
{
    /// <summary>
    /// No member of an interface that <c>src/</c> exposes to other assemblies takes an optional parameter, except a
    /// <see cref="CancellationToken"/> and the members in <see cref="s_allowed"/>, which predate the rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Adding a parameter to an interface member breaks every implementer, and an overload next to optional parameters is
    /// blocked by RS0026 and RS0027, so a member that ends in optional parameters cannot take another input without a
    /// breaking change. A member with more than one optional input takes a parameter object instead
    /// (<c>.claude/rules/code-style.md</c>).
    /// </para>
    /// <para>
    /// The sources are parsed, not matched as text, so strings, comments, attributes, indexers and default method bodies
    /// read as the compiler reads them. Exposure is read from the declared modifiers: an interface counts when it and
    /// every type that contains it are <c>public</c> or <c>protected</c>. Methods and indexers are checked; a delegate
    /// declared inside an interface is not.
    /// </para>
    /// <para>
    /// An allowlist entry names the member with its namespace and parameter types, so a new overload or a namesake
    /// interface cannot pass as an allowed member. The list only shrinks: an entry that no longer matches fails
    /// <see cref="Allowlist_HasNoStaleEntries"/>.
    /// </para>
    /// </remarks>
    public class InterfaceOptionalParameterGateTests
    {
        private static readonly string[] s_allowed =
        [
            "Polhem.Api.Contracts.Form.IFormApiConnector.GetListAsync(string, FilterNode?, SortFieldCollection?, PagingOptions?, CancellationToken)",
            "Polhem.Api.Contracts.Form.IFormApiConnector.GetLookupAsync(string, PagingOptions?, CancellationToken)",
            "Polhem.Business.Form.IFormRuleProcessor.ApplyBeforeDelete(FormSchema, DataSet, string)",
            "Polhem.Business.Form.IFormRuleProcessor.ApplyBeforeSave(FormSchema, DataSet, RoundingContext, string)",
            "Polhem.Business.Form.IFormRuleProcessor.ApplyNewRowDefaults(FormSchema, DataSet, string)",
            "Polhem.Core.Expressions.IExpressionEvaluator.Evaluate(string, IReadOnlyDictionary<string, object?>, string, DateTimeBasis)",
            "Polhem.Core.Expressions.IExpressionEvaluator.Evaluate(string, IReadOnlyDictionary<string, object?>, Type, string, DateTimeBasis)",
            "Polhem.Definition.Storage.IDefineAccess.GetDefine(DefineType, string[]?)",
            "Polhem.Definition.Storage.IDefineAccess.SaveDefine(DefineType, object, string[]?)",
            "Polhem.Repository.Abstractions.Factories.IRepositoryFactory.Create(Guid)",
            "Polhem.Repository.Abstractions.Form.IDataFormRepository.Delete(Guid, FilterNode?)",
            "Polhem.Repository.Abstractions.Form.IDataFormRepository.GetData(Guid, FilterNode?)",
            "Polhem.Repository.Abstractions.Form.IDataFormRepository.GetList(string, FilterNode?, SortFieldCollection?, PagingOptions?)",
            "Polhem.Repository.Abstractions.Form.IDataFormRepository.GetNewData(string)",
        ];

        [Fact]
        [DisplayName("No exposed interface member in src takes an optional parameter other than a CancellationToken")]
        public void InterfaceMembers_TakeNoOptionalParameters()
        {
            var found = ScanSources(out var scanned);

            Assert.True(scanned > 0, "No interface member was found, so the scan is not reading the sources.");
            var violations = found.Where(m => !s_allowed.Contains(m.Key, StringComparer.Ordinal))
                .Select(m => $"{m.Value}: \"{m.Key}\",").ToList();
            Assert.True(violations.Count == 0,
                "Interface members with optional parameters (take a parameter object instead, see .claude/rules/code-style.md):"
                + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        [Fact]
        [DisplayName("Every member on the optional-parameter allowlist still has an optional parameter, so the list only shrinks")]
        public void Allowlist_HasNoStaleEntries()
        {
            var found = ScanSources(out _);

            var stale = s_allowed.Where(entry => !found.ContainsKey(entry)).ToList();
            Assert.True(stale.Count == 0,
                "Remove these entries from the allowlist; they no longer take an optional parameter:" + Environment.NewLine
                + string.Join(Environment.NewLine, stale));
        }

        [Fact]
        [DisplayName("The scan reports the members this rule was made for and passes the shapes that only look like them")]
        public void Scan_RecognisesOptionalParameters()
        {
            // The two declarations the rule was made for, as they were, among shapes a text match got wrong: strings with
            // `;`, braces and `//`, an attribute with a named argument, a default method body, a nested tuple, an indexer,
            // nested interfaces of every exposure, and an overload of an allowed name.
            const string source = """
                namespace Sample.Db;

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

                    [SuppressMessage("Design", "CA1000", Justification = "kept; {see} http://example")]
                    DbCommandSpec BuildDelete(string tableName, FilterNode filter);

                    DbCommandSpec BuildCount(string tableName) => BuildCount(tableName, filter: null);

                    string Join(string[] parts, string separator = ";");

                    string Format(string text, string pattern = "{0} // }");

                    Task<int> CountAsync(string tableName, CancellationToken cancellationToken = default);

                    int this[int index, int offset = 0] { get; }

                    void Pair(((int, int), int) pair = default);
                }

                internal interface IHidden
                {
                    void Hidden(int value = 0);
                }

                internal class Host
                {
                    public interface INestedInInternal
                    {
                        void Nested(int value = 0);
                    }
                }

                public class Owner
                {
                    protected interface IProtectedNested
                    {
                        void Nested(int value = 0);
                    }
                }
                """;

            var found = Scan(source, "sample.cs", out var scanned);

            Assert.Equal(10, scanned);
            Assert.Equal(
            [
                "Sample.Db.IDatabaseInitializer.UpgradeTables(UpgradeOptions?, bool, Action<TableUpgradeEntry>?, IReadOnlyCollection<string>?)",
                "Sample.Db.IFormCommandBuilder.BuildSelect(string, string, FilterNode?, SortFieldCollection?, int?, int?)",
                "Sample.Db.IFormCommandBuilder.Format(string, string)",
                "Sample.Db.IFormCommandBuilder.Join(string[], string)",
                "Sample.Db.IFormCommandBuilder.Pair(((int, int), int))",
                "Sample.Db.IFormCommandBuilder.this[int, int]",
                "Sample.Db.Owner.IProtectedNested.Nested(int)",
            ], found.Keys.Order(StringComparer.Ordinal));
        }

        private static Dictionary<string, string> ScanSources(out int scanned)
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
            var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
            foreach (var declaration in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Where(IsExposed))
            {
                foreach (var member in declaration.Members)
                {
                    var (name, parameters) = member switch
                    {
                        MethodDeclarationSyntax method => (method.Identifier.ValueText, method.ParameterList.Parameters),
                        IndexerDeclarationSyntax indexer => ("this", indexer.ParameterList.Parameters),
                        _ => (null, default(SeparatedSyntaxList<ParameterSyntax>)),
                    };
                    if (name is null) continue;
                    scanned++;
                    if (!parameters.Any(IsOptional)) continue;

                    var types = string.Join(", ", parameters.Select(p => p.Type?.ToString() ?? string.Empty));
                    var signature = name == "this" ? $"this[{types}]" : $"{name}({types})";
                    found.TryAdd($"{QualifiedName(declaration)}.{signature}", file);
                }
            }
            return found;
        }

        private static bool IsOptional(ParameterSyntax parameter) =>
            parameter.Default is not null
            && parameter.Type?.ToString() is not ("CancellationToken" or "System.Threading.CancellationToken");

        // An interface is exposed when it and every type around it are declared public or protected; a top-level type
        // with no modifier is internal, and `private protected` is not visible outside the assembly.
        private static bool IsExposed(InterfaceDeclarationSyntax declaration) =>
            declaration.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().All(type =>
                (type.Modifiers.Any(SyntaxKind.PublicKeyword) || type.Modifiers.Any(SyntaxKind.ProtectedKeyword))
                && !type.Modifiers.Any(SyntaxKind.PrivateKeyword));

        private static string QualifiedName(InterfaceDeclarationSyntax declaration)
        {
            var parts = declaration.AncestorsAndSelf().Select(node => node switch
            {
                BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                BaseNamespaceDeclarationSyntax ns => ns.Name.ToString(),
                _ => null,
            }).Where(part => part is not null).Reverse();
            return string.Join(".", parts);
        }
    }
}
