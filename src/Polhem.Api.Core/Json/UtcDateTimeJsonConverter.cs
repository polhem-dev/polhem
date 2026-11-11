using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polhem.Api.Core.Wire;

namespace Polhem.Api.Core.Json
{
    /// <summary>
    /// Writes and reads the <see cref="DateTime"/> members of the JSON body codec as UTC instants, the way the MessagePack
    /// codec does. The rule and its reason are on <see cref="WireDateTime"/>.
    /// </summary>
    /// <remarks>
    /// The text format is System.Text.Json's own ISO 8601 form, so a UTC value is written exactly as before.
    /// <see cref="System.Data.DataTable"/> cells do not reach this converter: the table converter writes them itself.
    /// </remarks>
    internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
    {
        /// <inheritdoc />
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // The reader's own ISO 8601 check decides what is accepted, as with the default converter; the parse below
            // only moves the value to UTC without passing through this process's time zone.
            if (reader.TokenType != JsonTokenType.String || !(reader.TryGetDateTime(out _) || reader.TryGetDateTimeOffset(out _)))
            {
                throw new JsonException("The value is not an ISO 8601 date and time.");
            }
            try
            {
                return DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, WireDateTime.ParseStyles);
            }
            catch (FormatException ex)
            {
                throw new JsonException("The date and time is outside the supported range.", ex);
            }
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(WireDateTime.ToUtc(value));
    }
}
