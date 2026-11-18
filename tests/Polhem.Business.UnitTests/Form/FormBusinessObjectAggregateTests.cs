using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Numbers;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.Save"/> computes a master total from its detail rows (<c>Sum(Table.field)</c>)
    /// over every row of the record, whichever rows the payload carries.
    /// </summary>
    public class FormBusinessObjectAggregateTests : IClassFixture<SharedDbFixture>
    {
        private const string Total = "total";
        private const string Amount = "amount";

        private readonly SharedDbFixture _fx;

        public FormBusinessObjectAggregateTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A new record stores the sum of its detail rows")]
        public void Save_NewRecord_StoresSumOfDetails()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 10m, 20m);

                Assert.Equal(30m, ReadTotal(form, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A payload carrying only the edited detail row still sums every row of the record")]
        public void Save_PartialPayload_SumsStoredRowsToo()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 10m, 20m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                var lines = loaded.Tables[DetailName(form)]!;
                lines.Rows[0][Amount] = 15m;
                lines.Rows.Remove(lines.Rows[1]);

                NewBo(form).Save(new SaveArgs { DataSet = loaded });

                Assert.Equal(35m, ReadTotal(form, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A detail row the payload deletes is left out of the sum")]
        public void Save_PayloadDeletesRow_SumExcludesIt()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 10m, 20m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                var lines = loaded.Tables[DetailName(form)]!;
                var kept = Convert.ToDecimal(lines.Rows[1][Amount], CultureInfo.InvariantCulture);
                lines.Rows[0].Delete();
                lines.Rows.Remove(lines.Rows[1]);

                NewBo(form).Save(new SaveArgs { DataSet = loaded });

                Assert.Equal(kept, ReadTotal(form, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A payload that changes aggregated detail rows without the master row is refused")]
        public void Save_DetailChangesWithoutMaster_Throws()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 10m, 20m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[DetailName(form)]!.Rows[0][Amount] = 99m;
                loaded.Tables.Remove(form.Schema.ProgId);

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Equal(PolhemMessages.SaveMasterRowMissing, ex.MessageKey);
                Assert.Equal(30m, ReadTotal(form, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A rule that aggregates detail rows checks the stored rows a partial payload leaves out")]
        public void Save_PartialPayload_RuleSeesStoredRows()
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 60m, 10m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                var lines = loaded.Tables[DetailName(form)]!;
                var small = lines.Rows.Cast<DataRow>()
                    .Single(row => Convert.ToDecimal(row[Amount], CultureInfo.InvariantCulture) == 10m);
                small[Amount] = 70m;
                foreach (var other in lines.Rows.Cast<DataRow>().Where(row => row != small).ToList())
                    lines.Rows.Remove(other);

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Contains("At most one line", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                form.DropTables();
            }
        }

        private TransientForm NewRuleForm()
        {
            var form = NewForm(withTotal: false);
            form.Schema.Rules!.Add(new FormRule("few_large_lines",
                $"Count({DetailName(form)}, {Amount} > 50) < 2", "At most one line may exceed 50."));
            return form;
        }

        private static DataRow LineWith(DataTable lines, decimal amount)
            => lines.Rows.Cast<DataRow>().Single(row => Convert.ToDecimal(row[Amount], CultureInfo.InvariantCulture) == amount);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An unchanged detail row the caller rewrote is replaced by the stored row before a rule reads it")]
        public void Save_ForgedUnchangedRow_RuleReadsStoredValue()
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 60m, 10m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                var lines = loaded.Tables[DetailName(form)]!;
                var large = LineWith(lines, 60m);
                LineWith(lines, 10m)[Amount] = 70m;
                large[Amount] = 0m;
                large.AcceptChanges();

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Contains("At most one line", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A payload carrying two records of a form with aggregate rules is refused")]
        public void Save_TwoRecords_Refused()
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var rowId = SaveRecord(form, 60m, 10m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[form.Schema.ProgId]!.Rows.Add(Guid.NewGuid());
                LineWith(loaded.Tables[DetailName(form)]!, 10m)[Amount] = 70m;

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Equal(PolhemMessages.SaveOneRecordRequired, ex.MessageKey);
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbTheory(DatabaseType.SQLite)]
        [DisplayName("A detail row that belongs to another record is refused, whether added or modified")]
        [InlineData(true)]
        [InlineData(false)]
        public void Save_RowOfAnotherRecord_Refused(bool added)
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var target = SaveRecord(form, 60m, 10m);
                var other = SaveRecord(form, 5m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = other }).DataSet!;
                var lines = loaded.Tables[DetailName(form)]!;
                if (added)
                {
                    var row = lines.NewRow();
                    row[SysFields.RowId] = Guid.NewGuid();
                    row[SysFields.MasterRowId] = target;
                    row[Amount] = 70m;
                    lines.Rows.Add(row);
                }
                else
                {
                    var targetLines = NewBo(form).GetData(new GetDataArgs { RowId = target }).DataSet!.Tables[DetailName(form)]!;
                    var stolen = LineWith(targetLines, 10m);
                    lines.ImportRow(stolen);
                    var imported = lines.Rows[lines.Rows.Count - 1];
                    imported[SysFields.MasterRowId] = other;
                    imported[Amount] = 1m;
                }

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Equal(PolhemMessages.SaveDetailRecordMismatch, ex.MessageKey);
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbTheory(DatabaseType.SQLite)]
        [DisplayName("A row whose row id differs between its original and current versions is refused, in the detail or the master")]
        [InlineData(true)]
        [InlineData(false)]
        public void Save_RekeyedRow_Refused(bool detail)
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var target = SaveRecord(form, 60m, 10m);
                var own = SaveRecord(form, 5m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = own }).DataSet!;
                var table = detail ? loaded.Tables[DetailName(form)]! : loaded.Tables[form.Schema.ProgId]!;
                var victimRowId = detail
                    ? ValueUtilities.CGuid(LineWith(NewBo(form).GetData(new GetDataArgs { RowId = target }).DataSet!.Tables[DetailName(form)]!, 10m)[SysFields.RowId])
                    : target;
                var row = table.Rows[0];
                var ownRowId = row[SysFields.RowId];
                // Original names the victim's row, current names the caller's own: the update would hit the victim.
                row[SysFields.RowId] = victimRowId.ToString();
                row.AcceptChanges();
                row[SysFields.RowId] = ownRowId;
                if (detail) { row[Amount] = 70m; }

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Equal(PolhemMessages.SaveRowKeyChanged, ex.MessageKey);
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A payload that deletes its master row cannot change the detail rows of another record")]
        public void Save_DeletedMasterWithOtherRecordsRow_Refused()
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var target = SaveRecord(form, 60m, 10m);
                var own = SaveRecord(form, 5m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = own }).DataSet!;
                loaded.Tables[DetailName(form)]!.Rows[0].Delete();
                loaded.Tables[form.Schema.ProgId]!.Rows[0].Delete();
                var targetLines = NewBo(form).GetData(new GetDataArgs { RowId = target }).DataSet!.Tables[DetailName(form)]!;
                var lines = loaded.Tables[DetailName(form)]!;
                lines.ImportRow(LineWith(targetLines, 10m));
                lines.Rows[lines.Rows.Count - 1][Amount] = 70m;

                var ex = Assert.Throws<UserMessageException>(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Equal(PolhemMessages.SaveDetailRecordMismatch, ex.MessageKey);
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A payload that deletes a record with its own detail rows is saved")]
        public void Save_DeleteRecordWithItsRows_Succeeds()
        {
            var form = NewRuleForm();
            form.CreateTables();
            try
            {
                var own = SaveRecord(form, 5m, 7m);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = own }).DataSet!;
                foreach (var row in loaded.Tables[DetailName(form)]!.Rows.Cast<DataRow>().ToList()) { row.Delete(); }
                loaded.Tables[form.Schema.ProgId]!.Rows[0].Delete();

                var ex = Record.Exception(() => NewBo(form).Save(new SaveArgs { DataSet = loaded }));

                Assert.Null(ex);
            }
            finally
            {
                form.DropTables();
            }
        }

        private Guid SaveRecord(TransientForm form, params decimal[] amounts)
        {
            var rowId = Guid.NewGuid();
            var dataSet = form.Repository.GetNewData();
            dataSet.Tables[form.Schema.ProgId]!.Rows[0][SysFields.RowId] = rowId;
            foreach (var amount in amounts)
                dataSet.Tables[DetailName(form)]!.Rows.Add(Guid.NewGuid(), rowId, amount);
            NewBo(form).Save(new SaveArgs { DataSet = dataSet });
            return rowId;
        }

        private FormBusinessObject NewBo(TransientForm form)
            => new(form.CreateContext(), TestSessionFactory.CreateAccessToken(_fx), form.Schema.ProgId);

        private TransientForm NewForm(bool withTotal = true)
        {
            string progId = TransientForm.NewTableName("tb_agg_");
            string detailName = progId + "_d";
            var schema = new FormSchema(progId, "Aggregates") { CategoryId = TransientForm.CategoryId };

            var master = schema.Tables!.Add(progId, "Master");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields!.Add(new FormField(Total, "Total", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = withTotal ? $"Sum({detailName}.{Amount})" : string.Empty,
                ReadOnly = withTotal,
            });

            var detail = schema.Tables.Add(detailName, "Lines");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields!.Add(SysFields.MasterRowId, "Master Row Id", FieldDbType.Guid);
            detail.Fields!.Add(new FormField(Amount, "Amount", FieldDbType.Currency) { NumberKind = NumberKind.Amount });

            return new TransientForm(_fx, DatabaseType.SQLite, schema);
        }

        private static string DetailName(TransientForm form) => form.Schema.ProgId + "_d";

        private static decimal ReadTotal(TransientForm form, Guid rowId)
            => Convert.ToDecimal(form.DbAccess.ExecuteScalar(
                $"SELECT {form.Quote(Total)} FROM {form.Quote(form.Schema.ProgId)} WHERE {form.Quote(SysFields.RowId)}={{0}}", rowId),
                CultureInfo.InvariantCulture);
    }
}
