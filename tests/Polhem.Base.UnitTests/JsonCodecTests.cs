using System.ComponentModel;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests
{
    public class JsonCodecTests : SerializationTestBase
    {
        [Fact]
        [DisplayName("Serialize honours an inherited Specified property the same way XmlSerializer does")]
        public void Serialize_SpecifiedProperty_ControlsProperty()
        {
            var empty = new SpecifiedPayload { Name = "Carol" };
            var filled = new SpecifiedPayload { Name = "Carol", Tags = ["a"] };

            string emptyJson = JsonCodec.Serialize(empty, ignoreDefaultValue: false, ignoreNullValue: false);
            string filledJson = JsonCodec.Serialize(filled);

            Assert.DoesNotContain("\"tags\"", emptyJson, StringComparison.Ordinal);
            Assert.DoesNotContain("tagsSpecified", filledJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"tags\":[\"a\"]", filledJson, StringComparison.Ordinal);
            Assert.Equal(["a"], JsonCodec.Deserialize<SpecifiedPayload>(filledJson)!.Tags);
        }

        [Fact]
        [DisplayName("Serialize does not consult ShouldSerialize methods, so JSON output of types using them for XML is unchanged")]
        public void Serialize_ShouldSerializeMethod_IsNotConsulted()
        {
            var payload = new SpecifiedPayload { Name = "Carol" };

            string json = JsonCodec.Serialize(payload);

            Assert.Contains("\"notes\":[]", json, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SerializeToFile / DeserializeFromFile round-trip and set ObjectFilePath")]
        public void JsonFile_Roundtrip_SetsObjectFilePath()
        {
            var source = new SerializationTestPayload { Name = "Eve", Age = 33 };
            string path = TempPath("payload.json");

            JsonCodec.SerializeToFile(source, path);

            Assert.True(File.Exists(path));
            Assert.Equal(path, source.ObjectFilePath);

            var restored = JsonCodec.DeserializeFromFile<SerializationTestPayload>(path)!;
            Assert.Equal("Eve", restored.Name);
            Assert.Equal(path, restored.ObjectFilePath);
        }

        [Fact]
        [DisplayName("DeserializeFromFile throws InvalidOperationException when the file does not exist")]
        public void DeserializeFromFile_MissingFile_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => JsonCodec.DeserializeFromFile<SerializationTestPayload>(TempPath("missing.json")));
            Assert.Contains("DeserializeFromFile", ex.Message);
        }
    }
}
