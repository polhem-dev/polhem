using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Web.Blazor.Wasm.UnitTests
{
    /// <summary>
    /// Dependency gate for the WebAssembly host (ADR-059): nothing in its closure is a UI framework or the Blazor Server
    /// host, and nothing asks for the ASP.NET Core shared framework, which cannot load in the browser.
    /// </summary>
    /// <remarks>
    /// The complement of <c>POLHEM9001</c> in <c>src/Directory.Build.targets</c>, which sees direct references. This
    /// reads the graph from this test assembly's own <c>.deps.json</c>, as <c>BlazorSharedLayerDependencyGateTests</c> does.
    /// </remarks>
    public class BlazorWasmHostDependencyGateTests
    {
        private static readonly string[] s_forbiddenPrefixes =
        [
            "MudBlazor", "Microsoft.FluentUI", "DevExpress", "Polhem.Web.Blazor.Server", "Polhem.Web.Blazor.Mud",
            "Microsoft.AspNetCore.Components.Server", "Microsoft.AspNetCore.App",
        ];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Web.Blazor.Wasm contain no UI framework or server package")]
        public void Host_TransitiveDependencies_ContainNoUIFrameworkOrServer()
        {
            var closure = ResolveDependencyClosure("Polhem.Web.Blazor.Wasm");

            Assert.Contains("Polhem.Web.Blazor", closure, StringComparer.OrdinalIgnoreCase);
            var forbidden = closure
                .Where(name => s_forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.True(forbidden.Length == 0, $"Polhem.Web.Blazor.Wasm reaches: {string.Join(", ", forbidden)}.");
        }

        private static HashSet<string> ResolveDependencyClosure(string root)
        {
            var depsPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".deps.json");
            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            var libraries = document.RootElement.GetProperty("targets").EnumerateObject().First().Value
                .EnumerateObject()
                .ToDictionary(p => p.Name.Split('/')[0], p => p.Value, StringComparer.OrdinalIgnoreCase);

            var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            pending.Enqueue(root);
            while (pending.TryDequeue(out var name))
            {
                if (!libraries.TryGetValue(name, out var library)
                    || !library.TryGetProperty("dependencies", out var dependencies))
                {
                    continue;
                }
                foreach (var dependency in dependencies.EnumerateObject())
                {
                    if (closure.Add(dependency.Name))
                        pending.Enqueue(dependency.Name);
                }
            }
            return closure;
        }
    }
}
