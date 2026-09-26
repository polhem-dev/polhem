using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Polhem.Api.Core.MessagePack;
using Polhem.Base.Data;
using Polhem.Base.Serialization;
using Polhem.Definition.Collections;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Pins the invariant "serialization must not shift time values by a time zone offset" across the MessagePack,
    /// JSON and XML paths.
    /// </summary>
    /// <remarks>
    /// The paths do not behave symmetrically, so each missing test leaves one format unguarded.
    /// MessagePack and JSON always write naive values and are unaffected by <c>DataColumn.DateTimeMode</c>.
    /// XML is the only format that decides by <c>DateTimeMode</c> whether to write an offset, and the .NET default
    /// <c>UnspecifiedLocal</c> is exactly the value that writes one. Once an offset is in the XML, reading it back in
    /// another time zone shifts the value, possibly across a date boundary. The design background is in
    /// docs/adr/adr-032-datetime-timezone.md.
    ///
    /// The tests must hold in any time zone (developer machines are mostly Asia/Taipei, CI is UTC), so every expected
    /// value that depends on the local time zone is derived from <see cref="TimeZoneInfo.Local"/> instead of a
    /// hard-coded offset.
    /// </remarks>
    public class DateTimeSerializationOffsetTests
    {
        private static readonly CultureInfo s_inv = CultureInfo.InvariantCulture;

        /// <summary>The wall-clock value every case starts from.</summary>
        private static readonly DateTime s_sample = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        /// <summary>
        /// A DataSet XML document whose single DateTime column carries an explicit
        /// <c>msdata:DateTimeMode</c>, so read-side behaviour can be tested per mode.
        /// </summary>
        private const string XmlTemplate =
            "<S><xs:schema id=\"S\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">" +
            "<xs:element name=\"S\" msdata:IsDataSet=\"true\"><xs:complexType><xs:choice maxOccurs=\"unbounded\">" +
            "<xs:element name=\"T\"><xs:complexType><xs:sequence>" +
            "<xs:element name=\"d\" msdata:DateTimeMode=\"{0}\" type=\"xs:dateTime\" minOccurs=\"0\" />" +
            "</xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema>" +
            "<T><d>{1}</d></T></S>";

        private static DataTable BuildTable(DataSetDateTime mode, DateTime value)
        {
            var table = new DataTable("t");
            var column = new DataColumn("d", typeof(DateTime)) { DateTimeMode = mode };
            table.Columns.Add(column);
            table.Rows.Add(value);
            return table;
        }

        private static string WriteXml(DataTable table)
        {
            using var dataSet = new DataSet("s");
            dataSet.Tables.Add(table);
            using var writer = new StringWriter(s_inv);
            dataSet.WriteXml(writer, XmlWriteMode.WriteSchema);
            return writer.ToString();
        }

        private static string ExtractXmlValue(string xml)
        {
            var match = Regex.Match(xml, "<d>([^<]+)</d>", RegexOptions.None, TimeSpan.FromSeconds(1));
            Assert.True(match.Success, "The XML does not contain the expected <d> element value.");
            return match.Groups[1].Value;
        }

        private static DateTime ReadXmlValue(string mode, string wireValue)
        {
            using var dataSet = new DataSet();
            dataSet.ReadXml(new StringReader(string.Format(s_inv, XmlTemplate, mode, wireValue)), XmlReadMode.ReadSchema);
            return (DateTime)dataSet.Tables[0].Rows[0]["d"];
        }

        #region DataColumn normalization of Kind

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [DisplayName("DataColumn with DateTimeMode=Unspecified normalizes any Kind to Unspecified without changing the value")]
        public void DataColumn_UnspecifiedMode_NormalizesAnyKindWithoutShifting(DateTimeKind kind)
        {
            var table = BuildTable(DataSetDateTime.Unspecified, DateTime.SpecifyKind(s_sample, kind));

            var stored = (DateTime)table.Rows[0]["d"];

            Assert.Equal(DateTimeKind.Unspecified, stored.Kind);
            Assert.Equal(s_sample.TimeOfDay, stored.TimeOfDay);
            Assert.Equal(s_sample.Date, stored.Date);
        }

        [Fact]
        [DisplayName("AddColumn creates DateTime and Date columns with DateTimeMode=Unspecified")]
        public void AddColumn_DateTimeColumns_UseUnspecifiedDateTimeMode()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.AddColumn("order_date", FieldDbType.Date);

            Assert.Equal(DataSetDateTime.Unspecified, table.Columns["created_at"]!.DateTimeMode);
            Assert.Equal(DataSetDateTime.Unspecified, table.Columns["order_date"]!.DateTimeMode);
        }

        #endregion

        #region Round-trips in every format must not shift values

        [Fact]
        [DisplayName("A DataTable restored from MessagePack has DateTimeMode=Unspecified on its DateTime column")]
        public void MessagePack_RebuiltTable_UsesUnspecifiedDateTimeMode()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.Rows.Add(s_sample);

            var restored = MessagePackCodec.Deserialize<DataTable>(MessagePackCodec.Serialize(table));

            Assert.NotNull(restored);
            Assert.Equal(DataSetDateTime.Unspecified, restored.Columns["created_at"]!.DateTimeMode);
        }

        [Fact]
        [DisplayName("A DataTable restored from JSON has DateTimeMode=Unspecified on its DateTime column")]
        public void Json_RebuiltTable_UsesUnspecifiedDateTimeMode()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.Rows.Add(s_sample);

            var restored = JsonCodec.Deserialize<DataTable>(JsonCodec.Serialize(table));

            Assert.NotNull(restored);
            Assert.Equal(DataSetDateTime.Unspecified, restored.Columns["created_at"]!.DateTimeMode);
        }

        [Fact]
        [DisplayName("NormalizeDateTimeMode converts the ADO.NET default UnspecifiedLocal to Unspecified without changing the value")]
        public void NormalizeDateTimeMode_ConvertsAdoNetDefaultWithoutShifting()
        {
            // A table shaped the way DbDataAdapter.Fill / DataTable.Load leave it.
            var table = new DataTable("t");
            table.Columns.Add(new DataColumn("d", typeof(DateTime)));
            table.Rows.Add(s_sample);
            Assert.Equal(DataSetDateTime.UnspecifiedLocal, table.Columns["d"]!.DateTimeMode);

            table.NormalizeDateTimeMode();

            Assert.Equal(DataSetDateTime.Unspecified, table.Columns["d"]!.DateTimeMode);
            Assert.Equal(s_sample, (DateTime)table.Rows[0]["d"]);
            Assert.Equal("2026-01-01T09:00:00", ExtractXmlValue(WriteXml(table)));
        }


        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [DisplayName("MessagePack round-trip does not change the time value of a DataTable cell")]
        public void MessagePack_DataTableRoundTrip_PreservesWallClock(DateTimeKind kind)
        {
            var table = BuildTable(DataSetDateTime.Unspecified, DateTime.SpecifyKind(s_sample, kind));

            var restored = MessagePackCodec.Deserialize<DataTable>(MessagePackCodec.Serialize(table));

            Assert.NotNull(restored);
            var value = (DateTime)restored.Rows[0]["d"];
            Assert.Equal(s_sample, value);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [DisplayName("JSON round-trip does not change the time value of a DataTable cell")]
        public void Json_DataTableRoundTrip_PreservesWallClock(DateTimeKind kind)
        {
            var table = BuildTable(DataSetDateTime.Unspecified, DateTime.SpecifyKind(s_sample, kind));

            var restored = JsonCodec.Deserialize<DataTable>(JsonCodec.Serialize(table));

            Assert.NotNull(restored);
            var value = (DateTime)restored.Rows[0]["d"];
            Assert.Equal(s_sample, value);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [DisplayName("XML round-trip with DateTimeMode=Unspecified does not change the time value")]
        public void Xml_DataTableRoundTrip_PreservesWallClock(DateTimeKind kind)
        {
            var xml = WriteXml(BuildTable(DataSetDateTime.Unspecified, DateTime.SpecifyKind(s_sample, kind)));

            using var restored = new DataSet();
            restored.ReadXml(new StringReader(xml), XmlReadMode.ReadSchema);

            var value = (DateTime)restored.Tables[0].Rows[0]["d"];
            Assert.Equal(s_sample, value);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [DisplayName("MessagePack and JSON wires produce the same result for the same cell")]
        public void MessagePackAndJson_AgreeOnCellValue(DateTimeKind kind)
        {
            var value = DateTime.SpecifyKind(s_sample, kind);

            var viaMessagePack = MessagePackCodec.Deserialize<DataTable>(
                MessagePackCodec.Serialize(BuildTable(DataSetDateTime.Unspecified, value)));
            var viaJson = JsonCodec.Deserialize<DataTable>(
                JsonCodec.Serialize(BuildTable(DataSetDateTime.Unspecified, value)));

            Assert.NotNull(viaMessagePack);
            Assert.NotNull(viaJson);
            Assert.Equal((DateTime)viaMessagePack.Rows[0]["d"], (DateTime)viaJson.Rows[0]["d"]);
        }

        #endregion

        #region XML write side: DateTimeMode decides whether an offset is written

        [Fact]
        [DisplayName("XML with DateTimeMode=Unspecified writes the value without a time zone offset")]
        public void Xml_UnspecifiedMode_WritesNoOffset()
        {
            var wire = ExtractXmlValue(WriteXml(BuildTable(DataSetDateTime.Unspecified, s_sample)));

            Assert.Equal("2026-01-01T09:00:00", wire);
        }

        [Fact]
        [DisplayName("XML with the .NET default UnspecifiedLocal writes a time zone offset, which is why Unspecified must be set")]
        public void Xml_UnspecifiedLocalMode_WritesOffset()
        {
            var wire = ExtractXmlValue(WriteXml(BuildTable(DataSetDateTime.UnspecifiedLocal, s_sample)));

            var expectedOffset = TimeZoneInfo.Local.GetUtcOffset(s_sample);
            var expected = new DateTimeOffset(s_sample, expectedOffset).ToString("yyyy-MM-ddTHH:mm:sszzz", s_inv);
            Assert.Equal(expected, wire);
            Assert.NotEqual("2026-01-01T09:00:00", wire);
        }

        #endregion

        #region XML read side: an offset on the wire is always applied

        [Theory]
        [InlineData("Unspecified")]
        [InlineData("UnspecifiedLocal")]
        [DisplayName("XML read still applies an offset present on the wire because Unspecified does not mean ignoring it")]
        public void Xml_Read_AppliesOffsetPresentOnWire(string mode)
        {
            // A payload produced by a +08:00 writer. Both Unspecified and UnspecifiedLocal convert it
            // to the reader's local time and then drop the kind, so a reader west of the writer can
            // land on the previous calendar day.
            var value = ReadXmlValue(mode, "2026-01-01T09:00:00+08:00");

            var expected = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(8)).ToLocalTime().DateTime;
            Assert.Equal(expected, value);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }

        [Theory]
        [InlineData("Unspecified")]
        [InlineData("UnspecifiedLocal")]
        [DisplayName("XML read does not shift a naive value, and both Unspecified modes behave the same")]
        public void Xml_Read_NaiveValue_IsNotShifted(string mode)
        {
            var value = ReadXmlValue(mode, "2026-01-01T09:00:00");

            Assert.Equal(s_sample, value);
            Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        }

        #endregion

        #region Strongly typed properties: how Kind survives on the two wires

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [DisplayName("MessagePack typeless keeps the value of a non-Local DateTime and always marks it Utc")]
        public void MessagePack_TypelessNonLocalKind_PreservesWallClockAsUtc(DateTimeKind kind)
        {
            var original = new ParameterCollection { { "d", DateTime.SpecifyKind(s_sample, kind) } };

            var restored = MessagePackCodec.Deserialize<ParameterCollection>(MessagePackCodec.Serialize(original));

            Assert.NotNull(restored);
            var value = Assert.IsType<DateTime>(restored["d"].Value);
            Assert.Equal(s_sample.TimeOfDay, value.TimeOfDay);
            Assert.Equal(s_sample.Date, value.Date);
            Assert.Equal(DateTimeKind.Utc, value.Kind);
        }

        [Fact]
        [DisplayName("MessagePack typeless shifts a DateTime with Kind=Local to UTC")]
        public void MessagePack_TypelessLocalKind_ShiftsWallClockToUtc()
        {
            // The msgpack timestamp extension stores an absolute instant, so the formatter converts a
            // `Local` value to UTC on write. The instant survives but the wall-clock reading does not —
            // a receiver that treats the cell as a wall-clock value silently reads a different time.
            // This is the DTO-side counterpart to the JSON offset hazard, and the second reason D6
            // forbids `Local` on the wire.
            var original = new ParameterCollection { { "d", DateTime.SpecifyKind(s_sample, DateTimeKind.Local) } };

            var restored = MessagePackCodec.Deserialize<ParameterCollection>(MessagePackCodec.Serialize(original));

            Assert.NotNull(restored);
            var value = Assert.IsType<DateTime>(restored["d"].Value);
            Assert.Equal(DateTime.SpecifyKind(s_sample, DateTimeKind.Local).ToUniversalTime(), value);
            Assert.Equal(DateTimeKind.Utc, value.Kind);
        }

        [Fact]
        [DisplayName("For the same Local value the DataTable path keeps the wall-clock time while the typeless DTO path shifts it to UTC")]
        public void MessagePack_DataTableAndTypelessPaths_DisagreeOnLocalKind()
        {
            // Not a bug to fix but an asymmetry to remember: `DataColumn` normalises the kind away
            // before the formatter ever sees it, so the DataSet path cannot shift. A bare DTO
            // property has no such buffer. Guarding only one of the two paths leaves the other open.
            var local = DateTime.SpecifyKind(s_sample, DateTimeKind.Local);

            var viaTable = MessagePackCodec.Deserialize<DataTable>(
                MessagePackCodec.Serialize(BuildTable(DataSetDateTime.Unspecified, local)));
            var viaTypeless = MessagePackCodec.Deserialize<ParameterCollection>(
                MessagePackCodec.Serialize(new ParameterCollection { { "d", local } }));

            Assert.NotNull(viaTable);
            Assert.NotNull(viaTypeless);
            Assert.Equal(s_sample, (DateTime)viaTable.Rows[0]["d"]);
            Assert.Equal(local.ToUniversalTime(), (DateTime)viaTypeless["d"].Value!);
        }

        [Fact]
        [DisplayName("JSON writes a time zone offset for a DateTime with Kind=Local, the basis for D6 forbidding Local on the wire")]
        public void Json_LocalKind_WritesOffsetOnWire()
        {
            var json = JsonCodec.Serialize(DateTime.SpecifyKind(s_sample, DateTimeKind.Local));

            var expectedOffset = TimeZoneInfo.Local.GetUtcOffset(s_sample);
            Assert.Contains(new DateTimeOffset(s_sample, expectedOffset).ToString("zzz", s_inv), json, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [DisplayName("JSON keeps the value of Unspecified and Utc DateTimes")]
        public void Json_NonLocalKinds_PreserveWallClock(DateTimeKind kind)
        {
            var original = DateTime.SpecifyKind(s_sample, kind);

            var restored = JsonCodec.Deserialize<DateTime>(JsonCodec.Serialize(original));

            Assert.Equal(s_sample.TimeOfDay, restored.TimeOfDay);
            Assert.Equal(s_sample.Date, restored.Date);
        }

        #endregion
    }
}
