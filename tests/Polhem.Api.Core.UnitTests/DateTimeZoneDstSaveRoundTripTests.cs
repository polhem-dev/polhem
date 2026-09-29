using System.ComponentModel;
using System.Globalization;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Load, change another column, save back, during the DST fall-back overlap: the value of an instant column must
    /// not change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In US Eastern time on 2026-11-01, both 05:30Z and 06:30Z become the wall-clock time 01:30 through
    /// <see cref="DateTimeZoneConverter.UtcToUser(global::System.Data.DataSet, string)"/>. The information is lost the moment
    /// the response is converted into the user's time zone, and any conversion from wall-clock time back to UTC can
    /// only resolve deterministically to one of the two.
    /// </para>
    /// <para>
    /// The fix is on the server: <see cref="FormBusinessObject.Save"/> does not take the incoming <c>DateTime</c>; the
    /// instant columns of modified rows are overwritten with the values read back from the database. So this test
    /// deliberately hands the user-time-zone <c>DataSet</c> to Save as is, without any request-direction conversion.
    /// </para>
    /// <para>
    /// The whole chain uses real components: the response-direction conversion, the normalization in Save, the adapter
    /// write of <c>DataFormRepository</c>, and finally reading the stored value back with SQL. The defect only shows in
    /// the database, so these tests pin that a value the user did not touch is still the original value in the
    /// database after saving.
    /// </para>
    /// </remarks>
    public class DateTimeZoneDstSaveRoundTripTests : IClassFixture<SharedDbFixture>
    {
        private const string NewYork = "America/New_York";
        private const string NameColumn = "name";
        private const string InstantColumn = "occurred_at";

        // The earlier UTC value is 01:30 EDT. 06:30Z is the same wall-clock time as 01:30 EST.
        private static readonly DateTime s_firstOccurrenceUtc = new(2026, 11, 1, 5, 30, 0, DateTimeKind.Unspecified);

        private readonly SharedDbFixture _fx;

        public DateTimeZoneDstSaveRoundTripTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Precondition: 05:30Z and 06:30Z are the same wall-clock time in America/New_York")]
        public void Precondition_BothUtcValuesShareOneWallClockTime()
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(NewYork);
            var first = TimeZoneInfo.ConvertTimeFromUtc(s_firstOccurrenceUtc, zone);
            var second = TimeZoneInfo.ConvertTimeFromUtc(s_firstOccurrenceUtc.AddHours(1), zone);

            Assert.Equal(first, second);
            Assert.True(zone.IsAmbiguousTime(first));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an instant column in the DST overlap keeps its stored value when another column of the row is changed and saved")]
        public void Save_Sqlite_AmbiguousInstantUntouched_KeepsStoredValue()
            => RunUntouchedInstantKeepsValue(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: an instant column in the DST overlap keeps its stored value when another column of the row is changed and saved")]
        public void Save_SqlServer_AmbiguousInstantUntouched_KeepsStoredValue()
            => RunUntouchedInstantKeepsValue(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: an instant column in the DST overlap keeps its stored value when another column of the row is changed and saved")]
        public void Save_PostgreSql_AmbiguousInstantUntouched_KeepsStoredValue()
            => RunUntouchedInstantKeepsValue(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: an instant column in the DST overlap keeps its stored value when another column of the row is changed and saved")]
        public void Save_MySql_AmbiguousInstantUntouched_KeepsStoredValue()
            => RunUntouchedInstantKeepsValue(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: an instant column in the DST overlap keeps its stored value when another column of the row is changed and saved")]
        public void Save_Oracle_AmbiguousInstantUntouched_KeepsStoredValue()
            => RunUntouchedInstantKeepsValue(DatabaseType.Oracle);

        private void RunUntouchedInstantKeepsValue(DatabaseType databaseType)
        {
            string progId = TransientForm.NewTableName("tb_dst_");
            var schema = new FormSchema(progId, "DST round trip") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "DST");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField(NameColumn, "Name", FieldDbType.String) { MaxLength = 50 });
            table.Fields.Add(InstantColumn, "Occurred At", FieldDbType.DateTime);

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                form.DbAccess.ExecuteNonQuery(
                    $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote(NameColumn)}, {form.Quote(InstantColumn)}) " +
                    "VALUES ({0}, {1}, {2})",
                    rowId, "before", s_firstOccurrenceUtc);

                // Server-side `GetData` hands UTC to the connector; the client holds the copy converted into the user's time zone.
                var loaded = new FormBusinessObject(form.CreateContext(), TestSessionFactory.CreateAccessToken(_fx), progId)
                    .GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                var onScreen = DateTimeZoneConverter.UtcToUser(loaded, NewYork)!;
                onScreen.Tables[progId]!.Rows[0][NameColumn] = "after";

                new FormBusinessObject(form.CreateContext(), TestSessionFactory.CreateAccessToken(_fx), progId)
                    .Save(new SaveArgs { DataSet = onScreen });

                Assert.Equal("after", Convert.ToString(
                    form.DbAccess.ExecuteScalar(SelectSql(form, NameColumn), rowId), CultureInfo.InvariantCulture));
                Assert.Equal(s_firstOccurrenceUtc,
                    ValueUtilities.CDateTime(form.DbAccess.ExecuteScalar(SelectSql(form, InstantColumn), rowId)));
            }
            finally
            {
                form.DropTables();
            }
        }

        private static string SelectSql(TransientForm form, string column)
            => $"SELECT {form.Quote(column)} FROM {form.Quote(form.Schema.ProgId)} WHERE {form.Quote(SysFields.RowId)}={{0}}";
    }
}
