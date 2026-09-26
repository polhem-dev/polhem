using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Api.Contracts.AuditLog;
using Polhem.Base.Data;
using Polhem.Business.AuditLog;
using Polhem.Definition.Logging;

namespace Polhem.Business.UnitTests.AuditLog
{
    /// <summary>
    /// The deleted record payload (<c>AuditDeletedRecord</c>): the original record is written as is without modifying the DataSet passed in,
    /// and the column list read back is identical, entry by entry, to the old approach (mark every row Deleted, then write a change set).
    /// </summary>
    /// <remarks>
    /// Production no longer produces the old approach, but old delete records in databases still have that shape. So the consistency tests
    /// keep their own copy of the old approach in this file. Calling the production writer instead would silently switch these tests to the new shape, and old data would lose its regression guard.
    /// </remarks>
    public class DeletedRecordPayloadTests
    {
        private const string ProgId = "Employee";
        private const string DetailTableName = "EmployeeSkill";
        private const string EmptyDetailTableName = "EmployeeNote";
        private static readonly Guid s_masterId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly string s_textWithControls = "含" + (char)1 + "控制字元\r\n換行";

        /// <summary>
        /// Builds an original record shaped like <c>DataFormRepository.GetData</c>: the DataSet name matches the master table name, columns are
        /// created through <c>AddColumn</c> by <see cref="FieldDbType"/> (one column per type, plus one DBNull column),
        /// with one detail table and one empty detail table, and finally <c>AcceptChanges</c>.
        /// </summary>
        private static DataSet NewRecord(bool withDetailRows)
        {
            var dataSet = new DataSet(ProgId) { Locale = CultureInfo.InvariantCulture };

            var master = new DataTable(ProgId) { Locale = CultureInfo.InvariantCulture };
            master.AddColumn("sys_rowid", FieldDbType.Guid);
            foreach (var dbType in Enum.GetValues<FieldDbType>().Where(t => t != FieldDbType.Unknown))
            {
                master.AddColumn("f_" + dbType.ToString().ToLowerInvariant(), dbType);
            }
            master.AddColumn("f_null", FieldDbType.String).AllowDBNull = true;
            dataSet.Tables.Add(master);

            var masterRow = master.NewRow();
            foreach (DataColumn column in master.Columns)
            {
                masterRow[column] = SampleValue(column);
            }
            masterRow["sys_rowid"] = s_masterId;
            masterRow["f_null"] = DBNull.Value;
            master.Rows.Add(masterRow);

            var detail = new DataTable(DetailTableName) { Locale = CultureInfo.InvariantCulture };
            detail.AddColumn("sys_rowid", FieldDbType.Guid);
            detail.AddColumn("sys_master_rowid", FieldDbType.Guid);
            detail.AddColumn("skill", FieldDbType.String);
            dataSet.Tables.Add(detail);
            if (withDetailRows)
            {
                detail.Rows.Add(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), s_masterId, "C#");
                detail.Rows.Add(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), s_masterId, "SQL");
            }

            var emptyDetail = new DataTable(EmptyDetailTableName) { Locale = CultureInfo.InvariantCulture };
            emptyDetail.AddColumn("sys_rowid", FieldDbType.Guid);
            emptyDetail.AddColumn("note", FieldDbType.Text);
            dataSet.Tables.Add(emptyDetail);

