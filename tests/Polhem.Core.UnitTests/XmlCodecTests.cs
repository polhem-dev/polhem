using System.ComponentModel;
using Polhem.Core.Serialization;

namespace Polhem.Core.UnitTests
{
    public class XmlCodecTests : SerializationTestBase
    {
        [Fact]
        [DisplayName("Serialize and Deserialize round-trip the values")]
        public void Xml_Roundtrip_PreservesValues()
        {
            var source = new SerializationTestPayload { Name = "Alice", Age = 30 };

            string xml = XmlCodec.Serialize(source);
            var restored = XmlCodec.Deserialize<SerializationTestPayload>(xml);

            Assert.NotNull(restored);
            Assert.Equal("Alice", restored!.Name);
            Assert.Equal(30, restored.Age);
        }

        [Fact]
        [DisplayName("Serialize returns an empty string for null input")]
        public void Serialize_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, XmlCodec.Serialize(null!));
        }

        [Fact]
        [DisplayName("Deserialize returns the default of the type for an empty string")]
        public void Deserialize_EmptyString_ReturnsDefault()
        {
            var result = XmlCodec.Deserialize<SerializationTestPayload>(string.Empty);
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Serialize omits a collection whose inherited Specified property is false and writes it otherwise")]
        public void Serialize_SpecifiedProperty_ControlsCollectionElement()
        {
            var empty = new SpecifiedPayload { Name = "Bob" };
            var filled = new SpecifiedPayload { Name = "Bob", Tags = ["a"] };

            string emptyXml = XmlCodec.Serialize(empty);
            string filledXml = XmlCodec.Serialize(filled);

            Assert.DoesNotContain("<Tags", emptyXml, StringComparison.Ordinal);
            Assert.Contains("<Tags>", filledXml, StringComparison.Ordinal);
            Assert.Equal(["a"], XmlCodec.Deserialize<SpecifiedPayload>(filledXml)!.Tags);
        }

        [Fact]
        [DisplayName("SerializeToFile and DeserializeFromFile round-trip and set ObjectFilePath")]
        public void XmlFile_Roundtrip_SetsObjectFilePath()
        {
            var source = new SerializationTestPayload { Name = "Dan", Age = 50 };
            string path = TempPath("payload.xml");

            XmlCodec.SerializeToFile(source, path);

            Assert.True(File.Exists(path));
            Assert.Equal(path, source.ObjectFilePath);

            var restored = XmlCodec.DeserializeFromFile<SerializationTestPayload>(path)!;
            Assert.Equal("Dan", restored.Name);
            Assert.Equal(path, restored.ObjectFilePath);
        }

        [Fact]
        [DisplayName("DeserializeFromFile returns null for a missing file")]
        public void DeserializeFromFile_MissingFile_ReturnsNull()
        {
            // FileReadText returns empty for missing files, and Deserialize(string.Empty) returns default.
            var result = XmlCodec.DeserializeFromFile<SerializationTestPayload>(TempPath("missing.xml"));
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("DeserializeFromFile wraps malformed content in an InvalidOperationException")]
        public void DeserializeFromFile_MalformedXml_Throws()
        {
            string path = TempPath("broken.xml");
            File.WriteAllText(path, "<not-valid-xml");

            var ex = Assert.Throws<InvalidOperationException>(
                () => XmlCodec.DeserializeFromFile<SerializationTestPayload>(path));
            Assert.Contains("DeserializeFromFile", ex.Message);
        }

        [Fact]
        [DisplayName("XmlSerializerCache.Get returns the same instance for the same type")]
        public void XmlSerializerCache_Get_ReturnsCachedInstance()
        {
            var a = XmlSerializerCache.Get(typeof(SerializationTestPayload));
            var b = XmlSerializerCache.Get(typeof(SerializationTestPayload));

            Assert.Same(a, b);
        }

        [Fact]
        [DisplayName("Utf8StringWriter.Encoding is UTF-8 without a BOM")]
        public void Utf8StringWriter_Encoding_IsUtf8NoBom()
        {
            using var writer = new Utf8StringWriter();
            var encoding = (System.Text.UTF8Encoding)writer.Encoding;

            Assert.Equal("utf-8", encoding.WebName);
            Assert.Empty(encoding.GetPreamble());
        }
    }
}
