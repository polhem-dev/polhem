using System.Text.Json;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.Json
{
    /// <summary>
    /// Reads an <see cref="object"/>-typed member of a <c>Plain</c> body (a filter value, a parameter
    /// value) into a CLR value, instead of the <see cref="JsonElement"/> System.Text.Json produces by
    /// default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: without this, every such value reaches the business object and the data layer as a
    /// <see cref="JsonElement"/>. No database provider can bind one as a command parameter, so a
    /// <c>Plain</c> list request filtering with <c>Equal</c>, a range, <c>Between</c> or <c>In</c>
    /// failed at the SQL parameter, and host code saw a different type for the same parameter
    /// depending on the payload format.
    /// </para>
    /// <para>
    /// A bare JSON value names no CLR type, so the mapping is by JSON kind: a string is a
    /// <see cref="string"/>, a number is a <see cref="long"/> when it is an integer that fits and a
    /// <see cref="decimal"/> otherwise (a <see cref="double"/> only past the decimal range), a boolean
    /// is a <see cref="bool"/>, <c>null</c> is <c>null</c>, and an array is an <see cref="object"/>
    /// array read element by element with the same rules — the shape <c>In</c> expects. A JSON
    /// object has no CLR counterpart and stays a <see cref="JsonElement"/>.
    /// </para>
    /// <para>
    /// NOTE: a number becomes a <see cref="decimal"/>, not a <see cref="double"/>, because a
    /// fractional filter value is almost always money or a quantity, and a binary fraction compared
    /// against a decimal column misses rows. A string is never guessed into a date or a
    /// <see cref="Guid"/>: a code such as <c>2026-01-01</c> would then stop matching its own text
    /// column. A client that needs typed values declares the JSON body codec, whose envelope carries
    /// the type.
    /// </para>
    /// <para>
    /// Registered only in the options that read a <c>Plain</c> body. It must never join the JSON body
    /// codec's options: that wire carries the discriminated envelope, read by
    /// <see cref="WireValueJsonConverter"/>.
    /// </para>
    /// </remarks>
    internal sealed class PlainValueJsonConverter : JsonConverter<object>
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly PlainValueJsonConverter Instance = new PlainValueJsonConverter();

        private PlainValueJsonConverter() { }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

        /// <inheritdoc />
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    return ReadNumber(ref reader);
                case JsonTokenType.StartArray:
                    return ReadArray(ref reader, options);
                default:
                    using (var document = JsonDocument.ParseValue(ref reader))
                    {
                        return document.RootElement.Clone();
                    }
            }
        }

        private static object ReadNumber(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt64(out var integer))
                return integer;
            if (reader.TryGetDecimal(out var number))
                return number;
            return reader.GetDouble();
        }

        private object?[] ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var items = new List<object?>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                items.Add(Read(ref reader, typeof(object), options));

            return items.ToArray();
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var type = value.GetType();
            if (type == typeof(object))
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
                return;
            }

            // The runtime type, as System.Text.Json writes an `object` member without this converter.
            // Passing `typeof(object)` would come straight back here.
            JsonSerializer.Serialize(writer, value, type, options);
        }
    }
}
