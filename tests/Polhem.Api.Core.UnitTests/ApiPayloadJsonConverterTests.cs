using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Read/Write tests for ApiPayloadJsonConverter.
    /// </summary>
    public class ApiPayloadJsonConverterTests
    {
        private static JsonRpcParams? Deserialize(string json) =>
            JsonSerializer.Deserialize<JsonRpcParams>(json);

        private static string Serialize(JsonRpcParams payload) =>
            JsonSerializer.Serialize(payload);

        [Fact]
        [DisplayName("Read returns null for a null token")]
        public void Read_NullToken_ReturnsNull()
        {
            var result = Deserialize("null");
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Read throws JsonException for a token that is not StartObject")]
        public void Read_NonStartObject_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => Deserialize("123"));
        }

        [Fact]
        [DisplayName("Read of a Plain string value returns the string")]
        public void Read_PlainStringValue_ReturnsString()
        {
            var json = """{"format":0,"value":"hello","type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal(PayloadFormat.Plain, payload!.Format);
            Assert.Equal("hello", payload.Value);
        }

        [Fact]
        [DisplayName("Read of a Plain integer value parses it as long")]
        public void Read_PlainIntegerValue_ReturnsLong()
        {
            var json = """{"format":0,"value":42,"type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal(42L, payload!.Value);
        }

        [Fact]
        [DisplayName("Read of a Plain floating-point value parses it as double")]
        public void Read_PlainDoubleValue_ReturnsDouble()
        {
            var json = """{"format":0,"value":3.14,"type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal(3.14d, payload!.Value);
        }

        [Fact]
        [DisplayName("Read of a Plain true value parses it as bool")]
        public void Read_PlainTrueValue_ReturnsTrue()
        {
            var json = """{"format":0,"value":true,"type":""}""";
            var payload = Deserialize(json);

            Assert.True((bool)payload!.Value!);
        }

        [Fact]
        [DisplayName("Read of a Plain false value parses it as bool")]
        public void Read_PlainFalseValue_ReturnsFalse()
        {
            var json = """{"format":0,"value":false,"type":""}""";
            var payload = Deserialize(json);

            Assert.False((bool)payload!.Value!);
        }

        [Fact]
        [DisplayName("Read of a Plain null value returns a null Value")]
        public void Read_PlainNullValue_ReturnsNullValue()
        {
            var json = """{"format":0,"value":null,"type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Null(payload!.Value);
        }

        [Fact]
        [DisplayName("Read of a Plain object value keeps it as a JsonElement")]
        public void Read_PlainObjectValue_ReturnsJsonElement()
        {
            var json = """{"format":0,"value":{"a":1},"type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.IsType<JsonElement>(payload!.Value);
            var elem = (JsonElement)payload.Value!;
            Assert.Equal(JsonValueKind.Object, elem.ValueKind);
        }

        [Fact]
        [DisplayName("Read of an Encoded base64 string value decodes it to byte[]")]
        public void Read_EncodedBase64Value_ReturnsByteArray()
        {
            var original = Encoding.UTF8.GetBytes("raw-bytes");
            var base64 = Convert.ToBase64String(original);
            var json = $$"""{"format":1,"value":"{{base64}}","type":"System.Byte[]"}""";

            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal(PayloadFormat.Encoded, payload!.Format);
            Assert.IsType<byte[]>(payload.Value);
            Assert.Equal(original, (byte[])payload.Value!);
        }

        [Fact]
        [DisplayName("Read of an Encoded string value that is not base64 returns the string")]
        public void Read_EncodedInvalidBase64Value_ReturnsString()
        {
            // Characters outside the base64 alphabet make decoding throw `FormatException`.
            var json = """{"format":1,"value":"not base64!@#","type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal(PayloadFormat.Encoded, payload!.Format);
            Assert.Equal("not base64!@#", payload.Value);
        }

        [Fact]
        [DisplayName("Read of an Encoded non-string value falls back to the Plain parsing path")]
        public void Read_EncodedNonStringValue_FallsBackToPlain()
        {
            var json = """{"format":1,"value":99,"type":""}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal(PayloadFormat.Encoded, payload!.Format);
            Assert.Equal(99L, payload.Value);
        }

        [Fact]
        [DisplayName("Read skips unknown properties")]
        public void Read_UnknownProperty_IsSkipped()
        {
            var json = """{"format":0,"value":"x","type":"","extra":{"nested":true}}""";
            var payload = Deserialize(json);

            Assert.NotNull(payload);
            Assert.Equal("x", payload!.Value);
        }

        [Fact]
        [DisplayName("Write outputs null for a null payload")]
        public void Write_NullPayload_WritesNull()
        {
            JsonRpcParams? payload = null;
            var json = JsonSerializer.Serialize(payload);
            Assert.Equal("null", json);
        }

        [Fact]
        [DisplayName("Write outputs value=null when Value is null")]
        public void Write_NullValue_WritesNullValue()
        {
            var payload = new JsonRpcParams { Value = null, TypeName = "" };
            var json = Serialize(payload);
            Assert.Contains("\"value\":null", json);
            Assert.Contains("\"format\":0", json);
        }

        [Fact]
        [DisplayName("Write outputs a JSON string when Value is a string")]
        public void Write_StringValue_WritesStringValue()
        {
            var payload = new JsonRpcParams { Value = "hello", TypeName = "System.String" };
            var json = Serialize(payload);
            Assert.Contains("\"value\":\"hello\"", json);
            Assert.Contains("\"type\":\"System.String\"", json);
        }

        [Fact]
        [DisplayName("Read/Write round-trip preserves a string value")]
        public void ReadWrite_RoundTrip_PreservesStringValue()
        {
            var original = new JsonRpcParams { Value = "round-trip", TypeName = "" };
            var json = Serialize(original);
            var restored = Deserialize(json);

            Assert.NotNull(restored);
            Assert.Equal("round-trip", restored!.Value);
        }

        [Fact]
        [DisplayName("ApiPayloadJsonConverterFactory can convert concrete ApiPayload subclasses")]
        public void Factory_CanConvert_ConcreteSubtype()
        {
            var factory = new ApiPayloadJsonConverterFactory();
            Assert.True(factory.CanConvert(typeof(JsonRpcParams)));
            Assert.True(factory.CanConvert(typeof(JsonRpcResult)));
        }

        [Fact]
        [DisplayName("ApiPayloadJsonConverterFactory cannot convert the abstract ApiPayload or unrelated types")]
        public void Factory_CanConvert_RejectsAbstractOrUnrelated()
        {
            var factory = new ApiPayloadJsonConverterFactory();
            Assert.False(factory.CanConvert(typeof(ApiPayload)));
            Assert.False(factory.CanConvert(typeof(string)));
        }

        [Fact]
        [DisplayName("ApiPayloadJsonConverterFactory.CreateConverter returns the matching generic converter")]
        public void Factory_CreateConverter_ReturnsGenericConverter()
        {
            var factory = new ApiPayloadJsonConverterFactory();
            var converter = factory.CreateConverter(typeof(JsonRpcParams), new JsonSerializerOptions());

            Assert.NotNull(converter);
            Assert.IsType<ApiPayloadJsonConverter<JsonRpcParams>>(converter);
        }

        [Fact]
        [DisplayName("Read on the converter called directly returns null for a Null token (covers the TokenType.Null branch)")]
        public void Read_DirectConverter_NullToken_ReturnsNull()
        {
            // `JsonSerializer.Deserialize<T>("null")` is short-circuited by the framework and never reaches
            // `converter.Read`, so the converter has to be called directly to cover the `TokenType.Null` branch.
            var converter = new ApiPayloadJsonConverter<JsonRpcParams>();
            var bytes = Encoding.UTF8.GetBytes("null");
            var reader = new Utf8JsonReader(bytes);
            Assert.True(reader.Read());

            var result = converter.Read(ref reader, typeof(JsonRpcParams), new JsonSerializerOptions());
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Write on the converter called directly writes JSON null for a null value")]
        public void Write_DirectConverter_NullValue_WritesNull()
        {
            // `JsonSerializer.Serialize(null)` is short-circuited by the framework and never reaches
            // `converter.Write`, so the converter has to be called directly to cover the `value == null` branch.
            var converter = new ApiPayloadJsonConverter<JsonRpcParams>();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                converter.Write(writer, null!, new JsonSerializerOptions());
            }

            var json = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("null", json);
        }

        /// <summary>
        /// Tests serialization of the format, value and type properties of ApiPayload (through JsonRpcParams).
        /// </summary>
        [Theory]
        [InlineData(PayloadFormat.Plain)]
        [InlineData(PayloadFormat.Encoded)]
        [InlineData(PayloadFormat.Encrypted)]
        [DisplayName("ApiPayload JSON serialization preserves Format and TypeName")]
        public void ApiPayload_Serialize_PreservesFormatAndTypeName(PayloadFormat format)
        {
            // Create payload with the specified format via JSON round-trip
            var tempJson = JsonSerializer.Serialize(new { format = (int)format, value = "sample-data", type = "Polhem.Api.Core.Messages.System.PingRequest" });
            var payload = JsonSerializer.Deserialize<JsonRpcParams>(tempJson)!;

            var json = JsonSerializer.Serialize(payload);
            var deserialized = JsonSerializer.Deserialize<JsonRpcParams>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(format, deserialized.Format);
            Assert.Equal("sample-data", deserialized.Value);
            Assert.Equal("Polhem.Api.Core.Messages.System.PingRequest", deserialized.TypeName);

            using var jDoc = JsonDocument.Parse(json);
            var root = jDoc.RootElement;
            Assert.Equal((int)format, root.GetProperty("format").GetInt32());
        }
    }
}
