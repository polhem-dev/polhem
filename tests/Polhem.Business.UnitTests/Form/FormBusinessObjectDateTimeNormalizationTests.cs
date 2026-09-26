using System.ComponentModel;
using System.Data;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Base.Exceptions;
using Polhem.Business.AuditLog;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.Save"/> does not accept a <c>DateTime</c> sent by the caller: added rows get server values,
    /// modified and deleted rows have both versions overwritten with the values read back from the database, and system timestamps are stamped by the framework.
    /// </summary>
    /// <remarks>
    /// Every test calls <see cref="FormBusinessObject.Save"/> directly, without the Connector,
    /// so it also verifies that calls between server-side BOs do not accept the value either.
    /// The value passed in is always <see cref="s_clientValue"/>; any column stored with it means normalization missed something.
    /// </remarks>
    public class FormBusinessObjectDateTimeNormalizationTests : IClassFixture<SharedDbFixture>
    {
        private const string Note = "note";
        private const string EventTime = "event_time";
        private const string DefaultedTime = "defaulted_time";
        private const string ComputedTime = "computed_time";
        private const string LineTime = "line_time";

        private static readonly DateTime s_clientValue = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Unspecified);
        private static readonly DateTime s_storedInsert = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        private static readonly DateTime s_storedUpdate = new(2020, 1, 3, 3, 4, 5, DateTimeKind.Unspecified);
        private static readonly DateTime s_storedEvent = new(2020, 1, 4, 3, 4, 5, DateTimeKind.Unspecified);
        private static readonly DateTime s_storedLine = new(2020, 1, 5, 3, 4, 5, DateTimeKind.Unspecified);
        private static readonly DateTime s_storedDeletedLine = new(2020, 1, 6, 3, 4, 5, DateTimeKind.Unspecified);

        private static readonly string[] s_masterServerTimes = [SysFields.InsertTime, SysFields.UpdateTime, EventTime, DefaultedTime, ComputedTime];

        private readonly SharedDbFixture _fx;

        public FormBusinessObjectDateTimeNormalizationTests(SharedDbFixture fx) { _fx = fx; }

        #region Added rows

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a DateTime passed in for an added row is not stored; system timestamps, NOT NULL columns and expression columns are written by the server")]
        public void Save_Sqlite_AddedRow_UsesServerValues() => RunAddedRowUsesServerValues(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a DateTime passed in for an added row is not stored; system timestamps, NOT NULL columns and expression columns are written by the server")]
        public void Save_SqlServer_AddedRow_UsesServerValues() => RunAddedRowUsesServerValues(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a DateTime passed in for an added row is not stored; system timestamps, NOT NULL columns and expression columns are written by the server")]
        public void Save_PostgreSql_AddedRow_UsesServerValues() => RunAddedRowUsesServerValues(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a DateTime passed in for an added row is not stored; system timestamps, NOT NULL columns and expression columns are written by the server")]
        public void Save_MySql_AddedRow_UsesServerValues() => RunAddedRowUsesServerValues(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a DateTime passed in for an added row is not stored; system timestamps, NOT NULL columns and expression columns are written by the server")]
        public void Save_Oracle_AddedRow_UsesServerValues() => RunAddedRowUsesServerValues(DatabaseType.Oracle);

        private void RunAddedRowUsesServerValues(DatabaseType databaseType)
        {
            var form = NewForm(databaseType);
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var lineId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, lineId);

                var before = DateTime.UtcNow;
                new FormBusinessObject(form.CreateContext(), Guid.NewGuid(), form.Schema.ProgId)
                    .Save(new SaveArgs { DataSet = dataSet });
                var after = DateTime.UtcNow;

                foreach (var column in s_masterServerTimes)
                {
                    AssertServerReading(ReadInstant(form, form.Schema.ProgId, column, rowId), before, after, column);
                }
                AssertServerReading(ReadInstant(form, DetailName(form), LineTime, lineId), before, after, LineTime);
            }
            finally
            {
                form.DropTables();
            }
        }

        #endregion

        #region Modified and deleted rows

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a modified row keeps the stored DateTime, sys_update_time advances, non-time edits are kept in full, and the audit records the stored values")]
        public void Save_Sqlite_StoredRows_KeepStoredValues() => RunStoredRowsKeepStoredValues(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a modified row keeps the stored DateTime, sys_update_time advances, non-time edits are kept in full, and the audit records the stored values")]
        public void Save_SqlServer_StoredRows_KeepStoredValues() => RunStoredRowsKeepStoredValues(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a modified row keeps the stored DateTime, sys_update_time advances, non-time edits are kept in full, and the audit records the stored values")]
        public void Save_PostgreSql_StoredRows_KeepStoredValues() => RunStoredRowsKeepStoredValues(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a modified row keeps the stored DateTime, sys_update_time advances, non-time edits are kept in full, and the audit records the stored values")]
        public void Save_MySql_StoredRows_KeepStoredValues() => RunStoredRowsKeepStoredValues(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a modified row keeps the stored DateTime, sys_update_time advances, non-time edits are kept in full, and the audit records the stored values")]
        public void Save_Oracle_StoredRows_KeepStoredValues() => RunStoredRowsKeepStoredValues(DatabaseType.Oracle);

        private void RunStoredRowsKeepStoredValues(DatabaseType databaseType)
        {
            var form = NewForm(databaseType);
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var lineId = Guid.NewGuid();
                var deletedLineId = Guid.NewGuid();
                SeedStoredRecord(form, rowId, lineId, deletedLineId);

                var dataSet = new FormBusinessObject(form.CreateContext(), Guid.NewGuid(), form.Schema.ProgId)
                    .GetData(new GetDataArgs { RowId = rowId }).DataSet!;

                var master = dataSet.Tables[form.Schema.ProgId]!.Rows[0];
                master[Note] = "changed";
                master[SysFields.InsertTime] = s_clientValue;
                master[EventTime] = s_clientValue;

                var lines = dataSet.Tables[DetailName(form)]!;
                var line = FindRow(lines, lineId);
                line[Note] = "changed line";
                line[LineTime] = s_clientValue;

                // Makes Original the client value too before deleting, simulating a row after the Connector converted the response into the user's time zone.
                var deletedLine = FindRow(lines, deletedLineId);
                deletedLine[LineTime] = s_clientValue;
                deletedLine.AcceptChanges();
                deletedLine.Delete();

                var writer = new CapturingAuditLogWriter();
                var before = DateTime.UtcNow;
                new FormBusinessObject(form.CreateContext(AuditOverrides(writer)), Guid.NewGuid(), form.Schema.ProgId)
                    .Save(new SaveArgs { DataSet = dataSet });
                var after = DateTime.UtcNow;

                Assert.Equal("changed", ReadText(form, form.Schema.ProgId, Note, rowId));
                Assert.Equal("changed line", ReadText(form, DetailName(form), Note, lineId));
                Assert.Equal(s_storedInsert, ReadInstant(form, form.Schema.ProgId, SysFields.InsertTime, rowId));
                Assert.Equal(s_storedEvent, ReadInstant(form, form.Schema.ProgId, EventTime, rowId));
                Assert.Equal(s_storedLine, ReadInstant(form, DetailName(form), LineTime, lineId));
                AssertServerReading(ReadInstant(form, form.Schema.ProgId, SysFields.UpdateTime, rowId), before, after, SysFields.UpdateTime);
                Assert.Null(form.DbAccess.ExecuteScalar(SelectSql(form, DetailName(form), Note), deletedLineId));

                var changes = ChangeDiffGramReader.Read(Assert.IsType<ChangeAuditEntry>(Assert.Single(writer.Entries)).ChangesXml);
                Assert.Contains(changes, c => c.FieldName == Note);
                Assert.DoesNotContain(changes, c => c.RowState == ChangeKind.Update &&
                    c.FieldName is SysFields.InsertTime or EventTime or LineTime);
                var updateTime = Assert.Single(changes, c => c.FieldName == SysFields.UpdateTime);
                Assert.Equal(s_storedUpdate, ValueUtilities.CDateTime(updateTime.OldValue));
                var deletedLineTime = Assert.Single(changes, c => c.RowState == ChangeKind.Delete && c.FieldName == LineTime);
                Assert.Equal(s_storedDeletedLine, ValueUtilities.CDateTime(deletedLineTime.OldValue));
            }
            finally
            {
                form.DropTables();
            }
        }

        #endregion

        #region Row missing on read-back

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a modified row deleted before the read-back throws UserMessageException and nothing is written to the database")]
        public void Save_Sqlite_RowGoneBeforeReadBack_Throws() => RunRowGoneBeforeReadBackThrows(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a modified row deleted before the read-back throws UserMessageException and nothing is written to the database")]
        public void Save_SqlServer_RowGoneBeforeReadBack_Throws() => RunRowGoneBeforeReadBackThrows(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a modified row deleted before the read-back throws UserMessageException and nothing is written to the database")]
        public void Save_PostgreSql_RowGoneBeforeReadBack_Throws() => RunRowGoneBeforeReadBackThrows(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a modified row deleted before the read-back throws UserMessageException and nothing is written to the database")]
        public void Save_MySql_RowGoneBeforeReadBack_Throws() => RunRowGoneBeforeReadBackThrows(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a modified row deleted before the read-back throws UserMessageException and nothing is written to the database")]
        public void Save_Oracle_RowGoneBeforeReadBack_Throws() => RunRowGoneBeforeReadBackThrows(DatabaseType.Oracle);

        private void RunRowGoneBeforeReadBackThrows(DatabaseType databaseType)
        {
            var form = NewForm(databaseType);
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var lineId = Guid.NewGuid();
                SeedStoredRecord(form, rowId, lineId, Guid.NewGuid());

                var dataSet = new FormBusinessObject(form.CreateContext(), Guid.NewGuid(), form.Schema.ProgId)
                    .GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                dataSet.Tables[form.Schema.ProgId]!.Rows[0][Note] = "changed";
                FindRow(dataSet.Tables[DetailName(form)]!, lineId)[Note] = "changed line";

                form.DbAccess.ExecuteNonQuery(
                    $"DELETE FROM {form.Quote(DetailName(form))} WHERE {form.Quote(SysFields.RowId)}={{0}}", lineId);

                var bo = new FormBusinessObject(form.CreateContext(), Guid.NewGuid(), form.Schema.ProgId);
                Assert.Throws<UserMessageException>(() => bo.Save(new SaveArgs { DataSet = dataSet }));

                // The master is processed before the detail and its read-back succeeds. That it was not written proves the whole batch stopped before any write.
                Assert.Equal("stored", ReadText(form, form.Schema.ProgId, Note, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        #endregion

        #region Override seam

        /// <summary>
        /// The override seam: a BO that accepts the client's <c>event_time</c> overrides only the normalization method.
        /// </summary>
        private sealed class AcceptsEventTimeBo : FormBusinessObject
        {
            public AcceptsEventTimeBo(IPolhemContext ctx, string progId) : base(ctx, Guid.NewGuid(), progId) { }

            protected override void NormalizeDateTimes(SaveContext context)
            {
                var master = context.DataSet.Tables[context.Schema.ProgId]!;
                var supplied = master.Rows.Cast<DataRow>()
                    .Where(row => row.RowState == DataRowState.Added)
                    .Select(row => (Row: row, Value: row[EventTime]))
                    .ToList();

                base.NormalizeDateTimes(context);

                // A real BO would convert the user's time zone value to UTC here. The test only needs to show that the value written back is stored.
                foreach (var (row, value) in supplied) { row[EventTime] = value; }
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A BO that overrides NormalizeDateTimes can keep its own converted value, while the base still normalizes the other columns")]
        public void Save_OverriddenNormalization_KeepsValueTheBusinessObjectWrote()
        {
            var form = NewForm(DatabaseType.SQLite);
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, Guid.NewGuid());
                dataSet.Tables[form.Schema.ProgId]!.Rows[0][EventTime] = s_clientValue;

                var before = DateTime.UtcNow;
                new AcceptsEventTimeBo(form.CreateContext(), form.Schema.ProgId).Save(new SaveArgs { DataSet = dataSet });
                var after = DateTime.UtcNow;

                Assert.Equal(s_clientValue, ReadInstant(form, form.Schema.ProgId, EventTime, rowId));
                AssertServerReading(ReadInstant(form, form.Schema.ProgId, SysFields.InsertTime, rowId), before, after, SysFields.InsertTime);
            }
            finally
            {
                form.DropTables();
            }
        }

        #endregion

        #region Shared

        private sealed class CapturingAuditLogWriter : IAuditLogWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public void Write(AuditEntry entry) => Entries.Add(entry);
        }

        private static (Type, object?)[] AuditOverrides(IAuditLogWriter writer) =>
        [
            (typeof(AuditLogOptions), new AuditLogOptions { Enabled = true, ChangeEnabled = true, AccessEnabled = false }),
            (typeof(IAuditLogWriter), writer),
        ];

        private TransientForm NewForm(DatabaseType databaseType)
        {
            string progId = TransientForm.NewTableName("tb_dtn_");
            var schema = new FormSchema(progId, "DateTime normalization") { CategoryId = TransientForm.CategoryId };

            var master = schema.Tables!.Add(progId, "Master");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields!.AddStringField(Note, "Note", 50);
            master.Fields.Add(SysFields.InsertTime, "Insert Time", FieldDbType.DateTime);
            master.Fields.Add(SysFields.UpdateTime, "Update Time", FieldDbType.DateTime);
            master.Fields.Add(EventTime, "Event Time", FieldDbType.DateTime);
            master.Fields.Add(DefaultedTime, "Defaulted Time", FieldDbType.DateTime).DefaultValueExpression = "Now()";
            master.Fields.Add(ComputedTime, "Computed Time", FieldDbType.DateTime).ValueExpression = "Now()";

            var detail = schema.Tables.Add(progId + "_d", "Detail");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Master Row Id", FieldDbType.Guid);
            detail.Fields!.AddStringField(Note, "Note", 50);
            detail.Fields.Add(LineTime, "Line Time", FieldDbType.DateTime);

            return new TransientForm(_fx, databaseType, schema);
        }

        private static string DetailName(TransientForm form) => form.Schema.ProgId + "_d";

        /// <summary>
        /// A new document where every time column carries the client value and only <c>event_time</c> is sent empty, to verify that the server fills the NOT NULL column.
        /// </summary>
        private static DataSet NewRecord(TransientForm form, Guid rowId, Guid lineId)
        {
            var dataSet = form.Repository.GetNewData();
            var master = dataSet.Tables[form.Schema.ProgId]!.Rows[0];
            master[SysFields.RowId] = rowId;
            master[Note] = "new";
            master[SysFields.InsertTime] = s_clientValue;
            master[SysFields.UpdateTime] = s_clientValue;
            master[EventTime] = DBNull.Value;
            master[DefaultedTime] = s_clientValue;
            master[ComputedTime] = s_clientValue;

            var lines = dataSet.Tables[DetailName(form)]!;
            lines.Rows.Add(lineId, rowId, "line", s_clientValue);
            return dataSet;
        }

        /// <summary>
        /// Writes an existing document directly with SQL, with every time column set to a known value clearly different from now.
        /// </summary>
        private static void SeedStoredRecord(TransientForm form, Guid rowId, Guid lineId, Guid deletedLineId)
        {
            string master = form.Quote(form.Schema.ProgId);
            string detail = form.Quote(DetailName(form));
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {master} ({Columns(form, SysFields.RowId, Note, SysFields.InsertTime, SysFields.UpdateTime, EventTime, DefaultedTime, ComputedTime)}) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                rowId, "stored", s_storedInsert, s_storedUpdate, s_storedEvent, s_storedEvent, s_storedEvent);

            string lineColumns = Columns(form, SysFields.RowId, SysFields.MasterRowId, Note, LineTime);
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {detail} ({lineColumns}) VALUES ({{0}}, {{1}}, {{2}}, {{3}})",
                lineId, rowId, "stored line", s_storedLine);
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {detail} ({lineColumns}) VALUES ({{0}}, {{1}}, {{2}}, {{3}})",
                deletedLineId, rowId, "stored deleted line", s_storedDeletedLine);
        }

        private static string Columns(TransientForm form, params string[] names)
            => string.Join(", ", names.Select(form.Quote));

        private static DataRow FindRow(DataTable table, Guid rowId)
            => table.Rows.Cast<DataRow>().Single(row => ValueUtilities.CGuid(row[SysFields.RowId]) == rowId);

        private static string SelectSql(TransientForm form, string table, string column)
            => $"SELECT {form.Quote(column)} FROM {form.Quote(table)} WHERE {form.Quote(SysFields.RowId)}={{0}}";

        private static DateTime? ReadInstant(TransientForm form, string table, string column, Guid rowId)
            => ValueUtilities.CDateTime(form.DbAccess.ExecuteScalar(SelectSql(form, table, column), rowId));

        private static string? ReadText(TransientForm form, string table, string column, Guid rowId)
            => form.DbAccess.ExecuteScalar(SelectSql(form, table, column), rowId)?.ToString();

        /// <summary>
        /// The value must be a UTC reading taken during this save. One second of slack on each side covers column types that store only seconds.
        /// </summary>
        private static void AssertServerReading(DateTime? value, DateTime before, DateTime after, string column)
        {
            Assert.True(value.HasValue, $"'{column}' was not written.");
            Assert.InRange(value!.Value, before.AddSeconds(-1), after.AddSeconds(1));
        }

        #endregion
    }
}
