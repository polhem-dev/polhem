using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Dependency gate for the wire layer both ends share (ADR-055): the transitive closure of <c>Polhem.Api.Core</c>
    /// stays within its allowlist, and the closure of <c>Polhem.Api.Client</c> carries none of the server side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The complement of <c>POLHEM9001</c> in <c>src/Directory.Build.targets</c>, as <c>FrontEndDependencyGateTests</c>
    /// is for the front-end layer: the build-time lock sees the project references, this test also sees a package that
    /// arrives indirectly through one. The graph is read from this test assembly's own <c>.deps.json</c>, and a
    /// breadth-first walk from each root yields exactly that root's closure. This project references
    /// <c>Polhem.Api.Server</c> for its in-process tests, so the server side is in the graph, and a path to it from
    /// <c>Polhem.Api.Client</c> would be found.
    /// </para>
    /// <para>
    /// MessagePack is allowed together with its own closure, which follows MessagePack's releases rather than this
    /// framework's.
    /// </para>
    /// </remarks>
    public class ApiClientDependencyGateTests
    {
        private static readonly string[] s_apiCoreAllowed =
        [
            "MessagePack", "Polhem.JsonRpc", "Polhem.JsonRpc.Payload", "Polhem.Api.Contracts", "Polhem.Definition",
            "Polhem.Core", "Microsoft.Extensions.Localization.Abstractions",
        ];

        private static readonly string[] s_serverSide =
        [
            "Polhem.Api.Server", "Polhem.JsonRpc.Server", "Polhem.JsonRpc.Payload.Server", "Polhem.JsonRpc.AspNetCore",
        ];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Api.Core stay within MessagePack, the payload envelope and the foundation assemblies")]
        public void ApiCore_TransitiveDependencies_StayWithinAllowlist()
        {
            var graph = ReadDependencyGraph();
            var allowed = new HashSet<string>(s_apiCoreAllowed, StringComparer.OrdinalIgnoreCase);
            allowed.UnionWith(ResolveDependencyClosure(graph, "MessagePack"));

            var unexpected = ResolveDependencyClosure(graph, "Polhem.Api.Core")
                .Where(name => !allowed.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(unexpected.Length == 0,
                $"Polhem.Api.Core has transitive dependencies outside the allowlist: {string.Join(", ", unexpected)}. "
                + "Every API client inherits them. If this is deliberate, add it to PolhemAllowedDependency in "
                + "src/Directory.Build.targets and to this test, and record why in ADR-055.");
        }

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Api.Client carry no server-side assembly or JSON-RPC server package")]
        public void ApiClient_TransitiveDependencies_CarryNoServerSide()
        {
            var serverSide = new HashSet<string>(s_serverSide, StringComparer.OrdinalIgnoreCase);
            var reached = ResolveDependencyClosure(ReadDependencyGraph(), "Polhem.Api.Client")
                .Where(serverSide.Contains)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(reached.Length == 0,
                $"Polhem.Api.Client reaches the server side: {string.Join(", ", reached)}. An in-process client gets "
                + "its transport from IInProcessTransportFactory, which the backend registers (ADR-055).");
        }

        [Fact]
        [DisplayName("The API client dependency gate walks real edges, so an unresolved root cannot pass vacuously")]
        public void DependencyClosure_IsNotVacuous()
        {
            var graph = ReadDependencyGraph();
            var client = ResolveDependencyClosure(graph, "Polhem.Api.Client");
            Assert.Contains("Polhem.Api.Core", client, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.JsonRpc.Payload.Client", client, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("MessagePack", ResolveDependencyClosure(graph, "Polhem.Api.Core"), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.JsonRpc.Server", ResolveDependencyClosure(graph, "Polhem.Api.Server"), StringComparer.OrdinalIgnoreCase);
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
