using System.ComponentModel;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Polhem.Base.Collections;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Asserts that the trimmer descriptor embedded in <c>Polhem.Definition</c> names assemblies and
    /// namespaces that exist.
    /// </summary>
    /// <remarks>
    /// Nothing else notices a wrong name here. The iOS heads build with <c>TrimMode=partial</c>, which
    /// leaves the framework assemblies untrimmed, so the descriptor has no effect there at all. Under
    /// <c>TrimMode=full</c> a descriptor that names a missing assembly costs most of the definition
    /// types and produces a single IL2007 warning, in a head that is never warning-free.
    /// </remarks>
    public class TrimmerDescriptorGateTests
    {
        private const string ResourceName = "ILLink.Descriptors.xml";

        private static readonly Assembly[] s_rootedAssemblies =
        {
            typeof(FormSchema).Assembly,
            typeof(KeyCollectionBase<>).Assembly,
        };

        [Fact]
        [DisplayName("ILLink descriptor 的組件名與型別萬用字元都指得到實際的組件與命名空間")]
        public void Descriptor_NamesExistingAssembliesAndNamespaces()
        {
            var assemblies = LoadDescriptor().Root!.Elements("assembly").ToList();

            // Guards against a descriptor that no longer parses into the expected shape, which would
            // otherwise make the loop below pass without checking anything.
            Assert.Equal(s_rootedAssemblies.Length, assemblies.Count);

            foreach (var element in assemblies)
            {
                var name = (string)element.Attribute("fullname")!;
                var assembly = s_rootedAssemblies.SingleOrDefault(a => a.GetName().Name == name);
                Assert.True(assembly != null, $"ILLink descriptor roots assembly '{name}', which is not a framework assembly.");

                foreach (var type in element.Elements("type"))
                {
                    var pattern = (string)type.Attribute("fullname")!;
                    if (pattern == "*") { continue; }

                    var prefix = pattern.TrimEnd('*');
                    Assert.True(
                        assembly!.GetTypes().Any(t => t.FullName!.StartsWith(prefix, StringComparison.Ordinal)),
                        $"ILLink descriptor pattern '{pattern}' matches no type in {name}.");
                }
            }
        }

        private static XDocument LoadDescriptor()
        {
            using var stream = typeof(FormSchema).Assembly.GetManifestResourceStream(ResourceName);
            Assert.True(stream != null, $"{ResourceName} is not embedded in Polhem.Definition.");
            using var reader = XmlReader.Create(stream!,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }
    }
}
