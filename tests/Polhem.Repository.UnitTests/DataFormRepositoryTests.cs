using System.ComponentModel;
using System.Data.Common;
using System.Reflection;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Form;

using Polhem.Tests.Shared;
namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Pure logic tests for the constructor validation of <see cref="DataFormRepository"/>,
    /// <see cref="DataFormRepository.GetNewData"/> and the private static helpers (<c>ConvertDefaultValue</c>,
    /// <c>TryCoerceToGuid</c>). No database connection is needed.
    /// </summary>
    public class DataFormRepositoryTests
    {
        #region Stubs

        private sealed class StubDefineAccess : IDefineAccess
        {
            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            public ProgramSettings GetProgramSettings() => throw new NotImplementedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public FormSchema GetFormSchema(string progId) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotImplementedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }

        private sealed class StubDbAccessFactory : IDbAccessFactory
        {
            public DbAccess Create(string databaseId) => throw new NotImplementedException();
        }

        private sealed class StubConnectionManager : IDbConnectionManager
        {
            public DbConnectionInfo GetConnectionInfo(string databaseId) => throw new NotImplementedException();
            public DbConnection CreateConnection(string databaseId) => throw new NotImplementedException();
            public bool Remove(string databaseId) => false;
            public void Clear() { }
            public bool Contains(string databaseId) => false;
            public int Count => 0;
        }

        // CA1861: the `typeof()` arrays are hoisted into static readonly fields.
        private static readonly Type[] s_convertDefaultValueParams = [typeof(string), typeof(Type)];
        private static readonly Type[] s_tryCoerceToGuidParams = [typeof(object)];

        private static DataFormRepository CreateRepository(FormSchema? schema = null)
        {
            schema ??= BuildSchema();
            return new DataFormRepository(TestRepositoryContext.Create(new StubConnectionManager(), defineAccess: new StubDefineAccess(), dbAccessFactory: new StubDbAccessFactory()), "Employee", schema, "testdb");
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("emp_name", "Name", FieldDbType.String);
            return schema;
        }

        private static MethodInfo GetPrivateStaticMethod(string name, Type[] paramTypes)
        {
            var method = typeof(DataFormRepository).GetMethod(
                name, BindingFlags.NonPublic | BindingFlags.Static, null, paramTypes, null);
            Assert.NotNull(method);
            return method!;
        }

        #endregion

        #region Constructor validation

        [Fact]
        [DisplayName("DataFormRepository constructor throws ArgumentNullException for a null ctx")]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataFormRepository(null!, "Employee", BuildSchema(), "testdb"));
        }

        [Fact]
        [DisplayName("DataFormRepository constructor throws ArgumentNullException for a null schema")]
        public void Constructor_NullSchema_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataFormRepository(TestRepositoryContext.Create(new StubConnectionManager(), defineAccess: new StubDefineAccess(), dbAccessFactory: new StubDbAccessFactory()), "Employee", null!, "testdb"));
        }

        [Fact]
        [DisplayName("DataFormRepository constructor throws ArgumentNullException for a null databaseId")]
        public void Constructor_NullDatabaseId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataFormRepository(TestRepositoryContext.Create(new StubConnectionManager(), defineAccess: new StubDefineAccess(), dbAccessFactory: new StubDbAccessFactory()), "Employee", BuildSchema(), null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("DataFormRepository constructor throws ArgumentException for a blank databaseId")]
        public void Constructor_BlankDatabaseId_ThrowsArgumentException(string databaseId)
        {
            Assert.Throws<ArgumentException>(() =>
                new DataFormRepository(TestRepositoryContext.Create(new StubConnectionManager(), defineAccess: new StubDefineAccess(), dbAccessFactory: new StubDbAccessFactory()), "Employee", BuildSchema(), databaseId));
        }

        [Fact]
        [DisplayName("With the unified signature a null progId is normalized to an empty string instead of throwing")]
        public void Constructor_NullProgId_NormalizesToEmpty()
        {
            // A repository on the registry axis always has a progId; one on the framework axis does not, and with
            // the unified signature it passes an empty string. Both share one constructor, so progId is optional.
            var repo = new DataFormRepository(TestRepositoryContext.Create(new StubConnectionManager(), defineAccess: new StubDefineAccess(), dbAccessFactory: new StubDbAccessFactory()), null!, BuildSchema(), "testdb");

            Assert.Equal(string.Empty, repo.ProgId);
        }

        [Fact]
        [DisplayName("DataFormRepository constructor sets the ProgId property")]
        public void Constructor_ValidArgs_SetsProgId()
        {
            var repo = CreateRepository();
            Assert.Equal("Employee", repo.ProgId);
        }

        #endregion

        #region GetNewData

        [Fact]
        [DisplayName("GetNewData returns a DataSet with one master row when the schema has a master table")]
        public void GetNewData_SchemaWithMasterTable_ReturnsDataSetWithOneRow()
        {
            var repo = CreateRepository();
            var dataSet = repo.GetNewData();

            Assert.NotNull(dataSet);
            Assert.Equal("Employee", dataSet.DataSetName);
            Assert.True(dataSet.Tables.Contains("Employee"));
            Assert.Equal(1, dataSet.Tables["Employee"]!.Rows.Count);
        }

        [Fact]
        [DisplayName("GetNewData gives the master row a non-empty sys_rowid Guid")]
        public void GetNewData_MasterRow_HasNonEmptyRowId()
        {
            var repo = CreateRepository();
            var dataSet = repo.GetNewData();
            var masterRow = dataSet.Tables["Employee"]!.Rows[0];
            var rowId = (Guid)masterRow[SysFields.RowId];
            Assert.NotEqual(Guid.Empty, rowId);
        }

        [Theory]
        [InlineData("Pacific/Kiritimati")]
        [InlineData("Pacific/Pago_Pago")]
        [DisplayName("GetNewData defaults a new row's DateTime to the current UTC time and its Date to today in the user's time zone")]
        public void GetNewData_TimeDefaults_DateTimeIsUtcAndDateIsUserDay(string timeZoneId)
        {
            // The server-side DataSet is in UTC (ADR-032 D3), and the Connector converts the response as UTC.
            // The skeleton table is built with `AddColumn`. If a column carried a clock default taken when the column
            // was created, `FormRowDefaults` would be skipped.
            // UTC+14 and UTC-11: at any moment at least one of the two zones has a "today" different from UTC, so
            // the Date assertion cannot pass vacuously on a day that happens to match. For DateTime, both zones
            // differ from UTC by more than ten hours.
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            master.Fields.Add("created_at", "Created At", FieldDbType.DateTime);
            var repo = CreateRepository(schema);
            var utcBefore = DateTime.UtcNow;

            var dataSet = repo.GetNewData(timeZoneId);

            var utcAfter = DateTime.UtcNow;
            var masterRow = dataSet.Tables["Employee"]!.Rows[0];
            Assert.InRange((DateTime)masterRow["created_at"], utcBefore, utcAfter);
            Assert.Equal(FrameworkClock.Today(timeZoneId).ToDateTime(TimeOnly.MinValue), (DateTime)masterRow["hire_date"]);
        }

        [Fact]
        [DisplayName("GetNewData throws InvalidOperationException when the schema has no master table")]
        public void GetNewData_SchemaWithoutMasterTable_ThrowsInvalidOperationException()
        {
            var schema = new FormSchema("NoMaster", "NoMaster");
            var repo = CreateRepository(schema);
            Assert.Throws<InvalidOperationException>(() => repo.GetNewData());
        }

        [Fact]
        [DisplayName("GetNewData seeds non-null values into the master row from the schema (including a Short column without a default)")]
        public void GetNewData_MasterRow_SeedsNonNullDefaults()
        {
            // The master table name must match CreateRepository's progId ("Employee").
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("title", "Title", FieldDbType.String);
            master.Fields.Add("seq", "Seq", FieldDbType.Short);   // DBNull column default → seeded
            var repo = CreateRepository(schema);

            var row = repo.GetNewData().Tables["Employee"]!.Rows[0];

            Assert.NotEqual(Guid.Empty, (Guid)row[SysFields.RowId]);
            Assert.Equal(string.Empty, row["title"]);
            Assert.NotEqual(DBNull.Value, row["seq"]);
            Assert.Equal((short)0, row["seq"]);
        }

        #endregion

        #region ConvertDefaultValue (private static method)

        [Theory]
        [InlineData("hello world")]
        [InlineData("")]
        [DisplayName("ConvertDefaultValue returns the raw string for the string type")]
        public void ConvertDefaultValue_StringType_ReturnsRawString(string rawValue)
        {
            var method = GetPrivateStaticMethod("ConvertDefaultValue", s_convertDefaultValueParams);
            // `rawValue` is a variable, which avoids CA1861 (an array with a variable element does not trigger it).
            var result = method.Invoke(null, new object[] { rawValue, typeof(string) });
            Assert.Equal(rawValue, result);
        }

        [Fact]
        [DisplayName("ConvertDefaultValue parses a valid Guid string for the Guid type")]
        public void ConvertDefaultValue_GuidType_ValidString_ReturnsParsedGuid()
        {
            var method = GetPrivateStaticMethod("ConvertDefaultValue", s_convertDefaultValueParams);
            var expected = Guid.NewGuid();
            // `expected.ToString()` is not a constant, which avoids CA1861.
            var result = method.Invoke(null, new object[] { expected.ToString(), typeof(Guid) });
            Assert.Equal(expected, (Guid)result!);
        }

        [Theory]
        [InlineData("not-a-guid")]
        [InlineData("12345")]
        [DisplayName("ConvertDefaultValue returns Guid.Empty for an invalid string with the Guid type")]
        public void ConvertDefaultValue_GuidType_InvalidString_ReturnsGuidEmpty(string invalidGuid)
        {
            var method = GetPrivateStaticMethod("ConvertDefaultValue", s_convertDefaultValueParams);
            var result = method.Invoke(null, new object[] { invalidGuid, typeof(Guid) });
            Assert.Equal(Guid.Empty, (Guid)result!);
        }

        [Theory]
        [InlineData("42", 42)]
        [InlineData("0", 0)]
        [DisplayName("ConvertDefaultValue converts a valid numeric string for the int type")]
        public void ConvertDefaultValue_IntType_ValidString_ReturnsInt(string raw, int expected)
        {
            var method = GetPrivateStaticMethod("ConvertDefaultValue", s_convertDefaultValueParams);
            var result = method.Invoke(null, new object[] { raw, typeof(int) });
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("not-a-number")]
        [InlineData("abc")]
        [DisplayName("ConvertDefaultValue returns DBNull.Value for an invalid string with the int type")]
        public void ConvertDefaultValue_IntType_InvalidString_ReturnsDBNull(string invalidInt)
        {
            var method = GetPrivateStaticMethod("ConvertDefaultValue", s_convertDefaultValueParams);
            var result = method.Invoke(null, new object[] { invalidInt, typeof(int) });
            Assert.Same(DBNull.Value, result);
        }

        #endregion

        #region TryCoerceToGuid (private static method)

        [Fact]
        [DisplayName("TryCoerceToGuid returns the same Guid for a Guid input")]
        public void TryCoerceToGuid_GuidInput_ReturnsSameGuid()
        {
            var method = GetPrivateStaticMethod("TryCoerceToGuid", s_tryCoerceToGuidParams);
            var expected = Guid.NewGuid();
            // `expected` is a variable, so CA1861 does not trigger.
            var result = method.Invoke(null, new object?[] { expected });
            Assert.Equal(expected, (Guid)result!);
        }

        [Fact]
        [DisplayName("TryCoerceToGuid parses a valid Guid string and returns the Guid")]
        public void TryCoerceToGuid_ValidGuidString_ReturnsParsedGuid()
        {
            var method = GetPrivateStaticMethod("TryCoerceToGuid", s_tryCoerceToGuidParams);
            var expected = Guid.NewGuid();
            // `expected.ToString()` is not a constant, so CA1861 does not trigger.
            var result = method.Invoke(null, new object?[] { expected.ToString() });
            Assert.Equal(expected, (Guid)result!);
        }

        [Theory]
        [InlineData("not-a-guid")]
        [InlineData("12345")]
        [DisplayName("TryCoerceToGuid returns null for an invalid string")]
        public void TryCoerceToGuid_InvalidString_ReturnsNull(string invalidGuid)
        {
            var method = GetPrivateStaticMethod("TryCoerceToGuid", s_tryCoerceToGuidParams);
            var result = method.Invoke(null, new object?[] { invalidGuid });
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("TryCoerceToGuid returns null for a null input")]
        public void TryCoerceToGuid_NullInput_ReturnsNull()
        {
            var method = GetPrivateStaticMethod("TryCoerceToGuid", s_tryCoerceToGuidParams);
            // The local `nullValue` keeps the array literal from triggering CA1861.
            object? nullValue = null;
            var result = method.Invoke(null, new object?[] { nullValue });
            Assert.Null(result);
        }

        #endregion
    }
}
