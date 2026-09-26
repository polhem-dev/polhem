using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Api.Contracts.AuditLog;
using Polhem.Business.AuditLog;
using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// Covers the changes_xml format with an embedded XSD: a payload is actually written with <c>AuditDiffGram.Serialize</c>
    /// and read back through <c>ChangeDiffGramReader.Read</c>, verifying the Added / Modified / Deleted row states, that only changed columns are emitted, the before image,
    /// the string form of values, and output identical to the old (schemaless) format.
    /// </summary>
    /// <remarks>
    /// <c>ChangeDiffGramReaderCoverageTests</c> covers the old format (<c>SchemalessDiffGramReader</c>),
    /// those cases are regression guards and must not be merged into this file.
    /// </remarks>
    public class SchemaBoundChangeReaderTests
    {
        private const string TableName = "ft_order";
        private static readonly Guid s_rowId = Guid.Parse("2a640240-1111-2222-3333-444455556666");
        private static readonly DateTime s_orderDate = new(2027, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);

        /// <summary>
        /// Builds a table with representative column types (one each of string, decimal, DateTime, bool and int) covering every branch of value stringification.
        /// </summary>
        private static DataSet NewDataSet()
        {
            var table = new DataTable(TableName) { Locale = CultureInfo.InvariantCulture };
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            var orderDate = table.Columns.Add("order_date", typeof(DateTime));
            // IMPORTANT: mirror DataTableExtensions.AddColumn, which every framework-built table goes
            // through. A DataTable defaults DateTime columns to UnspecifiedLocal, and the DiffGram then
            // writes a timezone offset (2027-03-01T00:00:00+08:00) that the restored value no longer
            // carries — the two payload shapes would disagree on that column for a reason production
            // never hits. Leaving this line out makes the parity test fail on the reader's behalf.
            orderDate.DateTimeMode = DataSetDateTime.Unspecified;
            table.Columns.Add("total_amount", typeof(decimal));
            table.Columns.Add("is_urgent", typeof(bool));
            table.Columns.Add("quantity", typeof(int));

            var dataSet = new DataSet("form") { Locale = CultureInfo.InvariantCulture };
            dataSet.Tables.Add(table);
            return dataSet;
        }

        private static DataRow SeedRow(DataSet dataSet)
        {
            var table = dataSet.Tables[TableName]!;
            var row = table.Rows.Add(s_rowId, "SO-0001", s_orderDate, 100.50m, false, 3);
            dataSet.AcceptChanges();
            return row;
        }

        /// <summary>Writes the current format (wrapper element + embedded XSD + DiffGram).</summary>
        private static string Serialize(DataSet dataSet)
        {
            using var changes = dataSet.GetChanges()!;
            return AuditDiffGram.Serialize(changes);
        }

        /// <summary>Writes the old format from before the change (a bare DiffGram without schema) for the consistency comparison.</summary>
        private static string SerializeSchemaless(DataSet dataSet)
        {
            using var changes = dataSet.GetChanges()!;
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            changes.WriteXml(writer, XmlWriteMode.DiffGram);
            return writer.ToString();
        }

        // ---- Payload shape ----

        [Fact]
        [DisplayName("The payload has AuditChanges as its root, contains the XSD and the DiffGram, and keeps its indentation")]
        public void Serialize_EmitsWrappedSchemaAndDiffGram()
        {
            var dataSet = NewDataSet();
            var row = SeedRow(dataSet);
            row["sys_id"] = "SO-0002";

            var xml = Serialize(dataSet);

            Assert.StartsWith("<AuditChanges>", xml, StringComparison.Ordinal);
            Assert.Contains(":schema", xml, StringComparison.Ordinal);
            Assert.Contains(":diffgram", xml, StringComparison.Ordinal);
            Assert.Contains("<diffgr:before", xml, StringComparison.Ordinal);
            // The indentation is deliberate (it keeps audit rows readable for people). It is also a regression guard for the reader:
            // a minified payload would hide that sub-tree reader bug.
            Assert.Contains("\n", xml, StringComparison.Ordinal);
        }

        // ---- Row states ----

        [Fact]
        [DisplayName("A modified row emits only the columns that actually changed, with old and new values")]
        public void Read_ModifiedRow_EmitsOnlyChangedColumns()
        {
            var dataSet = NewDataSet();
            var row = SeedRow(dataSet);
            row["sys_id"] = "SO-0002";
            row["total_amount"] = 250.75m;

            var result = ChangeDiffGramReader.Read(Serialize(dataSet));

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.Equal(ChangeKind.Update, c.RowState));
            Assert.All(result, c => Assert.Equal(TableName, c.TableName));
            Assert.All(result, c => Assert.Equal(s_rowId.ToString(), c.RowKey));
            Assert.DoesNotContain(result, c => c.FieldName == "sys_rowid");

            var id = Assert.Single(result, c => c.FieldName == "sys_id");
            Assert.Equal("SO-0001", id.OldValue);
            Assert.Equal("SO-0002", id.NewValue);
            var amount = Assert.Single(result, c => c.FieldName == "total_amount");
            Assert.Equal("100.50", amount.OldValue);
            Assert.Equal("250.75", amount.NewValue);
        }

        [Fact]
        [DisplayName("An inserted row emits an Insert for every non-row-key column, with a null OldValue")]
        public void Read_InsertedRow_EmitsInsertPerColumn()
        {
            var dataSet = NewDataSet();
            dataSet.Tables[TableName]!.Rows.Add(s_rowId, "SO-0009", s_orderDate, 88m, true, 7);

            var result = ChangeDiffGramReader.Read(Serialize(dataSet));

            Assert.All(result, c => Assert.Equal(ChangeKind.Insert, c.RowState));
            Assert.All(result, c => Assert.Null(c.OldValue));
            Assert.All(result, c => Assert.Equal(s_rowId.ToString(), c.RowKey));
            Assert.DoesNotContain(result, c => c.FieldName == "sys_rowid");
            var id = Assert.Single(result, c => c.FieldName == "sys_id");
            Assert.Equal("SO-0009", id.NewValue);
        }

        [Fact]
        [DisplayName("A deleted row emits the before image, with a null NewValue")]
        public void Read_DeletedRow_EmitsBeforeImage()
        {
            var dataSet = NewDataSet();
            var row = SeedRow(dataSet);
            row.Delete();

            var result = ChangeDiffGramReader.Read(Serialize(dataSet));

            Assert.All(result, c => Assert.Equal(ChangeKind.Delete, c.RowState));
            Assert.All(result, c => Assert.Null(c.NewValue));
            Assert.All(result, c => Assert.Equal(s_rowId.ToString(), c.RowKey));
            Assert.DoesNotContain(result, c => c.FieldName == "sys_rowid");
            var id = Assert.Single(result, c => c.FieldName == "sys_id");
            Assert.Equal("SO-0001", id.OldValue);
        }

        [Fact]
        [DisplayName("Inserts, updates and deletes in the same payload are each restored")]
        public void Read_MixedRowStates_RestoresEach()
        {
            var dataSet = NewDataSet();
            var table = dataSet.Tables[TableName]!;
            var kept = table.Rows.Add(s_rowId, "SO-0001", s_orderDate, 100.50m, false, 3);
            var removed = table.Rows.Add(Guid.NewGuid(), "SO-0002", s_orderDate, 20m, false, 1);
            dataSet.AcceptChanges();

            kept["quantity"] = 9;
            removed.Delete();
            table.Rows.Add(Guid.NewGuid(), "SO-0003", s_orderDate, 30m, true, 2);

            var result = ChangeDiffGramReader.Read(Serialize(dataSet));

            var updated = Assert.Single(result, c => c.RowState == ChangeKind.Update);
            Assert.Equal("quantity", updated.FieldName);
            Assert.Equal("3", updated.OldValue);
            Assert.Equal("9", updated.NewValue);
            Assert.Contains(result, c => c.RowState == ChangeKind.Delete && c.FieldName == "sys_id" && c.OldValue == "SO-0002");
            Assert.Contains(result, c => c.RowState == ChangeKind.Insert && c.FieldName == "sys_id" && c.NewValue == "SO-0003");
        }

        // ---- Value stringification ----

        [Fact]
        [DisplayName("Values are written in the XML lexical form (culture-independent), never with ToString")]
        public void Read_Values_UseXmlLexicalForm()
        {
            var dataSet = NewDataSet();
            var row = SeedRow(dataSet);
            row["order_date"] = new DateTime(2028, 12, 25, 13, 45, 30, DateTimeKind.Unspecified);
            row["is_urgent"] = true;

            var result = ChangeDiffGramReader.Read(Serialize(dataSet));

            // `ToString()` never produces these two formats under any culture, so this assertion is enough to catch culture-dependent code.
            var date = Assert.Single(result, c => c.FieldName == "order_date");
            Assert.Equal("2027-03-01T00:00:00", date.OldValue);
            Assert.Equal("2028-12-25T13:45:30", date.NewValue);
            var urgent = Assert.Single(result, c => c.FieldName == "is_urgent");
            Assert.Equal("false", urgent.OldValue);
            Assert.Equal("true", urgent.NewValue);
        }

        // ---- Consistency with the old format ----

        [Fact]
        [DisplayName("For the same changes, the new and old payloads restore exactly the same column changes")]
        public void Read_NewAndLegacyPayloads_ProduceIdenticalChanges()
        {
            static DataSet Mutate()
            {
                var dataSet = NewDataSet();
                var row = SeedRow(dataSet);
                row["sys_id"] = "SO-0002";
                row["order_date"] = new DateTime(2028, 12, 25, 13, 45, 30, DateTimeKind.Unspecified);
                row["total_amount"] = 250.75m;
                row["is_urgent"] = true;
                row["quantity"] = 9;
                return dataSet;
            }

            var fromNew = ChangeDiffGramReader.Read(Serialize(Mutate()));
            var fromLegacy = ChangeDiffGramReader.Read(SerializeSchemaless(Mutate()));

            static IEnumerable<string> Flatten(IEnumerable<RecordFieldChange> changes)
                => changes.Select(c => $"{c.TableName}|{c.RowKey}|{c.RowState}|{c.FieldName}|{c.OldValue}|{c.NewValue}")
                          .OrderBy(x => x, StringComparer.Ordinal);

            Assert.NotEmpty(fromNew);
            Assert.Equal(Flatten(fromLegacy), Flatten(fromNew));
        }

        // ---- Non-payload and corrupt input ----

        [Theory]
        [InlineData("<AuditChanges />")]
        [InlineData("<AuditChanges></AuditChanges>")]
        [InlineData("<AuditChanges><NotASchema /></AuditChanges>")]
        [DisplayName("Returns an empty list instead of throwing when the wrapper element exists but its content is not a payload")]
        public void Read_WrapperWithoutUsableContent_ReturnsEmpty(string input)
        {
            var result = ChangeDiffGramReader.Read(input);

            Assert.Empty(result);
        }

        [Fact]
        [DisplayName("A minimal delete marker returns an empty list (the dispatch must not mistake it for the new format)")]
        public void Read_MinimalDeleteMarker_ReturnsEmpty()
        {
            var result = ChangeDiffGramReader.Read("<DeletedRow table=\"ft_order\" sys_rowid=\"abc\" />");

            Assert.Empty(result);
        }
    }
}
