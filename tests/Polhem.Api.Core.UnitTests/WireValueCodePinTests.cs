using System.Buffers;
using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Wire;
using Polhem.Definition.Collections;
using MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Pins the discriminators of <c>WireValueCode</c> on the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These values **are part of the wire format**: renumbering makes old and new versions read the same bytes as
    /// different types, and **silently** so. `WireContractDriftTests` compares whether a type is registered, not its
    /// number, and a round-trip test encodes and decodes in the same process, so it still passes when both sides change.
    /// </para>
    /// <para>
    /// So this file tests two layers: the constant table itself (any change turns it red), and **the bytes actually
    /// written** (pinned together with the envelope framing). Pinning only the constants is not enough: someone who
    /// changes the constants and the test table together still passes silently. With the encoding pinned too, that
    /// change must also change the golden values, and it reads as an explicit wire change.
    /// </para>
    /// </remarks>
    public class WireValueCodePinTests
    {
        /// <summary>
        /// The authoritative table of discriminators. **A new type only takes the next number; existing ones never change.**
        /// </summary>
        public static TheoryData<int, int, string> PinnedCodes => new()
        {
            { WireValueCode.Boolean,        1, nameof(WireValueCode.Boolean) },
            { WireValueCode.Byte,           2, nameof(WireValueCode.Byte) },
            { WireValueCode.SByte,          3, nameof(WireValueCode.SByte) },
            { WireValueCode.Int16,          4, nameof(WireValueCode.Int16) },
            { WireValueCode.UInt16,         5, nameof(WireValueCode.UInt16) },
            { WireValueCode.Int32,          6, nameof(WireValueCode.Int32) },
            { WireValueCode.UInt32,         7, nameof(WireValueCode.UInt32) },
            { WireValueCode.Int64,          8, nameof(WireValueCode.Int64) },
            { WireValueCode.UInt64,         9, nameof(WireValueCode.UInt64) },
            { WireValueCode.Single,        10, nameof(WireValueCode.Single) },
            { WireValueCode.Double,        11, nameof(WireValueCode.Double) },
            { WireValueCode.Decimal,       12, nameof(WireValueCode.Decimal) },
            { WireValueCode.String,        13, nameof(WireValueCode.String) },
            { WireValueCode.DateTime,      14, nameof(WireValueCode.DateTime) },
            { WireValueCode.DateTimeOffset, 15, nameof(WireValueCode.DateTimeOffset) },
            { WireValueCode.TimeSpan,      16, nameof(WireValueCode.TimeSpan) },
            { WireValueCode.DateOnly,      17, nameof(WireValueCode.DateOnly) },
            { WireValueCode.Guid,          18, nameof(WireValueCode.Guid) },
            { WireValueCode.ByteArray,     19, nameof(WireValueCode.ByteArray) },
            { WireValueCode.DBNull,        20, nameof(WireValueCode.DBNull) },
            { WireValueCode.DataTable,     21, nameof(WireValueCode.DataTable) },
            { WireValueCode.ObjectArray,   22, nameof(WireValueCode.ObjectArray) },
        };

        [Theory]
        [MemberData(nameof(PinnedCodes))]
        [DisplayName("Every WireValueCode discriminator keeps its pinned value")]
        public void Code_KeepsItsPinnedValue(int actual, int expected, string name)
        {
            Assert.True(
                actual == expected,
                $"WireValueCode.{name} changed from {expected} to {actual}. Discriminators are part of the wire format; " +
                "changing one makes old and new versions read the same bytes as different types. Give a new type the next number.");
        }

        [Fact]
        [DisplayName("Count is exactly the highest discriminator plus one (it sizes the dispatch table)")]
        public void Count_IsOnePastTheHighestCode()
        {
            var highest = PinnedCodes.Select(row => (int)row[1]).Max();

            Assert.Equal(WireValueCode.Count, highest + 1);
        }

        /// <summary>
        /// One sample value per discriminator, used to verify **the bytes actually written**.
        /// </summary>
        /// <remarks>
        /// WARNING: The expected values are **literal numbers** and must not be changed to <c>WireValueCode.X</c>.
        /// With the constants, the expected values would follow a renumbering, so the test would be self-consistent and
        /// always green. The first version was written that way: in a reverse check (swapping Guid and ByteArray) only
        /// the constant-table test went red, and this test stayed all green.
        /// </remarks>
        public static TheoryData<object, int> SampleValues => new()
        {
            { true,                                     1 },
            { (byte)8,                                  2 },
            { (sbyte)-8,                                3 },
            { (short)-16,                               4 },
            { (ushort)16,                               5 },
            { -32,                                      6 },
            { 32u,                                      7 },
            { -64L,                                     8 },
            { 64UL,                                     9 },
            { 1.5f,                                     10 },
            { 2.5d,                                     11 },
            { 99.99m,                                   12 },
            { "text",                                   13 },
            { new DateTime(2026, 8, 12, 1, 2, 3, DateTimeKind.Utc), 14 },
            { new DateTimeOffset(2026, 8, 12, 1, 2, 3, TimeSpan.FromHours(8)), 15 },
            { TimeSpan.FromMinutes(90),                 16 },
            { new DateOnly(2026, 8, 12),                17 },
            { Guid.Parse("11112222-3333-4444-5555-666677778888"), 18 },
            { new byte[] { 1, 2, 3 },                   19 },
            { DBNull.Value,                             20 },
            { new object[] { 1, "two" },                22 },
        };

        [Theory]
        // The sample values are deliberately `object` (discriminators exist for heterogeneous values), so they cannot be
        // serialized into separate data rows at discovery time. Discovery enumeration is therefore disabled (xUnit1045).
        // The tests still run; Test Explorer just shows them as a single item.
        [MemberData(nameof(SampleValues), DisableDiscoveryEnumeration = true)]
        [DisplayName("The first element the envelope actually writes is the discriminator of the value type")]
        public void Envelope_WritesTheExpectedDiscriminator(object value, int expectedCode)
        {
            var bytes = SerializeValue(value);
            var reader = new MessagePackReader(new ReadOnlySequence<byte>(bytes));

            // The envelope is a two-element array of discriminator and value. Asserting the array header too turns the test red if the framing itself changes.
            Assert.Equal(2, reader.ReadArrayHeader());
            Assert.Equal(expectedCode, reader.ReadInt32());
        }

        [Fact]
        [DisplayName("A DataTable value goes through the same envelope with discriminator 21")]
        public void Envelope_DataTable_WritesItsDiscriminator()
        {
            var table = new DataTable("t");
            table.Columns.Add("c", typeof(int));
            table.Rows.Add(1);

            var bytes = SerializeValue(table);
            var reader = new MessagePackReader(new ReadOnlySequence<byte>(bytes));

            Assert.Equal(2, reader.ReadArrayHeader());
            Assert.Equal(21, reader.ReadInt32());
        }

        [Fact]
        [DisplayName("DateTimeOffset round-trips and keeps its offset")]
        public void DateTimeOffset_RoundTrips_PreservingOffset()
        {
            // Discriminator 15 used to be the only branch without a round-trip test.
            var value = new DateTimeOffset(2026, 8, 12, 1, 2, 3, TimeSpan.FromHours(8));

            var source = new ParameterCollection { { "v", value } };
            var restored = MessagePackCodec.Deserialize<ParameterCollection>(MessagePackCodec.Serialize(source));

            var actual = Assert.IsType<DateTimeOffset>(restored!["v"].Value);
            Assert.Equal(value, actual);
            Assert.Equal(TimeSpan.FromHours(8), actual.Offset);
        }

        /// <summary>
        /// Returns the raw bytes that <c>WireValueFormatter</c> writes for a single value.
        /// </summary>
        /// <remarks>
        /// Going through <c>ParameterCollection</c> and slicing the bytes would mix in the outer envelope, so the
        /// formatter is called directly. The options come from <c>MessagePackCodec</c> so that the resolver is the same
        /// one the production path uses.
        /// </remarks>
        private static byte[] SerializeValue(object value)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            WireValueFormatter.Instance.Serialize(ref writer, value, MessagePackCodec.SerializerOptions);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
    }
}
