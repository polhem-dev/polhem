using System.ComponentModel;
using System.Xml;
using System.Xml.Linq;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// Asserts that the trimmer descriptor embedded in <c>Polhem.Expressions</c> preserves exactly the types an
    /// expression can reach.
    /// </summary>
    /// <remarks>
    /// Nothing else notices a gap here. A trimmed mobile head keeps a BCL member only when something statically
    /// references it, and DynamicExpresso reaches members by reflection. A missing root shows up on a device as an
    /// expression that parses on the desktop and fails with "No applicable method" in a Release build, after which
    /// live computation silently turns itself off for the form.
    /// </remarks>
    public class TrimmerDescriptorGateTests
    {
        private const string ResourceName = "ILLink.Descriptors.xml";

        [Fact]
        [DisplayName("The ILLink descriptor is embedded in Polhem.Expressions under the name the trimmer picks up")]
        public void Descriptor_IsEmbedded()
        {
            using var stream = typeof(DynamicExpressoEvaluator).Assembly.GetManifestResourceStream(ResourceName);

            Assert.NotNull(stream);
        }

        [Fact]
        [DisplayName("The ILLink descriptor fully preserves every type the expression interpreter exposes, and nothing else")]
        public void Descriptor_PreservesExactlyTheExposedTypes()
        {
            var exposed = new DynamicExpressoEvaluator().ExposedTypes
                .Select(type => $"{type.Assembly.GetName().Name}|{type.FullName}")
                .ToHashSet(StringComparer.Ordinal);
            var rooted = LoadDescriptor().Root!.Elements("assembly")
                .SelectMany(assembly => assembly.Elements("type").Select(type => (Assembly: assembly, Type: type)))
                .ToList();

            // Guards against an exposure list that stopped enumerating anything, which would make the comparison
            // below pass vacuously. `Math` is the type the expression rules document first.
            Assert.Contains("System.Private.CoreLib|System.Math", exposed);

            Assert.All(rooted, entry => Assert.Equal("all", (string?)entry.Type.Attribute("preserve")));
            var rootedNames = rooted
                .Select(entry => $"{(string)entry.Assembly.Attribute("fullname")!}|{(string)entry.Type.Attribute("fullname")!}")
                .ToHashSet(StringComparer.Ordinal);

            Assert.Empty(exposed.Except(rootedNames));
            Assert.Empty(rootedNames.Except(exposed));
        }

        private static XDocument LoadDescriptor()
        {
            using var stream = typeof(DynamicExpressoEvaluator).Assembly.GetManifestResourceStream(ResourceName);
            Assert.True(stream != null, $"{ResourceName} is not embedded in Polhem.Expressions.");
            using var reader = XmlReader.Create(stream!,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }
    }
}
