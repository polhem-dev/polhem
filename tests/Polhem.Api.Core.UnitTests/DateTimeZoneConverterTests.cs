using System.ComponentModel;
using System.Data;
using System.Xml;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core.Data;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// <see cref="DateTimeZoneConverter"/> tests: instant columns shift from UTC to the user's time zone, calendar-day
    /// columns stay as they are, row states and both versions (Current / Original) are kept, the source is not
    /// modified in place, and filter values are converted.
    /// </summary>
    /// <remarks>
    /// Expected values are always derived from <see cref="TimeZoneInfo"/> at run time, never hard-coded offsets, so the
    /// tests hold both on a development machine (Asia/Taipei) and in CI (UTC). The design is in
    /// maintainers/adr/adr-032-datetime-timezone.md (D4).
    /// </remarks>
    public class DateTimeZoneConverterTests
    {
        private const string Taipei = "Asia/Taipei";
        private static readonly DateTime s_utc9Am = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        private static DateTime ExpectedInTaipei(DateTime utcValue)
            => DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(utcValue, DateTimeKind.Unspecified),
                    TimeZoneInfo.FindSystemTimeZoneById(Taipei)),
                DateTimeKind.Unspecified);

        private static DataTable BuildTable()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.AddColumn("order_date", FieldDbType.Date);
            table.AddColumn("remark", FieldDbType.String);
            return table;
        }

        private static DataTable BuildTableWithRow()
        {
            var table = BuildTable();
            table.Rows.Add(s_utc9Am, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), "a");
            table.AcceptChanges();
            return table;
        }

        [Fact]
        [DisplayName("A DateTime column shifts from UTC to the user's time zone")]
        public void UtcToUser_ShiftsInstantColumn()
        {
            var converted = DateTimeZoneConverter.UtcToUser(BuildTableWithRow(), Taipei);

            Assert.NotNull(converted);
            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)converted.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("A Date column is never converted (a calendar day has no instant to re-express)")]
        public void UtcToUser_LeavesCalendarDayColumnUntouched()
        {
            var converted = DateTimeZoneConverter.UtcToUser(BuildTableWithRow(), Taipei);

            Assert.NotNull(converted);
            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), (DateTime)converted.Rows[0]["order_date"]);
        }

        [Fact]
        [DisplayName("A Date column read back from DataSet XML is not converted either")]
        public void UtcToUser_XmlRestoredCalendarDayColumn_LeftUntouched()
        {
            using var source = new DataSet("ds");
            source.Tables.Add(BuildTableWithRow());
            using var writer = new StringWriter();
            source.WriteXml(writer, XmlWriteMode.WriteSchema);
            using var restored = new DataSet();
            using var stringReader = new StringReader(writer.ToString());
            using var reader = XmlReader.Create(stringReader,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            restored.ReadXml(reader, XmlReadMode.ReadSchema);

            var converted = DateTimeZoneConverter.UtcToUser(restored.Tables["orders"]!, Taipei);

            // The marker read back is a string. If it were not parsed, the Date column would be treated as an instant and shifted to Taipei time.
            Assert.NotNull(converted);
            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), (DateTime)converted.Rows[0]["order_date"]);
        }

        [Fact]
        [DisplayName("A blank time zone is a no-op and returns the same reference")]
        public void BlankTimeZone_IsNoOp()
        {
            var source = BuildTableWithRow();

            var converted = DateTimeZoneConverter.UtcToUser(source, string.Empty);

            Assert.Same(source, converted);
        }

        [Fact]
        [DisplayName("Conversion does not modify the source in place (in-process, the source is the caller's own object)")]
        public void Convert_DoesNotMutateSource()
        {
            var source = BuildTableWithRow();

            DateTimeZoneConverter.UtcToUser(source, Taipei);

            Assert.Equal(s_utc9Am, (DateTime)source.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("Both the Current and the Original version of a Modified row are converted")]
        public void Convert_ModifiedRow_ConvertsBothVersions()
        {
            // Converting only Current would leave the two versions in different time zones, which breaks the server's concurrency check and the audit DiffGram.
            var table = BuildTableWithRow();
            var newUtc = new DateTime(2026, 1, 2, 15, 0, 0, DateTimeKind.Unspecified);
            table.Rows[0]["created_at"] = newUtc;
            Assert.Equal(DataRowState.Modified, table.Rows[0].RowState);

            var converted = DateTimeZoneConverter.UtcToUser(table, Taipei);

            Assert.NotNull(converted);
            var row = converted.Rows[0];
            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal(ExpectedInTaipei(newUtc), (DateTime)row["created_at", DataRowVersion.Current]);
            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)row["created_at", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("When a Modified row changed only a non-instant column, that edit is kept")]
        public void Convert_ModifiedRowWithNonInstantEdit_KeepsTheEdit()
        {
            // Rewriting Original requires `RejectChanges` first, which reverts the whole row, not only the instant columns.
            var table = BuildTableWithRow();
            table.Rows[0]["remark"] = "edited";

            var converted = DateTimeZoneConverter.UtcToUser(table, Taipei);

            Assert.NotNull(converted);
            var row = converted.Rows[0];
            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal("edited", row["remark", DataRowVersion.Current]);
            Assert.Equal("a", row["remark", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("An Added row stays Added and its values are converted")]
        public void Convert_AddedRow_KeepsState()
        {
            var table = BuildTable();
            table.Rows.Add(s_utc9Am, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), "a");
            Assert.Equal(DataRowState.Added, table.Rows[0].RowState);

            var converted = DateTimeZoneConverter.UtcToUser(table, Taipei);

            Assert.NotNull(converted);
            Assert.Equal(DataRowState.Added, converted.Rows[0].RowState);
            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)converted.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("A Deleted row stays Deleted and its Original values are converted too (the audit reads them)")]
        public void Convert_DeletedRow_KeepsStateAndConvertsOriginal()
        {
            var table = BuildTableWithRow();
            table.Rows[0].Delete();

            var converted = DateTimeZoneConverter.UtcToUser(table, Taipei);

            Assert.NotNull(converted);
            var row = converted.Rows[0];
            Assert.Equal(DataRowState.Deleted, row.RowState);
            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)row["created_at", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("An Unchanged row stays Unchanged and does not become Modified through conversion")]
        public void Convert_UnchangedRow_StaysUnchanged()
        {
            var converted = DateTimeZoneConverter.UtcToUser(BuildTableWithRow(), Taipei);

            Assert.NotNull(converted);
            Assert.Equal(DataRowState.Unchanged, converted.Rows[0].RowState);
        }

        [Fact]
        [DisplayName("Every table in a DataSet is converted")]
        public void Convert_DataSet_ConvertsEveryTable()
        {
            using var dataSet = new DataSet("s");
            dataSet.Tables.Add(BuildTableWithRow());
            var detail = BuildTableWithRow();
            detail.TableName = "order_items";
            dataSet.Tables.Add(detail);

            var converted = DateTimeZoneConverter.UtcToUser(dataSet, Taipei);

            Assert.NotNull(converted);
            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)converted.Tables["orders"]!.Rows[0]["created_at"]);
            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)converted.Tables["order_items"]!.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("DBNull cells are left alone")]
        public void Convert_NullCell_IsLeftAlone()
        {
            var table = BuildTable();
            table.Columns["created_at"]!.AllowDBNull = true;
            table.Rows.Add(DBNull.Value, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), "a");
            table.AcceptChanges();

            var converted = DateTimeZoneConverter.UtcToUser(table, Taipei);

            Assert.NotNull(converted);
            Assert.Equal(DBNull.Value, converted.Rows[0]["created_at"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("Filter values: a DateTime is converted and a DateOnly is not")]
        public void ConvertFilterValue_RespectsValueType(bool toUtc)
        {
            var day = new DateOnly(2026, 1, 1);

            var shifted = DateTimeZoneConverter.ConvertFilterValue(s_utc9Am, Taipei, toUtc);
            var untouched = DateTimeZoneConverter.ConvertFilterValue(day, Taipei, toUtc);

            Assert.NotEqual(s_utc9Am, shifted);
            Assert.Equal(day, untouched);
        }

        [Fact]
        [DisplayName("Filter values: a non-temporal value is returned as is")]
        public void ConvertFilterValue_NonTemporalValue_IsReturnedAsIs()
        {
            Assert.Equal("open", DateTimeZoneConverter.ConvertFilterValue("open", Taipei, toUtc: true));
            Assert.Null(DateTimeZoneConverter.ConvertFilterValue(null, Taipei, toUtc: true));
        }

        [Fact]
        [DisplayName("With UTC as the user's time zone, conversion is the identity (values unchanged to the tick)")]
        public void UtcUser_ConversionIsIdentity()
        {
            // D10 defines "zero cost" as zero complexity, not zero run time: the pipeline still runs, it just does not
            // change values. This test pins that "does not change", so nobody later adds value-altering work to the path.
            var converted = DateTimeZoneConverter.UtcToUser(BuildTableWithRow(), "UTC");
            var filterValue = (DateTime)DateTimeZoneConverter.ConvertFilterValue(s_utc9Am, "UTC", toUtc: true)!;

            Assert.NotNull(converted);
            Assert.Equal(s_utc9Am.Ticks, ((DateTime)converted.Rows[0]["created_at"]).Ticks);
            Assert.Equal(s_utc9Am.Ticks, filterValue.Ticks);
        }

        [Fact]
        [DisplayName("An unresolvable time zone throws instead of silently skipping the conversion")]
        public void Convert_UnresolvableZone_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => DateTimeZoneConverter.UtcToUser(BuildTableWithRow(), "Not/AZone"));

            Assert.Contains("Not/AZone", exception.Message, StringComparison.Ordinal);
        }
    }
}
