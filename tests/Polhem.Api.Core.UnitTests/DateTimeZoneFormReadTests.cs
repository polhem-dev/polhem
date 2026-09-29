using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Transformers;
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
    /// An instant column read back by a form must shift to the user's time zone after the response-direction
    /// conversion; a date column does not shift.
    /// </summary>
    /// <remarks>
    /// Handing the result of <see cref="FormBusinessObject.GetData"/> straight to the converter matches an in-process
    /// (Local) call: that path does not serialize, so the converter sees the <c>DataTable</c> the repository read back.
    /// SQLite also covers the two wires: a wire rebuilds columns from the declared types, so the converter sees a
    /// different shape than in process, and both must be right. SQLite stores dates as text and is the only database
    /// where the repository has to convert the column type.
    /// </remarks>
    public class DateTimeZoneFormReadTests : IClassFixture<SharedDbFixture>
    {
        private const string Taipei = "Asia/Taipei";
        private const string InstantColumn = "occurred_at";
        private const string DayColumn = "due_on";

        private static readonly DateTime s_storedUtc = new(2026, 3, 1, 1, 30, 0, DateTimeKind.Unspecified);
        private static readonly DateTime s_expectedTaipei = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Unspecified);
        private static readonly DateTime s_storedDay = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private readonly SharedDbFixture _fx;

        public DateTimeZoneFormReadTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an instant column read back by a form shifts to the user time zone in an in-process conversion")]
        public void UtcToUser_Sqlite_FormInstantColumn_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.SQLite, InProcess));

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: an instant column read back by a form shifts to the user time zone in an in-process conversion")]
        public void UtcToUser_SqlServer_FormInstantColumn_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.SQLServer, InProcess));

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: an instant column read back by a form shifts to the user time zone in an in-process conversion")]
        public void UtcToUser_PostgreSql_FormInstantColumn_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.PostgreSQL, InProcess));

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: an instant column read back by a form shifts to the user time zone in an in-process conversion")]
        public void UtcToUser_MySql_FormInstantColumn_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.MySQL, InProcess));

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: an instant column read back by a form shifts to the user time zone in an in-process conversion")]
        public void UtcToUser_Oracle_FormInstantColumn_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.Oracle, InProcess));

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an instant column read back by a form shifts to the user time zone after the MessagePack wire")]
        public void UtcToUser_SqliteAfterMessagePackWire_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.SQLite, OverMessagePack));

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an instant column read back by a form shifts to the user time zone after the JSON wire")]
        public void UtcToUser_SqliteAfterJsonWire_ShiftsToUserZone()
            => Assert.Equal(s_expectedTaipei, InstantAfter(DatabaseType.SQLite, OverJson));

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a date column read back by a form does not shift in an in-process conversion to the user time zone")]
        public void UtcToUser_Sqlite_FormDateColumn_DoesNotShift()
            => Assert.Equal(s_storedDay, ValueUtilities.CDateTime(ReadAndConvert(DatabaseType.SQLite, InProcess)[DayColumn]));

        private static DataSet InProcess(DataSet dataSet) => dataSet;

        private static DataSet OverMessagePack(DataSet dataSet)
            => MessagePackCodec.Deserialize<GetDataResponse>(
                MessagePackCodec.Serialize(new GetDataResponse { DataSet = dataSet })).DataSet!;

        private static DataSet OverJson(DataSet dataSet)
        {
            var serializer = new JsonPayloadSerializer();
            var bytes = serializer.Serialize(new GetDataResponse { DataSet = dataSet }, typeof(GetDataResponse));
            return ((GetDataResponse)serializer.Deserialize(bytes, typeof(GetDataResponse))!).DataSet!;
        }

        private DateTime? InstantAfter(DatabaseType databaseType, Func<DataSet, DataSet> transport)
            => ValueUtilities.CDateTime(ReadAndConvert(databaseType, transport)[InstantColumn]);

        /// <summary>
        /// Stores a known instant and day, reads the record through the business object, carries it
        /// across <paramref name="transport"/>, and returns the row after the user-zone conversion.
        /// </summary>
        private DataRow ReadAndConvert(DatabaseType databaseType, Func<DataSet, DataSet> transport)
        {
            string progId = TransientForm.NewTableName("tb_tzr_");
            var schema = new FormSchema(progId, "Zone read") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "Zone read");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(InstantColumn, "Occurred At", FieldDbType.DateTime);
            table.Fields.Add(DayColumn, "Due On", FieldDbType.Date);

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                var rowId = Guid.NewGuid();
                form.DbAccess.ExecuteNonQuery(
                    $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote(InstantColumn)}, {form.Quote(DayColumn)}) " +
                    "VALUES ({0}, {1}, {2})",
                    rowId, s_storedUtc, s_storedDay);

                var loaded = new FormBusinessObject(form.CreateContext(), TestSessionFactory.CreateAccessToken(_fx), progId)
                    .GetData(new GetDataArgs { RowId = rowId }).DataSet!;
                return DateTimeZoneConverter.UtcToUser(transport(loaded), Taipei)!.Tables[progId]!.Rows[0];
            }
            finally
            {
                form.DropTables();
            }
        }
    }
}
