using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// An application chooses its UI package and its host package independently (ADR-059), so this package reaches no
    /// host package and no other UI framework. The graph is read from this test assembly's <c>.deps.json</c>.
    /// </summary>
    public class MudPackageDependencyGateTests
    {
        private static readonly string[] s_forbiddenPrefixes =
        [
            "Polhem.Web.Blazor.Server", "Microsoft.FluentUI", "DevExpress",
            "Microsoft.AspNetCore.Components.Server", "Microsoft.AspNetCore.Components.WebAssembly",
        ];

        [Fact]
        [DisplayName("The transitive dependencies of Polhem.Web.Blazor.Mud contain no host package or other UI framework")]
        public void MudPackage_TransitiveDependencies_ContainNoHostOrOtherFramework()
        {
            var closure = ResolveDependencyClosure("Polhem.Web.Blazor.Mud");

            Assert.Contains("MudBlazor", closure, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(closure, name => s_forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
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
                if (!closure.Add(name) || !libraries.TryGetValue(name, out var library)) continue;
                if (!library.TryGetProperty("dependencies", out var dependencies)) continue;
                foreach (var dependency in dependencies.EnumerateObject())
                    pending.Enqueue(dependency.Name);
            }
            return closure;
        }
    }
}
