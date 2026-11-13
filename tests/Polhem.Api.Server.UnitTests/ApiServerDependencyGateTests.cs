using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Api.Server.UnitTests
{
    /// <summary>
    /// Dependency gate for the server half of the wire (ADR-055): the transitive closure of <c>Polhem.Api.Server</c>
    /// stays within the wire layer, the JSON-RPC server packages and the abstractions they use, so it never reaches a
    /// repository, the database layer or a business object.
    /// </summary>
    /// <remarks>
    /// The complement of <c>POLHEM9001</c> in <c>src/Directory.Build.targets</c>: the build-time lock sees the project
    /// references, this test also sees a package that arrives indirectly through one. The graph is read from this test
    /// assembly's own <c>.deps.json</c>. This project references <c>Polhem.Business</c> for its tests, so the business,
    /// repository and database layers are in the graph, and a path to them from <c>Polhem.Api.Server</c> would be found.
    /// The external packages on the list are allowed together with their own closures, which follow their releases.
    /// </remarks>
    public class ApiServerDependencyGateTests
    {
        private static readonly string[] s_allowed =
        [
            "Polhem.Api.Core", "Polhem.Api.Contracts", "Polhem.Definition", "Polhem.Core",
            "Polhem.JsonRpc", "Polhem.JsonRpc.Payload", "Polhem.JsonRpc.Server", "Polhem.JsonRpc.Payload.Server",
            "MessagePack", "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Localization.Abstractions",
        ];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Api.Server stay within the wire layer and the JSON-RPC server packages")]
        public void ApiServer_TransitiveDependencies_StayWithinAllowlist()
        {
            var graph = ReadDependencyGraph();
            var allowed = new HashSet<string>(s_allowed, StringComparer.OrdinalIgnoreCase);
            foreach (var external in s_allowed.Where(name => !name.StartsWith("Polhem.", StringComparison.Ordinal)))
                allowed.UnionWith(ResolveDependencyClosure(graph, external));

            var unexpected = ResolveDependencyClosure(graph, "Polhem.Api.Server")
                .Where(name => !allowed.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(unexpected.Length == 0,
                $"Polhem.Api.Server has transitive dependencies outside the allowlist: {string.Join(", ", unexpected)}. "
                + "The API layer must not reach the business, repository or database layers. If a new dependency is "
                + "deliberate, add it to PolhemAllowedDependency in src/Directory.Build.targets and to this test.");
        }

        [Fact]
        [DisplayName("The API server dependency gate walks real edges, and the layers it keeps out are in the graph")]
        public void DependencyClosure_IsNotVacuous()
        {
            var graph = ReadDependencyGraph();
            var server = ResolveDependencyClosure(graph, "Polhem.Api.Server");

            Assert.Contains("Polhem.Api.Core", server, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.JsonRpc.Server", server, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.Repository.Abstractions", graph.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.Business", graph.Keys, StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> ResolveDependencyClosure(Dictionary<string, string[]> graph, string rootLibrary)
        {
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
