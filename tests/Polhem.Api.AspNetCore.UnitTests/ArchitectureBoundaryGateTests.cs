using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Api.AspNetCore.UnitTests
{
    /// <summary>
    /// Turns the hard constraints of the architecture documents into executable checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These constraints are the core of the framework's layering claims (see
    /// <c>docs/en/development-constraints.md</c> and <c>docs/en/dependency-map.md</c>). They used to be confirmed
    /// <b>only by a person or agent rescanning during each health check</b>, while the ADR-038 edge had two
    /// gates. Health checks run a few times a year, so weeks passed between a violation reaching main and its
    /// discovery.
    /// </para>
    /// <para>
    /// <b>Why it lives in this test project.</b> The assertions read the test assembly's own <c>.deps.json</c>,
    /// so the nodes visible in the graph are the nodes in this project's transitive closure.
    /// <c>Polhem.Api.AspNetCore.UnitTests</c> is the only project that sees both the whole backend (through
    /// <c>Polhem.Api.AspNetCore</c> → <c>Polhem.Hosting</c>) and <c>Polhem.Api.Client</c> (through
    /// <c>Polhem.Tests.Shared</c>). The latter is essential: **the forbidden node must exist in the graph**,
    /// otherwise "not in the closure" is always true simply because the node is not in the graph at all.
    /// </para>
    /// <para>
    /// The technique follows <c>DefinitionDependencyGateTests</c>: build the graph from the <c>targets</c> section
    /// of deps.json and run a BFS from the given node. It uses deps.json rather than
    /// <see cref="Assembly.GetReferencedAssemblies"/> because the latter only reflects assemblies actually
    /// referenced by the IL, so a reference that is declared but not yet used would be missed, and that is
    /// exactly what this gate is meant to stop.
    /// </para>
    /// </remarks>
    public class ArchitectureBoundaryGateTests
    {
        /// <summary>The composition layer: by definition it spans every layer, so it is the only place that may depend directly on implementation assemblies.</summary>
        private const string CompositionRoot = "Polhem.Hosting";

        /// <summary>The list of "this assembly's transitive closure must not contain that assembly".</summary>
        public static TheoryData<string, string> ForbiddenEdges() => new()
        {
            // 1. The business logic layer must not depend on the data access implementation. A BO gets data only
            //    through the Repository abstractions.
            { "Polhem.Business", "Polhem.Db" },
            { "Polhem.Business", "Polhem.Repository" },

            // 2. The backend must not depend on the client library. `Polhem.Web.Blazor.Server` depending on it is
            //    correct (it is a front-end RCL), so it is not listed.
            { "Polhem.Api.AspNetCore", "Polhem.Api.Client" },
            { "Polhem.Hosting", "Polhem.Api.Client" },
            { "Polhem.Api.Core", "Polhem.Api.Client" },
            { "Polhem.Business", "Polhem.Api.Client" },
            { "Polhem.Repository", "Polhem.Api.Client" },
            { "Polhem.Db", "Polhem.Api.Client" },
        };

        /// <summary>The list of "this assembly must not <b>use</b> types from that assembly".</summary>
        /// <remarks>
        /// <para>
        /// This guards something different from the closure list above, with a different tool. The closure uses
        /// deps.json and catches dependencies that are declared but not yet used; this list catches **the
        /// opposite half**, types that are used without being declared in the csproj.
        /// </para>
        /// <para>
        /// WARNING: this check <b>cannot</b> use deps.json. The SDK merges transitive project references into
        /// <c>@(ProjectReference)</c> before Build, so code can <c>using</c> an assembly that only arrives
        /// transitively through another one, and neither the csproj nor deps.json shows that edge. Measured: with
        /// the API layer changed back to resolving <c>ICacheContainer</c> directly, the deps.json version of the
        /// assertion stayed green. <see cref="Assembly.GetReferencedAssemblies"/> reflects what the compiled IL
        /// really references, and that is what this constraint has to look at.
        /// </para>
        /// </remarks>
        public static TheoryData<string, string> ForbiddenAssemblyUsages() => new()
        {
            // The API layer must not know how the cache is implemented. It asks "does this deployment have an
            // API key gate in force", and that question goes through `IApiKeyGateStateProvider` in
            // `Polhem.Definition`. The "Cross-Layer Forbidden Practices" table in development-constraints lists
            // "the API layer references the Repository layer directly". This entry is outside that table but has
            // the same shape.
            { "Polhem.Api.AspNetCore", "Polhem.ObjectCaching" },
        };

        [Theory]
        [MemberData(nameof(ForbiddenAssemblyUsages))]
        [DisplayName("Hard constraint: the IL of the given assembly does not reference the forbidden assembly (transitively visible does not mean usable)")]
        public void CompiledAssembly_DoesNotReferenceForbiddenAssembly(string root, string forbidden)
        {
            var assembly = LoadFrameworkAssembly(root);
            var referenced = assembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .Where(name => name != null)
                .ToArray();

            // Guards against a vacuous pass: with the wrong assembly loaded or an empty reference list, the
            // assertion below would always hold.
            Assert.NotEmpty(referenced);
            Assert.Contains("Polhem.Definition", referenced, StringComparer.OrdinalIgnoreCase);

            Assert.False(
                referenced.Contains(forbidden, StringComparer.OrdinalIgnoreCase),
                $"The IL of {root} references {forbidden}, which crosses a layer boundary. Being transitively visible does not make it usable. " +
                "If you need an answer it provides, add a read-only query interface to Polhem.Definition and let the composition layer inject the implementation.");
        }

        /// <summary>
        /// Loads the given framework assembly from the test output directory.
        /// </summary>
        /// <param name="assemblyName">The assembly name (without the extension).</param>
        private static Assembly LoadFrameworkAssembly(string assemblyName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
            Assert.True(File.Exists(path), $"Assembly not found: {path}");
            return Assembly.LoadFrom(path);
        }

        [Theory]
        [MemberData(nameof(ForbiddenEdges))]
        [DisplayName("Hard constraint: the transitive dependency closure of the given assembly does not contain the forbidden assembly")]
        public void TransitiveClosure_DoesNotContainForbiddenAssembly(string root, string forbidden)
        {
            var graph = ReadDependencyGraph();

            // Both nodes must be in the graph, otherwise the assertion always holds because the node is not visible.
            Assert.True(graph.ContainsKey(root), $"{root} not found in deps.json, so the gate cannot check it.");
            Assert.True(
                graph.ContainsKey(forbidden),
                $"{forbidden} not found in deps.json. When the forbidden node is not in the graph, the assertion below always holds; " +
                "this test project must see it (directly or transitively).");

            var closure = ResolveClosure(graph, root);

            Assert.False(
                closure.Contains(forbidden),
                $"The transitive dependency closure of {root} contains {forbidden}, which violates a hard layering constraint. " +
                $"If this is a deliberate architectural change, update docs/en/development-constraints.md and " +
                "docs/en/dependency-map.md as well, state the reason here and remove the entry.");
        }

        [Fact]
        [DisplayName("Hard constraint: the Repository abstractions are not bypassed (only the composition layer depends on the implementation directly)")]
        public void RepositoryImplementation_IsOnlyReferencedByTheCompositionRoot()
        {
            var graph = ReadDependencyGraph();
            const string Implementation = "Polhem.Repository";
            Assert.True(graph.ContainsKey(Implementation), $"{Implementation} not found in deps.json.");

            // Only direct dependencies count. Transitive ones are brought in by the composition layer itself and
            // are not a bypass.
            var offenders = graph
                .Where(entry => entry.Key.StartsWith("Polhem.", StringComparison.Ordinal))
                .Where(entry => !IsExempt(entry.Key))
                .Where(entry => entry.Value.Contains(Implementation, StringComparer.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"These assemblies depend directly on {Implementation} instead of Polhem.Repository.Abstractions: " +
                $"{string.Join(", ", offenders)}. Data access should go through the abstractions, with the implementation injected by the composition layer.");

            static bool IsExempt(string name)
                => string.Equals(name, CompositionRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, Implementation, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".UnitTests", StringComparison.Ordinal)
                || string.Equals(name, "Polhem.Tests.Shared", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("Hard constraint: Polhem.Api.Contracts holds only contracts and no implementation")]
        public void ApiContracts_ContainNoImplementation()
        {
            var assembly = typeof(Polhem.Api.Contracts.System.IPingRequest).Assembly;
            const string RootNamespace = "Polhem.Api.Contracts";

            // Only types declared in source count. WARNING: coverage instrumentation **injects types into the
            // assembly**. Coverlet injects `Coverlet.Core.Instrumentation.Tracker.<assembly>_<guid>` with methods
            // such as `RecordHit`, which to this gate looks like implementation mixed into the contracts.
            // The filter is by namespace rather than by tool name, because each instrumentation tool injects
            // under its own namespace.
            //
            // Lite-mode CI cannot catch this: coverage is collected only in full mode (the
            // `--collect:"XPlat Code Coverage"` in build-ci.yml). A local reproduction needs the same flag.
            var types = assembly.GetTypes()
                .Where(type => !type.IsNested)
                .Where(type => type.Namespace is not null
                    && (type.Namespace == RootNamespace
                        || type.Namespace.StartsWith(RootNamespace + ".", StringComparison.Ordinal)))
                .ToArray();

            // Guards against a vacuous pass, placed **after** the filter: if the namespace condition above is wrong,
            // the loop never runs and the test is always green.
            Assert.NotEmpty(types);

            var offenders = new List<string>();
            foreach (var type in types.Where(t => !t.IsInterface && !t.IsEnum))
            {
                // Non-interface types in the contracts are pure data carriers only. Any method other than property
                // accessors and constructors counts as implementation.
                var methods = type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                              | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => !method.IsSpecialName)
                    .Select(method => method.Name)
                    .ToArray();

                if (methods.Length > 0)
                {
                    offenders.Add($"{type.FullName} ({string.Join(", ", methods)})");
                }
            }

            Assert.True(
                offenders.Count == 0,
                $"Polhem.Api.Contracts contains types with behavior: {string.Join("; ", offenders)}. " +
                "The contracts describe shape only. Implementation belongs to the message types of Polhem.Api.Core or to the BO layer; " +
                "mixed in here, every consumer inherits it.");
        }

        /// <summary>
        /// Returns the transitive dependency closure of <paramref name="root"/> (excluding itself).
        /// </summary>
        private static HashSet<string> ResolveClosure(Dictionary<string, string[]> graph, string root)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>(graph[root]);
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
        /// Reads the test assembly's deps.json and returns the dependency graph "library name → direct dependency names".
        /// </summary>
        private static Dictionary<string, string[]> ReadDependencyGraph()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var depsPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.deps.json");
            Assert.True(File.Exists(depsPath), $"Dependency file not found: {depsPath}");

            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            // With a `RuntimeIdentifier` there are two targets (RID-less and RID-specific). Taking the one with the
            // most entries gets the full graph under both kinds of build.
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
