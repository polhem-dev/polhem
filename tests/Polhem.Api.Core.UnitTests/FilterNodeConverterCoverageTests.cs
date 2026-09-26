using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Guards that every property declared as <see cref="FilterNode"/> carries
    /// <see cref="FilterNodeJsonConverter"/>.
    /// </summary>
    /// <remarks>
    /// A missing attribute shows no symptom: System.Text.Json binds to the declared type, so the whole filter subtree
    /// disappears silently, with no exception and no log. The compiler does not warn, and the type itself cannot carry
    /// the attribute: a converter placed on <see cref="FilterNode"/> is inherited by the subclasses and recurses
    /// forever (the stack overflows and the process crashes). So the attribute can only go on each property, and a
    /// per-property rule is exactly the kind that gets missed.
    /// <para>
    /// Hence this test: adding a <see cref="FilterNode"/> property without the attribute turns it red.
    /// </para>
    /// </remarks>
    public class FilterNodeConverterCoverageTests
    {
        [Fact]
        [DisplayName("Every property of type FilterNode declares FilterNodeJsonConverter")]
        public void EveryFilterNodeProperty_DeclaresTheConverter()
        {
            var assemblies = new[]
            {
                typeof(Messages.Form.GetListRequest).Assembly,   // Polhem.Api.Core
                typeof(FilterNode).Assembly,                     // Polhem.Definition
                typeof(Polhem.Business.BusinessObject).Assembly,    // Polhem.Business
            };

            var properties = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false })
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(p => p.PropertyType == typeof(FilterNode))
                // Only writable properties count: only a property with a setter takes part in deserialization and
                // holds a value on the wire. A get-only one (such as `DeleteContext.ScopeFilter` on the BO runtime
                // context, an object that also carries repository interfaces) never goes through JSON, and marking it
                // would only mislead readers into thinking it goes on the wire.
                .Where(p => p.SetMethod?.IsPublic == true)
                .ToList();

            // Finding no property means the scan itself is broken (a type moved, an assembly renamed), not that everything complies.
            Assert.NotEmpty(properties);

            var missing = properties
                .Where(p => p.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
                            != typeof(FilterNodeJsonConverter))
                .Select(p => $"{p.DeclaringType!.FullName}.{p.Name}")
                .ToList();

            Assert.True(missing.Count == 0,
                "The following FilterNode properties lack [JsonConverter(typeof(FilterNodeJsonConverter))], " +
                "so JSON serialization silently drops the whole filter subtree:" + global::System.Environment.NewLine +
                string.Join(global::System.Environment.NewLine, missing));
        }
    }
}
