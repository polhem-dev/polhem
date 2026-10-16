using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.UI.Avalonia.UnitTests
{
    /// <summary>
    /// Dependency gate for the front-end layer (ADR-054): the transitive closure of <c>Polhem.UI.Core</c> and
    /// <c>Polhem.UI.Avalonia</c> reaches the backend only through <c>Polhem.Api.Contracts</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The complement of <c>POLHEM9001</c> in <c>src/Directory.Build.targets</c>, as
    /// <c>DefinitionDependencyGateTests</c> is for the foundation assemblies: the build-time lock sees the project
    /// references, this test also sees a package that arrives indirectly through one. The graph is read from this test
    /// assembly's own <c>.deps.json</c>, and a breadth-first walk from each root yields exactly that root's closure,
    /// whatever else this test project references.
    /// </para>
    /// <para>
    /// <c>Polhem.UI.Core</c> is held to an allowlist. <c>Polhem.UI.Avalonia</c> is held to a denylist instead, because
    /// the Avalonia packages bring a closure of their own that follows Avalonia's releases; what this gate exists to
    /// stop is the transport: the API client, the API core with MessagePack and the JSON-RPC packages, and the bridge
    /// that carries them.
    /// </para>
    /// </remarks>
    public class FrontEndDependencyGateTests
    {
        private static readonly string[] s_uiCoreAllowed =
        [
            "Polhem.Api.Contracts", "Polhem.Definition", "Polhem.Core", "Microsoft.Extensions.Localization.Abstractions",
        ];

        private static readonly string[] s_transportPrefixes =
        [
            "Polhem.Api.Client", "Polhem.Api.Core", "Polhem.UI.ApiBridge", "Polhem.JsonRpc", "MessagePack",
        ];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.UI.Core stay within the contracts, definition and core assemblies")]
        public void UICore_TransitiveDependencies_StayWithinAllowlist()
        {
            var allowed = new HashSet<string>(s_uiCoreAllowed, StringComparer.OrdinalIgnoreCase);
            var unexpected = ResolveDependencyClosure("Polhem.UI.Core")
                .Where(name => !allowed.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(unexpected.Length == 0,
                $"Polhem.UI.Core has transitive dependencies outside the allowlist: {string.Join(", ", unexpected)}. "
                + "Every front end inherits them. If this is deliberate, add it to PolhemAllowedDependency in "
                + "src/Directory.Build.targets and to this test, and record why in ADR-054.");
        }

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.UI.Avalonia carry no API client, API core, bridge, JSON-RPC or MessagePack")]
        public void UIAvalonia_TransitiveDependencies_CarryNoTransport()
        {
            var transport = ResolveDependencyClosure("Polhem.UI.Avalonia")
                .Where(name => s_transportPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(transport.Length == 0,
                $"Polhem.UI.Avalonia reaches the transport: {string.Join(", ", transport)}. A front end depends on the "
                + "contracts in Polhem.Api.Contracts; a head that has a backend references Polhem.UI.ApiBridge itself (ADR-054).");
        }

        [Fact]
        [DisplayName("The front-end dependency gate walks real edges, so an unresolved root cannot pass vacuously")]
        public void DependencyClosure_IsNotVacuous()
        {
            Assert.Contains("Polhem.Api.Contracts", ResolveDependencyClosure("Polhem.UI.Core"), StringComparer.OrdinalIgnoreCase);
            var avalonia = ResolveDependencyClosure("Polhem.UI.Avalonia");
            Assert.Contains("Polhem.UI.Core", avalonia, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Avalonia", avalonia, StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> ResolveDependencyClosure(string rootLibrary)
        {
            var graph = ReadDependencyGraph();
            Assert.True(graph.ContainsKey(rootLibrary),
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

        private static Dictionary<string, string[]> ReadDependencyGraph()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var depsPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.deps.json");
            Assert.True(File.Exists(depsPath), $"Dependency file not found: {depsPath}");

            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            // With a RuntimeIdentifier there are two targets; the one with the most entries is the complete graph.
            var target = document.RootElement
                .GetProperty("targets")
                .EnumerateObject()
                .OrderByDescending(entry => entry.Value.EnumerateObject().Count())
                .First()
                .Value;

            var graph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in target.EnumerateObject())
            {
                var name = library.Name.Split('/')[0];
                graph[name] = library.Value.TryGetProperty("dependencies", out var dependencies)
                    ? dependencies.EnumerateObject().Select(entry => entry.Name).ToArray()
                    : [];
            }
            return graph;
        }
    }
}
