using System.ComponentModel;
using System.Reflection;
using Polhem.Definition.Filters;
using Polhem.Definition.Organization;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Checks that no definition type decides whether a member is written through a <c>ShouldSerialize{Property}()</c>
    /// method.
    /// </summary>
    /// <remarks>
    /// The reflection-only <c>XmlSerializer</c> on iOS throws <see cref="NullReferenceException"/> when such a method is
    /// declared on a base class of the type being written, and <see cref="Polhem.Core.Serialization.JsonCodec"/> does
    /// not consult it, so XML and JSON would omit different members. A get-only <c>{Property}Specified</c> property works
    /// on every runtime and in both formats; <c>src/Polhem.Definition/CLAUDE.md</c> has the convention.
    /// </remarks>
    public class SpecifiedConventionGateTests
    {
        [Fact]
        [DisplayName("No type in Polhem.Definition declares a ShouldSerialize method")]
        public void DefinitionTypes_DeclareNoShouldSerializeMethod()
        {
            var assembly = typeof(FilterGroup).Assembly;
            var types = assembly.GetTypes()
                .Where(type => type.Namespace?.StartsWith("Polhem.Definition", StringComparison.Ordinal) == true)
                .ToArray();

            var offenders = types
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                        | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name.StartsWith("ShouldSerialize", StringComparison.Ordinal))
                    .Select(method => $"{type.FullName}.{method.Name}"))
                .ToArray();

            Assert.Contains(typeof(FilterGroup), types);
            Assert.Contains(typeof(DepartmentNode), types);
            Assert.True(offenders.Length == 0, "Use a get-only {Property}Specified instead: " + string.Join(", ", offenders));
        }
    }
}
