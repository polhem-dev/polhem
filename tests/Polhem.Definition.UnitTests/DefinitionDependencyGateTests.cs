using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Dependency gate: asserts that the **transitive dependency closure** of every assembly locked by <c>POLHEM9001</c>
    /// stays within the allowlist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// adr-036 set the criterion "does this give the definition layer an external package dependency", but at the time it was
    /// enforced by grepping for keywords by eye, so it missed <c>DynamicExpresso.Core</c>, which came in transitively through
    /// <c>Polhem.Expressions</c>. This test turns the criterion into an executable check: any new external dependency must be
    /// added to the allowlist explicitly, forcing a decision instead of passing silently.
    /// </para>
    /// <para>
    /// The data source is **this test assembly's own <c>.deps.json</c>**, not <c>Polhem.Definition.deps.json</c>, which is
    /// not copied to the test output directory. The <c>targets</c> section of deps.json records the direct dependency edges
    /// of every library, so a BFS from the <c>Polhem.Definition</c> node yields exactly its own closure, regardless of which
    /// other projects this test project references. deps.json is used instead of
    /// <see cref="Assembly.GetReferencedAssemblies"/> because the latter only reflects assemblies actually referenced by IL:
    /// a package dependency that is declared but not yet used would be missed, and that is exactly what this gate must catch.
    /// </para>
    /// <para>
    /// The BCL never appears in the closure: framework assemblies are resolved from the shared framework
    /// (Microsoft.NETCore.App) and are not in the deps.json library list. The same holds for build-time packages marked
    /// <c>PrivateAssets="all"</c> (SourceLink, analyzers).
    /// </para>
    /// <para>
    /// <b>Why all three roots are guarded.</b> <c>POLHEM9001</c> is enabled by a string comparison against three project
    /// names in <c>src/Directory.Build.targets</c>: if a project is renamed, or someone edits that file and drops one, the
    /// target silently stops running and <b>nothing turns red</b>. <c>Polhem.Base</c> used to have a backstop in practice
    /// (it is inside the closure of <c>Polhem.Definition</c>), but <c>Polhem.Api.Contracts</c> is <b>downstream</b> and
    /// outside every closure being observed, so its only guard was that name string. With all three listed as roots,
    /// this test still catches it when the build-time lock stops working.
    /// </para>
    /// <para>
    /// The allowlist exists both here and in <c>PolhemAllowedDependency</c> of <c>Directory.Build.targets</c>.
    /// <b>This is deliberate</b>: if one side is relaxed and the other is not, one of them turns red. The <c>POLHEM9001</c>
    /// error message already asks for changes in three places (the allowlist, this test, ADR-038); the friction is the
    /// point, forcing a decision instead of passing silently.
    /// </para>
    /// </remarks>
    public class DefinitionDependencyGateTests
    {
        /// <summary>
        /// The assemblies locked by <c>POLHEM9001</c>, and the assemblies and packages each may have in its transitive
        /// dependency closure.
        /// </summary>
        /// <remarks>
        /// Matches <c>PolhemAllowedDependency</c> in <c>src/Directory.Build.targets</c> entry by entry.
        /// The list for <c>Polhem.Base</c> is deliberately empty: it must not have any dependency that flows to consumers.
        /// <c>Microsoft.Extensions.Localization.Abstractions</c> (used by
        /// <c>Language/LanguageResourceStringLocalizer.cs</c>) is a Microsoft first-party pure abstraction package versioned with .NET.
        /// It carries no implementation and locks in no engine, so it is allowed. Third-party implementation packages must not
        /// appear here.
        /// </remarks>
        private static readonly Dictionary<string, string[]> s_lockedLibraries =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Polhem.Base"] = [],
                ["Polhem.Definition"] = ["Polhem.Base", "Microsoft.Extensions.Localization.Abstractions"],
                ["Polhem.Api.Contracts"] = ["Polhem.Definition", "Polhem.Base", "Microsoft.Extensions.Localization.Abstractions"],
            };

        public static TheoryData<string> LockedLibraries()
        {
            var data = new TheoryData<string>();
            foreach (var name in s_lockedLibraries.Keys) { data.Add(name); }
            return data;
        }

        [Theory]
        [MemberData(nameof(LockedLibraries))]
        [DisplayName("Transitive dependencies of the assemblies locked by POLHEM9001 stay within the allowlist")]
        public void TransitiveDependencies_StayWithinWhitelist(string rootLibrary)
        {
            var allowed = new HashSet<string>(s_lockedLibraries[rootLibrary], StringComparer.OrdinalIgnoreCase);
            var closure = ResolveDependencyClosure(rootLibrary);

            var unexpected = closure
                .Where(name => !allowed.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                unexpected.Length == 0,
                $"{rootLibrary} has transitive dependencies outside the allowlist: {string.Join(", ", unexpected)}. " +
                "Anything added at this layer is inherited by every consumer of the framework (the adr-038 criterion). If this is a deliberate decision, " +
                "add it to PolhemAllowedDependency in src/Directory.Build.targets and to the allowlist of this test, " +
                "and record the reason in ADR-038.");
        }

        [Fact]
        [DisplayName("The dependency gate actually walks the dependency edges of every locked assembly")]
        public void DependencyClosure_IsNotVacuous()
        {
            // If the node names in deps.json change and resolving the BFS root returns an empty set, the test above would pass
            // unconditionally. This test turns "the gate is looking at something" into an assertion as well.
            Assert.Equal(3, s_lockedLibraries.Count);
            Assert.Contains("Polhem.Base", ResolveDependencyClosure("Polhem.Definition"), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.Definition", ResolveDependencyClosure("Polhem.Api.Contracts"), StringComparer.OrdinalIgnoreCase);

            // The closure of `Polhem.Base` is empty (that is its constraint), so check that the node itself is in the graph.
            // Otherwise "zero dependencies outside the allowlist" would always hold because the node was never found.
            Assert.True(ReadDependencyGraph().ContainsKey("Polhem.Base"));
        }

        /// <summary>
        /// Reads the dependency graph from the test assembly's deps.json and returns the transitive dependency closure of
        /// <paramref name="rootLibrary"/> (excluding itself).
        /// </summary>
        /// <param name="rootLibrary">The name of the starting library, without the version.</param>
        private static HashSet<string> ResolveDependencyClosure(string rootLibrary)
        {
            var graph = ReadDependencyGraph();

            Assert.True(
                graph.ContainsKey(rootLibrary),
                $"Library {rootLibrary} was not found in deps.json, so the dependency gate cannot check it.");

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>(graph[rootLibrary]);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current)) { continue; }
                if (graph.TryGetValue(current, out var next))
                {
                    foreach (var dependency in next) { pending.Enqueue(dependency); }
                }
            }
            return visited;
        }

        /// <summary>
        /// Reads the test assembly's deps.json and returns the dependency graph as library name to direct dependency names.
        /// </summary>
        private static Dictionary<string, string[]> ReadDependencyGraph()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var depsPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.deps.json");
            Assert.True(File.Exists(depsPath), $"Dependency file not found: {depsPath}");

            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            // When a `RuntimeIdentifier` is specified there are two targets (RID-less and RID-specific). Taking the one with
            // the most entries gets the complete graph under both kinds of build.
            var target = document.RootElement
                .GetProperty("targets")
                .EnumerateObject()
                .OrderByDescending(entry => entry.Value.EnumerateObject().Count())
                .First()
                .Value;

            var graph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in target.EnumerateObject())
            {
                // The key has the format "Name/Version".
                var name = library.Name.Split('/')[0];
                graph[name] = library.Value.TryGetProperty("dependencies", out var dependencies)
                    ? dependencies.EnumerateObject().Select(entry => entry.Name).ToArray()
                    : [];
            }
            return graph;
        }
    }
}
