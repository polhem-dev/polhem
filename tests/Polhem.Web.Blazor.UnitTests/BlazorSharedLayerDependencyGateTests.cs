using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Web.Blazor.UnitTests
{
    /// <summary>
    /// Dependency gate for the shared layer of the Blazor head (ADR-059): nothing in its transitive closure is a UI
    /// framework or a host package, so a UI package and a host package can each be chosen independently.
    /// </summary>
    /// <remarks>
    /// The complement of <c>POLHEM9001</c> in <c>src/Directory.Build.targets</c>: the build-time lock sees direct
    /// references and the framework reference, and this test also sees a package that arrives through a project
    /// reference. The graph is read from this test assembly's own <c>.deps.json</c>.
    /// </remarks>
    public class BlazorSharedLayerDependencyGateTests
    {
        private static readonly string[] s_forbiddenPrefixes =
        [
            "MudBlazor", "Microsoft.FluentUI", "DevExpress", "Polhem.Web.Blazor.Server", "Polhem.Web.Blazor.Mud", "Polhem.Web.Blazor.Fluent",
            "Microsoft.AspNetCore.Components.Server", "Microsoft.AspNetCore.Components.WebAssembly",
        ];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Web.Blazor contain no UI framework or host package")]
        public void SharedLayer_TransitiveDependencies_ContainNoUIFrameworkOrHost()
        {
            var forbidden = ResolveDependencyClosure("Polhem.Web.Blazor")
                .Where(name => s_forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(forbidden.Length == 0,
                $"Polhem.Web.Blazor reaches: {string.Join(", ", forbidden)}. An application chooses its UI package and its host "
                + "package independently, so the shared layer cannot depend on either (ADR-059).");
        }

        [Fact]
        [DisplayName("The dependency gate walks real edges, so an unresolved root cannot pass vacuously")]
        public void DependencyClosure_IsNotVacuous()
        {
            var closure = ResolveDependencyClosure("Polhem.Web.Blazor");

            Assert.Contains("Polhem.UI.Core", closure, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Microsoft.AspNetCore.Components.Web", closure, StringComparer.OrdinalIgnoreCase);
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
