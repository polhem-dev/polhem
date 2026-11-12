using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Compares an assembly's public types with the type entries of the XML documentation file built next to it.
    /// </summary>
    /// <remarks>
    /// CS1591 does not reach a Razor component's type: the Razor generator starts each generated file with
    /// <c>#pragma warning disable 1591</c>, so a component without a <c>/// &lt;summary&gt;</c> on its partial class builds
    /// cleanly and ships with no IntelliSense text. Reading the built documentation file is what sees the gap.
    /// </remarks>
    public static class TypeDocCoverage
    {
        /// <summary>
        /// Finds the public types of an assembly that its documentation file has no entry for.
        /// </summary>
        /// <param name="assembly">The assembly, loaded from the test's output directory.</param>
        /// <param name="checkedTypes">
        /// The documentation IDs of every public type that was checked: those in the assembly's own namespace or below it,
        /// apart from the classes Razor generates for <c>_Imports.razor</c>.
        /// </param>
        /// <returns>The documentation IDs of the types that have no entry, sorted.</returns>
        /// <exception cref="FileNotFoundException">The documentation file is not next to the assembly.</exception>
        public static IReadOnlyList<string> FindUndocumentedTypes(Assembly assembly, out IReadOnlyList<string> checkedTypes)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            string path = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(path))
                throw new FileNotFoundException($"The documentation file of {assembly.GetName().Name} is missing.", path);

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(path, settings);
            var documented = XDocument.Load(reader)
                .Descendants("member")
                .Select(member => (string?)member.Attribute("name"))
                .OfType<string>()
                .Where(name => name.StartsWith("T:", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            // Narrowed to the assembly's own namespace: coverage instrumentation injects a public tracker type in a
            // namespace of its own, which full-mode CI would otherwise report as undocumented.
            string root = assembly.GetName().Name!;
            var publicTypes = assembly.GetExportedTypes()
                .Where(type => type.Namespace is { } ns
                    && (ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal)))
                // Razor compiles each `_Imports.razor` into a public class that holds only directives; it is not a
                // component anyone writes into a page.
                .Where(type => type.Name != "_Imports")
                .Select(type => "T:" + type.FullName!.Replace('+', '.'))
                .Order(StringComparer.Ordinal)
                .ToArray();
            checkedTypes = publicTypes;
            return publicTypes.Where(id => !documented.Contains(id)).ToArray();
        }
    }
}
