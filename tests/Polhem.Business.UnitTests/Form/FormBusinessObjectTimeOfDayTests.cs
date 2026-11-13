using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.Save"/> stores a time of day in its fixed-width <c>HH:mm</c> form whoever sent it,
    /// so a time-of-day column keeps sorting and range-scanning in time order (ADR-033).
    /// </summary>
    public class FormBusinessObjectTimeOfDayTests : IClassFixture<SharedDbFixture>
    {
        private const string Starts = "starts";
        private const string LineStarts = "line_starts";
        private const string Note = "note";

        private readonly SharedDbFixture _fx;

        public FormBusinessObjectTimeOfDayTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A time of day sent without its leading zero is stored in the fixed-width form, in the master and the detail")]
        public void Save_UnpaddedTimeOfDay_StoresFixedWidth()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                var dataSet = NewRecord(form, rowId, starts: "8:30");
                var lineId = AddLine(form, dataSet, rowId, starts: "9:05");

                NewBo(form).Save(new SaveArgs { DataSet = dataSet });

                Assert.Equal("08:30", ReadText(form, form.Schema.ProgId, Starts, rowId));
                Assert.Equal("09:05", ReadText(form, form.Schema.ProgId + "_d", LineStarts, lineId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An empty time of day stays empty, the form for not filled in")]
        public void Save_EmptyTimeOfDay_StaysEmpty()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();

                NewBo(form).Save(new SaveArgs { DataSet = NewRecord(form, rowId, starts: string.Empty) });

                Assert.Equal(string.Empty, ReadText(form, form.Schema.ProgId, Starts, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbTheory(DatabaseType.SQLite)]
        [InlineData("25:00")]
        [InlineData("abc")]
        [InlineData("08:30:15")]
        [DisplayName("A value that is not a time of day is refused as an invalid request naming the field, and nothing is written")]
        public void Save_InvalidTimeOfDay_RefusedNamingTheField(string value)
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();

                var ex = Assert.Throws<InvalidRequestValueException>(
                    () => NewBo(form).Save(new SaveArgs { DataSet = NewRecord(form, rowId, starts: value) }));

                Assert.Contains(Starts, ex.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(value, ex.Message, StringComparison.Ordinal);
                Assert.Null(ReadText(form, form.Schema.ProgId, Starts, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A modified row's time of day is stored in the fixed-width form")]
        public void Save_ModifiedRow_StoresFixedWidth()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                NewBo(form).Save(new SaveArgs { DataSet = NewRecord(form, rowId, starts: "08:00") });
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[form.Schema.ProgId]!.Rows[0][Starts] = "7:15";

                NewBo(form).Save(new SaveArgs { DataSet = loaded });

                Assert.Equal("07:15", ReadText(form, form.Schema.ProgId, Starts, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A value stored before the server normalized does not stop a save that edits another field")]
        public void Save_StoredInvalidValueUnchanged_SavesOtherField()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                NewBo(form).Save(new SaveArgs { DataSet = NewRecord(form, rowId, starts: "08:00") });
                form.DbAccess.ExecuteNonQuery(
                    $"UPDATE {form.Quote(form.Schema.ProgId)} SET {form.Quote(Starts)}={{0}} WHERE {form.Quote(SysFields.RowId)}={{1}}", "noon", rowId);
                var loaded = NewBo(form).GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                loaded.Tables[form.Schema.ProgId]!.Rows[0][Note] = "edited";

                NewBo(form).Save(new SaveArgs { DataSet = loaded });

                Assert.Equal("edited", ReadText(form, form.Schema.ProgId, Note, rowId));
                Assert.Equal("noon", ReadText(form, form.Schema.ProgId, Starts, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A value of white space is stored as the empty string, the form for not filled in")]
        public void Save_BlankTimeOfDay_StoresEmpty()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();

                NewBo(form).Save(new SaveArgs { DataSet = NewRecord(form, rowId, starts: "   ") });

                Assert.Equal(string.Empty, ReadText(form, form.Schema.ProgId, Starts, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A time of day a custom business object writes in DoBeforeSave is stored in the fixed-width form")]
        public void Save_ValueWrittenInDoBeforeSave_StoresFixedWidth()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();

                new StampingBo(form, _fx, "7:45").Save(new SaveArgs { DataSet = NewRecord(form, rowId, starts: "08:00") });

                Assert.Equal("07:45", ReadText(form, form.Schema.ProgId, Starts, rowId));
            }
            finally
            {
                form.DropTables();
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A value that is not a time of day written by server code is a fault of that code, not an invalid request")]
        public void Save_InvalidValueWrittenInDoBeforeSave_ThrowsInvalidOperation()
        {
            var form = NewForm();
            form.CreateTables();
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(
                    () => new StampingBo(form, _fx, "noon").Save(new SaveArgs { DataSet = NewRecord(form, Guid.NewGuid(), starts: "08:00") }));

                Assert.IsNotType<InvalidRequestValueException>(ex);
            }
            finally
            {
                form.DropTables();
            }
        }

        private FormBusinessObject NewBo(TransientForm form)
            => new(form.CreateContext(), TestSessionFactory.CreateAccessToken(_fx), form.Schema.ProgId);

        private TransientForm NewForm()
        {
            string progId = TransientForm.NewTableName("tb_tod_");
            var schema = new FormSchema(progId, "Times of day") { CategoryId = TransientForm.CategoryId };

            var master = schema.Tables!.Add(progId, "Master");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields!.Add(Starts, "Starts", FieldDbType.Time);
            master.Fields!.Add(new FormField(Note, "Note", FieldDbType.String) { MaxLength = 20 });

            var detail = schema.Tables.Add(progId + "_d", "Lines");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields!.Add(SysFields.MasterRowId, "Master Row Id", FieldDbType.Guid);
            detail.Fields!.Add(LineStarts, "Line starts", FieldDbType.Time);

            return new TransientForm(_fx, DatabaseType.SQLite, schema);
        }

        private static DataSet NewRecord(TransientForm form, Guid rowId, string starts)
        {
            var dataSet = form.Repository.GetNewData();
            var master = dataSet.Tables[form.Schema.ProgId]!.Rows[0];
            master[SysFields.RowId] = rowId;
            master[Starts] = starts;
            return dataSet;
        }

        private static Guid AddLine(TransientForm form, DataSet dataSet, Guid rowId, string starts)
        {
            var lineId = Guid.NewGuid();
            dataSet.Tables[form.Schema.ProgId + "_d"]!.Rows.Add(lineId, rowId, starts);
            return lineId;
        }

        private static string? ReadText(TransientForm form, string table, string column, Guid rowId)
            => form.DbAccess.ExecuteScalar(
                $"SELECT {form.Quote(column)} FROM {form.Quote(table)} WHERE {form.Quote(SysFields.RowId)}={{0}}", rowId)?.ToString();

        /// <summary>A custom business object that writes a time of day of its own before the save.</summary>
        private sealed class StampingBo(TransientForm form, SharedDbFixture fx, string value)
            : FormBusinessObject(form.CreateContext(), TestSessionFactory.CreateAccessToken(fx), form.Schema.ProgId)
        {
            protected override void DoBeforeSave(SaveContext context)
            {
                base.DoBeforeSave(context);
                context.DataSet.Tables[form.Schema.ProgId]!.Rows[0][Starts] = value;
            }
        }
    }
}
