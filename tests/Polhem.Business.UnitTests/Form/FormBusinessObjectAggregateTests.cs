using System.ComponentModel;
using System.Globalization;
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

        private TransientForm NewForm()
        {
            string progId = TransientForm.NewTableName("tb_agg_");
            string detailName = progId + "_d";
            var schema = new FormSchema(progId, "Aggregates") { CategoryId = TransientForm.CategoryId };

            var master = schema.Tables!.Add(progId, "Master");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields!.Add(new FormField(Total, "Total", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = $"Sum({detailName}.{Amount})",
                ReadOnly = true,
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
