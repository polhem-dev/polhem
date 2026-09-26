using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Business.AuditLog;
using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// The DataSet <c>ChangeDiffGramReader.ReadDetail</c> restores from each kind of payload: an insert, an update, a deleted record
    /// and an old delete record each carry the right row state; the old schemaless format, a delete marker, blank input and corrupt input all give null.
    /// </summary>
    public class ChangeDetailDataSetTests
    {
        private const string TableName = "ft_order";
        private const string RowKey = "row-1";

        private static DataSet NewDataSet()
        {
            var table = new DataTable(TableName) { Locale = CultureInfo.InvariantCulture };
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            var dataSet = new DataSet(TableName) { Locale = CultureInfo.InvariantCulture };
            dataSet.Tables.Add(table);
            return dataSet;
        }

        private static string SerializeChanges(DataSet dataSet)
        {
            using var changes = dataSet.GetChanges()!;
            return AuditDiffGram.Serialize(changes);
        }

        private static DataRow SingleRow(DataSet? dataSet)
        {
            Assert.NotNull(dataSet);
            return Assert.Single(dataSet!.Tables[TableName]!.Rows.Cast<DataRow>());
        }

        [Fact]
        [DisplayName("An insert change set is restored as an Added row")]
        public void ReadDetail_Insert_ReturnsAddedRow()
        {
            using var source = NewDataSet();
            source.Tables[TableName]!.Rows.Add(RowKey, "新單");

            var (fields, dataSet) = ChangeDiffGramReader.ReadDetail(SerializeChanges(source));
            using (dataSet)
            {
                var row = SingleRow(dataSet);
                Assert.Equal(DataRowState.Added, row.RowState);
                Assert.Equal("新單", row["sys_name"]);
                Assert.Contains(fields, f => f.RowState == ChangeKind.Insert && f.FieldName == "sys_name");
            }
        }

        [Fact]
        [DisplayName("An update change set is restored as a Modified row with the original value")]
        public void ReadDetail_Update_ReturnsModifiedRowWithOriginal()
        {
            using var source = NewDataSet();
            var sourceRow = source.Tables[TableName]!.Rows.Add(RowKey, "原值");
            source.AcceptChanges();
            sourceRow["sys_name"] = "新值";

            var (_, dataSet) = ChangeDiffGramReader.ReadDetail(SerializeChanges(source));
            using (dataSet)
            {
                var row = SingleRow(dataSet);
                Assert.Equal(DataRowState.Modified, row.RowState);
                Assert.Equal("原值", row["sys_name", DataRowVersion.Original]);
                Assert.Equal("新值", row["sys_name", DataRowVersion.Current]);
            }
        }

        [Fact]
        [DisplayName("A deleted record is restored as an Unchanged row, the complete record before deletion")]
        public void ReadDetail_DeletedRecord_ReturnsUnchangedRecord()
        {
            using var source = NewDataSet();
            source.Tables[TableName]!.Rows.Add(RowKey, "待刪原單");
            source.AcceptChanges();

            var (fields, dataSet) = ChangeDiffGramReader.ReadDetail(AuditDiffGram.SerializeDeletedRecord(source));
            using (dataSet)
            {
                var row = SingleRow(dataSet);
                Assert.Equal(DataRowState.Unchanged, row.RowState);
                Assert.Equal("待刪原單", row["sys_name"]);
                var field = Assert.Single(fields);
                Assert.Equal(ChangeKind.Delete, field.RowState);
            }
        }

        [Fact]
        [DisplayName("An old delete record (a change set with rows marked Deleted) is restored as is, as a Deleted row")]
        public void ReadDetail_LegacyDeletedRows_ReturnsDeletedRow()
        {
            using var source = NewDataSet();
            var sourceRow = source.Tables[TableName]!.Rows.Add(RowKey, "舊刪除");
            source.AcceptChanges();
            sourceRow.Delete();

            var (_, dataSet) = ChangeDiffGramReader.ReadDetail(SerializeChanges(source));
            using (dataSet)
            {
                var row = SingleRow(dataSet);
                Assert.Equal(DataRowState.Deleted, row.RowState);
                Assert.Equal("舊刪除", row["sys_name", DataRowVersion.Original]);
            }
        }

        [Fact]
        [DisplayName("An old schemaless DiffGram yields the field list but no DataSet")]
        public void ReadDetail_SchemalessDiffGram_ReturnsFieldsWithoutDataSet()
        {
            using var source = NewDataSet();
            var sourceRow = source.Tables[TableName]!.Rows.Add(RowKey, "原值");
            source.AcceptChanges();
            sourceRow["sys_name"] = "新值";
            using var changes = source.GetChanges()!;
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            changes.WriteXml(writer, XmlWriteMode.DiffGram);

            var (fields, dataSet) = ChangeDiffGramReader.ReadDetail(writer.ToString());

            Assert.Null(dataSet);
            Assert.Single(fields);
        }

        [Theory]
        [InlineData("")]
        [InlineData("<DeletedRow table=\"ft_order\" sys_rowid=\"abc\" />")]
        [InlineData("<AuditChanges />")]
        [InlineData("<AuditChanges><broken>")]
        [DisplayName("A delete marker, blank input, an empty wrapper and a corrupt payload have no DataSet and an empty field list")]
        public void ReadDetail_NoRestorablePayload_ReturnsNothing(string payload)
        {
            var (fields, dataSet) = ChangeDiffGramReader.ReadDetail(payload);

            Assert.Null(dataSet);
            Assert.Empty(fields);
        }
    }
}
