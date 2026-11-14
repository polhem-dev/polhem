using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Definition.Drafting.UnitTests
{
    /// <summary>
    /// Dependency gate for the drafting feature: it talks to a model only through the <c>IChatClient</c> abstraction, so
    /// no model vendor's SDK may appear anywhere in its transitive closure. A vendor belongs in its own provider package,
    /// which the application chooses.
    /// </summary>
    /// <remarks>
    /// The graph is read from this test assembly's own <c>.deps.json</c>, so a vendor package that arrives indirectly,
    /// through another package or a project reference, is found as well.
    /// </remarks>
    public class DraftingDependencyGateTests
    {
        private static readonly string[] s_vendorPrefixes = ["Anthropic"];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Definition.Drafting include no model vendor SDK")]
        public void Drafting_TransitiveDependencies_ContainNoVendorSdk()
        {
            var closure = ResolveDependencyClosure(ReadDependencyGraph(), "Polhem.Definition.Drafting");

            var vendors = closure
                .Where(name => s_vendorPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(vendors.Length == 0,
                $"Polhem.Definition.Drafting depends on a model vendor's SDK: {string.Join(", ", vendors)}. "
                + "The feature must reach a model only through IChatClient; reference the vendor from a provider package.");
        }

        [Fact]
        [DisplayName("The drafting dependency gate walks real edges")]
        public void DependencyClosure_IsNotVacuous()
        {
            var closure = ResolveDependencyClosure(ReadDependencyGraph(), "Polhem.Definition.Drafting");

            Assert.Contains("Microsoft.Extensions.AI.Abstractions", closure, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.Definition", closure, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.Core", closure, StringComparer.OrdinalIgnoreCase);
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
