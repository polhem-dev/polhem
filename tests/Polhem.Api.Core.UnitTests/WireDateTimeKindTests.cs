using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Checks that both body codecs deliver a <see cref="DateTime"/> outside a <see cref="DataTable"/> as the same UTC
    /// instant, whatever its <see cref="DateTime.Kind"/> was when it was sent.
    /// </summary>
    /// <remarks>
    /// <c>WireCodecParityTests</c> samples every <see cref="DateTime"/> as UTC, the one kind on which the two codecs
    /// always agreed. An Unspecified value used to arrive as Unspecified over JSON and as UTC over MessagePack, and a
    /// Local value arrived in the reader's time zone over JSON.
    /// </remarks>
    public class WireDateTimeKindTests
    {
        private static readonly IPayloadCodec[] s_codecs =
            [new MessagePackPayloadCodec(), new JsonPayloadCodec(JsonBodyOptions.Options)];

        public static TheoryData<DateTimeKind> Kinds() =>
            [DateTimeKind.Utc, DateTimeKind.Unspecified, DateTimeKind.Local];

        private static DateTime Sample(DateTimeKind kind) => new DateTime(2026, 1, 1, 9, 0, 0, kind);

        private static DateTime ExpectedUtc(DateTime sent) => sent.Kind switch
        {
            DateTimeKind.Local => sent.ToUniversalTime(),
            _ => DateTime.SpecifyKind(sent, DateTimeKind.Utc)
        };

        private static T RoundTrip<T>(IPayloadCodec codec, T value) where T : class
            => (T)codec.Deserialize(codec.Serialize(value, typeof(T)), typeof(T))!;

        private static void AssertUtc(DateTime expected, object? actual, string where)
        {
            var time = Assert.IsType<DateTime>(actual);
            Assert.True(
                time == expected && time.Kind == DateTimeKind.Utc,
                $"{where}: expected {expected:O}, got {time:O} ({time.Kind})");
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        [DisplayName("A parameter value arrives as the same UTC instant over MessagePack and JSON")]
        public void ParameterValue_ArrivesAsUtc_OnBothCodecs(DateTimeKind kind)
        {
            var sent = Sample(kind);
            foreach (var codec in s_codecs)
            {
                var restored = RoundTrip(codec, new Parameter("v", sent));
                AssertUtc(ExpectedUtc(sent), restored.Value, codec.Name);
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        [DisplayName("Filter values, including the elements of an In list, arrive as the same UTC instant over MessagePack and JSON")]
        public void FilterValues_ArriveAsUtc_OnBothCodecs(DateTimeKind kind)
        {
            var sent = Sample(kind);
            var later = sent.AddDays(1);
            var request = new GetListRequest
            {
                Filter = FilterGroup.All(
                    new FilterCondition("from_date", ComparisonOperator.Between, sent, later),
                    new FilterCondition("due_date", ComparisonOperator.In, new object[] { sent }))
            };

            foreach (var codec in s_codecs)
            {
                var group = Assert.IsType<FilterGroup>(RoundTrip(codec, request).Filter);
                var between = Assert.IsType<FilterCondition>(group.Nodes[0]);
                AssertUtc(ExpectedUtc(sent), between.Value, $"{codec.Name} Value");
                AssertUtc(ExpectedUtc(later), between.SecondValue, $"{codec.Name} SecondValue");
                var list = Assert.IsType<object[]>(Assert.IsType<FilterCondition>(group.Nodes[1]).Value);
                AssertUtc(ExpectedUtc(sent), Assert.Single(list), $"{codec.Name} In");
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        [DisplayName("A DateTime property of a message arrives as the same UTC instant over MessagePack and JSON")]
        public void MessageProperty_ArrivesAsUtc_OnBothCodecs(DateTimeKind kind)
        {
            var sent = Sample(kind);
            foreach (var codec in s_codecs)
            {
                AssertUtc(ExpectedUtc(sent), RoundTrip(codec, new PingResponse { ServerTime = sent }).ServerTime, codec.Name);
                AssertUtc(ExpectedUtc(sent), RoundTrip(codec, new SetApiKeyExpiryRequest { ExpiredAt = sent }).ExpiredAt,
                    $"{codec.Name} nullable");
            }
        }

        [Fact]
        [DisplayName("A JSON body DateTime written without an offset is read as UTC, as MessagePack reads every timestamp")]
        public void JsonBody_ValueWithoutOffset_IsReadAsUtc()
        {
            var codec = new JsonPayloadCodec(JsonBodyOptions.Options);
            var body = Encoding.UTF8.GetBytes("""{"serverTime":"2026-01-01T09:00:00"}""");

            var restored = (PingResponse)codec.Deserialize(body, typeof(PingResponse))!;

            AssertUtc(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), restored.ServerTime, "json");
        }

        [Fact]
        [DisplayName("A JSON body DateTime written with an offset is read as that UTC instant, whatever this process's time zone")]
        public void JsonBody_ValueWithOffset_IsReadAsItsUtcInstant()
        {
            var codec = new JsonPayloadCodec(JsonBodyOptions.Options);
            var body = Encoding.UTF8.GetBytes("""{"serverTime":"2026-01-01T09:00:00+08:00"}""");

            var restored = (PingResponse)codec.Deserialize(body, typeof(PingResponse))!;

            AssertUtc(new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc), restored.ServerTime, "json");
        }

        [Theory]
        [InlineData("9999-12-31T20:00:00.0000000-01:00", "9999-12-31T21:00:00.0000000Z")]
        [InlineData("0001-01-01T03:00:00.0000000+01:00", "0001-01-01T02:00:00.0000000Z")]
        [DisplayName("A JSON DateTime with an offset near either end of the range keeps its instant, as a message property and as a value")]
        public void JsonBody_OffsetNearRangeEnd_KeepsItsInstant(string sent, string expected)
        {
            var codec = new JsonPayloadCodec(JsonBodyOptions.Options);
            var want = DateTime.Parse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            var message = (PingResponse)codec.Deserialize(
                Encoding.UTF8.GetBytes($$"""{"serverTime":"{{sent}}"}"""), typeof(PingResponse))!;
            var parameter = (Parameter)codec.Deserialize(
                Encoding.UTF8.GetBytes($$"""{"name":"v","value":[14,"{{sent}}"]}"""), typeof(Parameter))!;

            AssertUtc(want, message.ServerTime, "property");
            AssertUtc(want, parameter.Value, "value");
        }

        [Fact]
        [DisplayName("An Unspecified DateTime is written to a JSON body with the UTC marker")]
        public void JsonBody_UnspecifiedValue_IsWrittenAsUtc()
        {
            var codec = new JsonPayloadCodec(JsonBodyOptions.Options);
            var sent = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

            var property = Encoding.UTF8.GetString(codec.Serialize(new PingResponse { ServerTime = sent }, typeof(PingResponse)));
            var value = Encoding.UTF8.GetString(codec.Serialize(new Parameter("v", sent), typeof(Parameter)));

            Assert.Contains("\"2026-01-01T09:00:00Z\"", property, StringComparison.Ordinal);
            Assert.Contains("\"2026-01-01T09:00:00.0000000Z\"", value, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A DataTable cell keeps its naive value over JSON and is not written as a UTC instant")]
        public void DataTableCell_StaysNaive_OnJson()
        {
            var table = new DataTable("t");
            var column = table.Columns.Add("hired_at", typeof(DateTime));
            column.DateTimeMode = DataSetDateTime.Unspecified;
            table.Rows.Add(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified));
            var codec = new JsonPayloadCodec(JsonBodyOptions.Options);

            var json = Encoding.UTF8.GetString(codec.Serialize(table, typeof(DataTable)));

            Assert.Contains("\"2026-01-01T09:00:00\"", json, StringComparison.Ordinal);
        }
    }
}
