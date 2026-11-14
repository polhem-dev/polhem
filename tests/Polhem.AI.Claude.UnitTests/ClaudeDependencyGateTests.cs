using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.AI.Claude.UnitTests
{
    /// <summary>
    /// Dependency gate for the Claude provider: it supplies an <c>IChatClient</c> and nothing else, so no Polhem package
    /// may appear in its transitive closure. A feature package depends on the abstraction, and the application joins the
    /// two; a provider that referenced a feature would pull that feature into every application that only wants a model.
    /// </summary>
    public class ClaudeDependencyGateTests
    {
        [Fact]
        [DisplayName("The transitive dependencies of Polhem.AI.Claude include no Polhem package")]
        public void Provider_TransitiveDependencies_ContainNoPolhemPackage()
        {
            var polhem = ResolveDependencyClosure(ReadDependencyGraph(), "Polhem.AI.Claude")
                .Where(name => name.StartsWith("Polhem.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(polhem.Length == 0,
                $"Polhem.AI.Claude depends on {string.Join(", ", polhem)}. A provider package supplies the model only; "
                + "the application joins it to the features that use it.");
        }

        [Fact]
        [DisplayName("The provider dependency gate walks real edges")]
        public void DependencyClosure_IsNotVacuous()
        {
            var closure = ResolveDependencyClosure(ReadDependencyGraph(), "Polhem.AI.Claude");

            Assert.Contains("Anthropic", closure, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Microsoft.Extensions.AI.Abstractions", closure, StringComparer.OrdinalIgnoreCase);
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
