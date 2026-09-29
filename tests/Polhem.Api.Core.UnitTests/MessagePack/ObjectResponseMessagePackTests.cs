using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// MessagePack byte round-trip tests for the definition responses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GetFormSchemaResponse, GetFormLayoutResponse and GetLanguageResponse used to put the Define objects on the wire
    /// directly, handled by ContractlessStandardResolver reflection. That path was broken on the <b>way back</b>: the
    /// nested collections of definition types are get-only, and JSON / MessagePack bind by writability, so they could
    /// be sent but not received. A .NET caller got an empty shell with only the scalar fields and no Tables/Sections,
    /// and no error.
    /// </para>
    /// <para>
    /// They now all carry an XML string instead. So this test checks two things: the string survives the byte
    /// round-trip on the wire, and when the restored XML is deserialized back into a definition object, <b>the nested
    /// collections are really there</b>. The latter is exactly what the old approach lost and what this change is
    /// meant to guarantee.
    /// </para>
    /// </remarks>
    public class ObjectResponseMessagePackTests
    {
        [Fact]
        [DisplayName("After a GetFormSchemaResponse byte round-trip, the XML still restores a FormSchema with its Tables")]
        public void GetFormSchemaResponse_ByteRoundTrip_PreservesNestedCollections()
        {
            var schema = new FormSchema("Employee", "員工資料");
            schema.Tables!.Add("Employee", "員工主檔");
            var original = new GetFormSchemaResponse { Xml = XmlCodec.Serialize(schema) };

            byte[] bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<GetFormSchemaResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(original.Xml, restored.Xml);
            var roundTripped = XmlCodec.Deserialize<FormSchema>(restored.Xml!);
            Assert.NotNull(roundTripped);
            Assert.Equal("Employee", roundTripped!.ProgId);
            Assert.Equal("員工資料", roundTripped.DisplayName);
            // The old approach failed here: the collection came back empty.
            Assert.Single(roundTripped.Tables!);
        }

        [Fact]
        [DisplayName("After a GetFormLayoutResponse byte round-trip, the XML still restores a FormLayout with its Sections")]
        public void GetFormLayoutResponse_ByteRoundTrip_PreservesNestedCollections()
        {
            var layout = new FormLayout { LayoutId = "Employee", ProgId = "Employee", Caption = "員工資料", ColumnCount = 3 };
            layout.Sections!.Add(new LayoutSection { Name = "Main", Caption = "主要資料" });
            var original = new GetFormLayoutResponse { Xml = XmlCodec.Serialize(layout) };

            byte[] bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<GetFormLayoutResponse>(bytes);

            Assert.NotNull(restored);
            var roundTripped = XmlCodec.Deserialize<FormLayout>(restored.Xml!);
            Assert.NotNull(roundTripped);
            Assert.Equal("Employee", roundTripped!.LayoutId);
            Assert.Equal(3, roundTripped.ColumnCount);
            Assert.Single(roundTripped.Sections!);
        }

        [Fact]
        [DisplayName("After a GetLanguageResponse byte round-trip, the XML still restores a LanguageResource with its Items")]
        public void GetLanguageResponse_ByteRoundTrip_PreservesNestedCollections()
        {
            var resource = new LanguageResource { Namespace = "Common", Lang = "zh-TW" };
            resource.Items.Add("Greeting", "你好");
            var original = new GetLanguageResponse { Xml = XmlCodec.Serialize(resource) };

            byte[] bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<GetLanguageResponse>(bytes);

            Assert.NotNull(restored);
            var roundTripped = XmlCodec.Deserialize<LanguageResource>(restored.Xml!);
            Assert.NotNull(roundTripped);
            Assert.Equal("Common", roundTripped!.Namespace);
            Assert.Equal("你好", roundTripped.GetText("Greeting"));
        }

        [Fact]
        [DisplayName("An empty Xml (returned when the definition does not exist) stays empty after a byte round-trip")]
        public void EmptyXml_ByteRoundTrip_StaysEmpty()
        {
            var original = new GetFormLayoutResponse { Xml = string.Empty };

            var restored = MessagePackCodec.Deserialize<GetFormLayoutResponse>(MessagePackCodec.Serialize(original));

            Assert.NotNull(restored);
            Assert.True(string.IsNullOrEmpty(restored.Xml));
        }
    }
}