            dataSet.AcceptChanges();
            return dataSet;
        }

        /// <summary>
        /// Gives a sample value by the column's CLR type, and throws for any type not listed. If a new <see cref="FieldDbType"/>
        /// maps to a new CLR type, these tests should go red rather than skip that column.
        /// </summary>
        private static object SampleValue(DataColumn column)
        {
            var type = column.DataType;
            if (type == typeof(string)) { return column.ColumnName == "f_string" ? s_textWithControls : "08:30"; }
            if (type == typeof(bool)) { return true; }
            if (type == typeof(short)) { return (short)-3; }
            if (type == typeof(int)) { return 42; }
            if (type == typeof(long)) { return 9_000_000_000L; }
            if (type == typeof(decimal)) { return 1234.50m; }
            if (type == typeof(DateTime)) { return new DateTime(2026, 9, 11, 13, 45, 30, DateTimeKind.Unspecified); }
            if (type == typeof(TimeSpan)) { return new TimeSpan(8, 30, 0); }
            if (type == typeof(Guid)) { return Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"); }
            if (type == typeof(byte[])) { return new byte[] { 1, 2, 3 }; }
            throw new InvalidOperationException($"No sample value for column '{column.ColumnName}' of type {type}.");
        }

        /// <summary>
        /// The old approach: copy the original record, mark every row Deleted, then write a change set with <c>GetChanges</c>.
        /// </summary>
        private static string SerializeLegacyDeletedRecord(DataSet record)
        {
            using var marked = record.Copy();
            foreach (DataTable table in marked.Tables)
            {
                for (int i = table.Rows.Count - 1; i >= 0; i--)
                {
                    table.Rows[i].Delete();
                }
            }
            using var changes = marked.GetChanges()!;
            return AuditDiffGram.Serialize(changes);
        }

        private static List<string> Flatten(IEnumerable<RecordFieldChange> changes)
            => changes.Select(c => $"{c.TableName}|{c.RowKey}|{c.RowState}|{c.FieldName}|{c.OldValue}|{c.NewValue}")
                      .OrderBy(x => x, StringComparer.Ordinal)
                      .ToList();

        [Fact]
        [DisplayName("The deleted record has AuditDeletedRecord as its root, has no before section, and does not modify the DataSet passed in")]
        public void SerializeDeletedRecord_WritesRecordAsLoaded_WithoutModifyingIt()
        {
            using var record = NewRecord(withDetailRows: true);

            string xml = AuditDiffGram.SerializeDeletedRecord(record);

            Assert.StartsWith("<" + AuditDiffGram.DeletedRecordRootElementName + ">", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("diffgr:before", xml, StringComparison.Ordinal);
            Assert.All(record.Tables.Cast<DataTable>().SelectMany(t => t.Rows.Cast<DataRow>()),
                row => Assert.Equal(DataRowState.Unchanged, row.RowState));
        }

        [Fact]
        [DisplayName("Every non-key column of the deleted record reads back as Delete, and values with control characters and CR are restored verbatim")]
        public void Read_DeletedRecord_EmitsEveryColumnAsDelete()
        {
            using var record = NewRecord(withDetailRows: true);

            var result = ChangeDiffGramReader.Read(AuditDiffGram.SerializeDeletedRecord(record));

            int expected = record.Tables.Cast<DataTable>().Sum(t => t.Rows.Count * (t.Columns.Count - 1));
            Assert.Equal(expected, result.Count);
            Assert.All(result, c => Assert.Equal(ChangeKind.Delete, c.RowState));
            Assert.All(result, c => Assert.Null(c.NewValue));
            Assert.Contains(result, c => c.TableName == DetailTableName && c.FieldName == "skill" && c.OldValue == "SQL");
            var text = Assert.Single(result, c => c.FieldName == "f_string");
            Assert.Equal(s_masterId.ToString(), text.RowKey);
            Assert.Equal(s_textWithControls, text.OldValue);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("The delete payloads produced for the same record by the new and old approaches read back identical column lists")]
        public void Read_DeletedRecord_MatchesLegacyDeletedRowsPayload(bool withDetailRows)
        {
            using var record = NewRecord(withDetailRows);

            var fromNew = ChangeDiffGramReader.Read(AuditDiffGram.SerializeDeletedRecord(record));
            var fromLegacy = ChangeDiffGramReader.Read(SerializeLegacyDeletedRecord(record));

            Assert.NotEmpty(fromNew);
            Assert.Equal(Flatten(fromLegacy), Flatten(fromNew));
        }
    }
}
