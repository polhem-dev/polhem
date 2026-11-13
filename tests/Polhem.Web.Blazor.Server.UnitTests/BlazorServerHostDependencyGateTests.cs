using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Web.Blazor.Server.UnitTests
{
    /// <summary>
    /// Dependency gate for the Blazor Server host (ADR-059): an application chooses its UI package and its host package
    /// independently, so nothing in the host's closure is a UI framework, a UI package or the WebAssembly host.
    /// </summary>
    /// <remarks>
    /// <c>POLHEM9001</c> does not govern this project: its direct references include ASP.NET Core, which an allowlist
    /// would have to name in full. This reads the graph from this test assembly's own <c>.deps.json</c>. The test project
    /// references the MudBlazor and Fluent UI packages itself, so they are in that file, and a reference from the host
    /// would show under the host's own entry.
    /// </remarks>
    public class BlazorServerHostDependencyGateTests
    {
        private static readonly string[] s_forbiddenPrefixes =
        [
            "MudBlazor", "Microsoft.FluentUI", "DevExpress", "Polhem.Web.Blazor.Mud", "Polhem.Web.Blazor.Fluent",
            "Polhem.Web.Blazor.Wasm",
        ];

        // The WebAssembly client runtime, matched exactly: a server that serves WebAssembly may use
        // Microsoft.AspNetCore.Components.WebAssembly.Server, and that is not a reason to fail.
        private const string WebAssemblyClient = "Microsoft.AspNetCore.Components.WebAssembly";

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Web.Blazor.Server contain no UI framework, UI package or WebAssembly host")]
        public void Host_TransitiveDependencies_ContainNoUIFrameworkOrOtherHost()
        {
            var libraries = ReadLibraries();
            var closure = ResolveDependencyClosure(libraries, "Polhem.Web.Blazor.Server");

            // Not vacuous: the host reaches the shared layer, and the forbidden packages are in the graph to be found.
            Assert.Contains("Polhem.Web.Blazor", closure, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Polhem.Web.Blazor.Mud", libraries.Keys, StringComparer.OrdinalIgnoreCase);
            var forbidden = closure.Where(IsForbidden).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.True(forbidden.Length == 0, $"Polhem.Web.Blazor.Server reaches: {string.Join(", ", forbidden)}.");
        }

        [Fact]
        [DisplayName("Polhem.Web.Blazor.Server is compiled against no UI framework, UI package or WebAssembly host")]
        public void Host_ReferencedAssemblies_ContainNoUIFrameworkOrOtherHost()
        {
            // The compiled references, which a reference marked PrivateAssets="all" hides from .deps.json but not from here.
            var referenced = typeof(Polhem.Web.Blazor.Server.DependencyInjection.PolhemBlazorOptions).Assembly.GetReferencedAssemblies()
                .Select(name => name.Name ?? string.Empty)
                .ToArray();

            Assert.Contains("Polhem.Web.Blazor", referenced, StringComparer.OrdinalIgnoreCase);
            var forbidden = referenced.Where(IsForbidden).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.True(forbidden.Length == 0, $"Polhem.Web.Blazor.Server references: {string.Join(", ", forbidden)}.");
        }

        private static bool IsForbidden(string name)
            => string.Equals(name, WebAssemblyClient, StringComparison.OrdinalIgnoreCase)
                || s_forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        private static Dictionary<string, JsonElement> ReadLibraries()
        {
            var depsPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".deps.json");
            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            return document.RootElement.GetProperty("targets").EnumerateObject().First().Value
                .EnumerateObject()
                .ToDictionary(p => p.Name.Split('/')[0], p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> ResolveDependencyClosure(Dictionary<string, JsonElement> libraries, string root)
        {
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
