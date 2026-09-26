using System.ComponentModel;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests
{
    public class JsonCodecTests : SerializationTestBase
    {
        private static readonly SerializeState[] s_expectedStateChanges =
            { SerializeState.Serialize, SerializeState.None };

        [Fact]
        [DisplayName("JSON serialization raises SerializeState while it runs and clears it afterwards")]
        public void Json_Serialize_RaisesAndClearsSerializeState()
        {
            var source = new SerializationTestPayload { Name = "Carol", Age = 40 };

            string json = JsonCodec.Serialize(source);

            Assert.Equal(s_expectedStateChanges, source.StateChanges);
            Assert.Equal(SerializeState.None, source.SerializeState);

            // Deserialization must not touch the state, so `IsSerializeEmpty` keeps every value.
            var restored = JsonCodec.Deserialize<SerializationTestPayload>(json)!;
            Assert.Equal("Carol", restored.Name);
            Assert.Equal(40, restored.Age);
            Assert.Empty(restored.StateChanges);
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
