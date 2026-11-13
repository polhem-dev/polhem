using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Business.AuditLog;
using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// The schema-bound payload read back by <see cref="ChangeDiffGramReader"/>: how each column type is rendered as
    /// text, a table without a row key, and a wrapper that holds nothing readable.
    /// </summary>
    public class ChangeDiffGramReaderValueTests
    {
        private const string TableName = "ft_values";

        [Fact]
        [DisplayName("An inserted row of a table without sys_rowid reads back with a null row key")]
        public void Read_TableWithoutRowId_RowKeyNull()
        {
            var changes = ReadInserted(table => table.Columns.Add("memo", typeof(string)), "hello");

            var change = Assert.Single(changes);
            Assert.Null(change.RowKey);
            Assert.Equal(ChangeKind.Insert, change.RowState);
            Assert.Equal("hello", change.NewValue);
        }

        [Theory]
        [InlineData(typeof(long), 1234567890123L, "1234567890123")]
        [InlineData(typeof(short), (short)-12, "-12")]
        [InlineData(typeof(byte), (byte)200, "200")]
        [InlineData(typeof(double), 1.5d, "1.5")]
        [InlineData(typeof(float), 2.25f, "2.25")]
        [InlineData(typeof(bool), true, "true")]
        [InlineData(typeof(char), 'x', "x")]
        [DisplayName("A restored numeric, boolean or other value renders in its invariant XML spelling")]
        public void Read_ScalarColumn_RendersInvariantText(Type type, object value, string expected)
        {
            var change = Assert.Single(ReadInserted(table => table.Columns.Add("v", type), value));

            Assert.Equal(expected, change.NewValue);
        }

        [Fact]
        [DisplayName("A restored Guid renders in its XML spelling")]
        public void Read_GuidColumn_RendersXmlText()
        {
            var value = Guid.Parse("11111111-2222-3333-4444-555555555555");

            var change = Assert.Single(ReadInserted(table => table.Columns.Add("v", typeof(Guid)), value));

            Assert.Equal("11111111-2222-3333-4444-555555555555", change.NewValue);
        }

        [Fact]
        [DisplayName("A restored TimeSpan renders as an XML duration")]
        public void Read_TimeSpanColumn_RendersDuration()
        {
            var change = Assert.Single(ReadInserted(table => table.Columns.Add("v", typeof(TimeSpan)), TimeSpan.FromMinutes(90)));

            Assert.Equal("PT1H30M", change.NewValue);
        }

        [Fact]
        [DisplayName("A restored byte array renders as base64")]
        public void Read_ByteArrayColumn_RendersBase64()
        {
            var change = Assert.Single(ReadInserted(table => table.Columns.Add("v", typeof(byte[])), new byte[] { 1, 2, 3 }));

            Assert.Equal(Convert.ToBase64String([1, 2, 3]), change.NewValue);
        }

        [Fact]
        [DisplayName("A restored DateTimeOffset renders in its XML spelling with its offset")]
        public void Read_DateTimeOffsetColumn_RendersWithOffset()
        {
            var value = new DateTimeOffset(2027, 3, 1, 8, 30, 0, TimeSpan.FromHours(8));

            var change = Assert.Single(ReadInserted(table => table.Columns.Add("v", typeof(DateTimeOffset)), value));

            Assert.Equal(global::System.Xml.XmlConvert.ToString(value), change.NewValue);
        }

        [Fact]
        [DisplayName("A null value in an inserted row reads back as a null new value")]
        public void Read_NullValue_ReadsBackNull()
        {
            var change = Assert.Single(ReadInserted(table => table.Columns.Add("memo", typeof(string)), DBNull.Value));

            Assert.Null(change.NewValue);
        }

        [Fact]
        [DisplayName("A schema-bound wrapper with nothing inside reads back as no field detail and no data set")]
        public void ReadDetail_EmptyWrapper_ReturnsEmpty()
        {
            var (fields, dataSet) = ChangeDiffGramReader.ReadDetail($"<{AuditDiffGram.RootElementName}></{AuditDiffGram.RootElementName}>");

            Assert.Empty(fields);
            Assert.Null(dataSet);
        }

        [Fact]
        [DisplayName("A schema-bound wrapper around broken XML reads back as no field detail and no data set")]
        public void ReadDetail_DamagedSchemaBoundPayload_ReturnsEmpty()
        {
            var xml = $"<{AuditDiffGram.RootElementName}><a></b></{AuditDiffGram.RootElementName}>";

            var (fields, dataSet) = ChangeDiffGramReader.ReadDetail(xml);

            Assert.Empty(fields);
            Assert.Null(dataSet);
        }

        [Fact]
        [DisplayName("A deleted-record wrapper with nothing inside reads back as no field detail")]
        public void Read_EmptyDeletedRecordWrapper_ReturnsEmpty()
        {
            Assert.Empty(ChangeDiffGramReader.Read(
                $"<{AuditDiffGram.DeletedRecordRootElementName}></{AuditDiffGram.DeletedRecordRootElementName}>"));
        }

        private static List<Polhem.Api.Contracts.AuditLog.RecordFieldChange> ReadInserted(Action<DataTable> addColumn, object value)
        {
            var table = new DataTable(TableName) { Locale = CultureInfo.InvariantCulture };
            addColumn(table);
            using var dataSet = new DataSet("form") { Locale = CultureInfo.InvariantCulture };
            dataSet.Tables.Add(table);
            table.Rows.Add(value);
            using var changes = dataSet.GetChanges()!;
            return ChangeDiffGramReader.Read(AuditDiffGram.Serialize(changes));
        }
    }
}
