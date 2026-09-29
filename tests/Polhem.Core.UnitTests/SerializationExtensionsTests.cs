using System.ComponentModel;
using Polhem.Core.Serialization;

namespace Polhem.Core.UnitTests
{
    public class SerializationExtensionsTests : SerializationTestBase
    {
        [Fact]
        [DisplayName("SerializationExtensions.ToXml / ToJson produce the same results as XmlCodec / JsonCodec")]
        public void SerializationExtensions_ToXmlAndToJson_WorkAsFacade()
        {
            var source = new SerializationTestPayload { Name = "Frank", Age = 18 };

            string xml = source.ToXml();
            string json = source.ToJson();

            Assert.Equal(XmlCodec.Serialize(new SerializationTestPayload { Name = "Frank", Age = 18 }), xml);
            Assert.Contains("\"name\"", json);
        }

        [Fact]
        [DisplayName("SerializationExtensions.Save writes the format matching the file extension")]
        public void SerializationExtensions_Save_DispatchesByExtension()
        {
            var xmlSource = new SerializationTestPayload { Name = "Henry", Age = 1 };
            string xmlPath = TempPath("save.xml");
            xmlSource.SetObjectFilePath(xmlPath);
            xmlSource.Save();
            Assert.True(File.Exists(xmlPath));

            var jsonSource = new SerializationTestPayload { Name = "Ivy", Age = 2 };
            string jsonPath = TempPath("save.json");
            jsonSource.SetObjectFilePath(jsonPath);
            jsonSource.Save();
            Assert.True(File.Exists(jsonPath));
        }

        [Fact]
        [DisplayName("SerializationExtensions.Save throws ArgumentException for an empty ObjectFilePath")]
        public void SerializationExtensions_Save_EmptyPath_Throws()
        {
            var source = new SerializationTestPayload { Name = "John", Age = 5 };
            Assert.Throws<ArgumentException>(() => source.Save());
        }

        [Fact]
        [DisplayName("SerializationExtensions.Save throws NotSupportedException for an unsupported file extension")]
        public void SerializationExtensions_Save_UnknownExtension_Throws()
        {
            var source = new SerializationTestPayload { Name = "Kate", Age = 6 };
            source.SetObjectFilePath(TempPath("save.dat"));
            Assert.Throws<NotSupportedException>(() => source.Save());
        }
    }
}
