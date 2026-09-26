using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class DbCommandSpecTests : IClassFixture<SharedDbFixture>
    {
        public DbCommandSpecTests(SharedDbFixture _) { }

        #region Constructors

        [Fact]
        [DisplayName("The default constructor creates an empty command spec")]
        public void DefaultConstructor_CreatesEmptySpec()
        {
            var spec = new DbCommandSpec();

            Assert.Equal(DbCommandKind.NonQuery, spec.Kind);
            Assert.Equal(string.Empty, spec.CommandText);
            Assert.Equal(CommandType.Text, spec.CommandType);
            Assert.Equal(30, spec.CommandTimeout);
            Assert.NotNull(spec.Parameters);
            Assert.Empty(spec.Parameters);
        }

        [Fact]
        [DisplayName("The positional constructor adds parameters named p0 and p1 in order")]
        public void PositionalConstructor_AddsP0P1Parameters()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE A = {0} AND B = {1}", "x", 123);

            Assert.Equal(DbCommandKind.Scalar, spec.Kind);
            Assert.Equal("SELECT * FROM T WHERE A = {0} AND B = {1}", spec.CommandText);
            Assert.Equal(2, spec.Parameters.Count);
            Assert.Equal("p0", spec.Parameters[0].Name);
            Assert.Equal("x", spec.Parameters[0].Value);
            Assert.Equal("p1", spec.Parameters[1].Name);
            Assert.Equal(123, spec.Parameters[1].Value);
        }

        [Fact]
        [DisplayName("The positional constructor creates no parameters when no values are given")]
        public void PositionalConstructor_NoValues_CreatesNoParameters()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "SELECT 1");

            Assert.Empty(spec.Parameters);
        }

        [Fact]
        [DisplayName("The named constructor adds the dictionary entries to Parameters")]
        public void NamedConstructor_AddsParameters()
        {
            var dict = new Dictionary<string, object>
            {
                { "Id", 1 },
                { "Name", "Polhem" }
            };

            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE Id = {Id} AND Name = {Name}", dict);

            Assert.Equal(2, spec.Parameters.Count);
            Assert.Equal(1, spec.Parameters["Id"].Value);
            Assert.Equal("Polhem", spec.Parameters["Name"].Value);
        }

        [Fact]
        [DisplayName("The named constructor creates empty Parameters for a null dictionary")]
        public void NamedConstructor_NullDictionary_CreatesNoParameters()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "SELECT 1", parameters: null);

            Assert.Empty(spec.Parameters);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("The positional constructor throws ArgumentNullException for an empty commandText")]
        public void PositionalConstructor_EmptyCommandText_Throws(string? commandText)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DbCommandSpec(DbCommandKind.NonQuery, commandText!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("The named constructor throws ArgumentNullException for an empty commandText")]
        public void NamedConstructor_EmptyCommandText_Throws(string? commandText)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DbCommandSpec(DbCommandKind.NonQuery, commandText!, new Dictionary<string, object>()));
        }

        #endregion

        #region Placeholder resolution (verified through CreateCommand)

        [Fact]
        [DisplayName("CreateCommand resolves the positional placeholder {0} to a prefixed parameter name")]
        public void CreateCommand_PositionalPlaceholder_ResolvesToPrefixedName()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE A = {0} AND B = {1}", "x", 1);

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal("SELECT * FROM T WHERE A = @p0 AND B = @p1", cmd.CommandText);
            Assert.Equal(2, cmd.Parameters.Count);
            Assert.Equal("@p0", cmd.Parameters[0].ParameterName);
            Assert.Equal("x", cmd.Parameters[0].Value);
            Assert.Equal("@p1", cmd.Parameters[1].ParameterName);
            Assert.Equal(1, cmd.Parameters[1].Value);
        }

        [Fact]
        [DisplayName("CreateCommand resolves the named placeholder {Name} to a prefixed parameter name")]
        public void CreateCommand_NamedPlaceholder_ResolvesToPrefixedName()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE Id = {Id}",
                new Dictionary<string, object> { { "Id", 99 } });

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal("SELECT * FROM T WHERE Id = @Id", cmd.CommandText);
            Assert.Single(cmd.Parameters);
            Assert.Equal("@Id", cmd.Parameters[0].ParameterName);
        }

        [Fact]
        [DisplayName("Named placeholders match case-insensitively")]
        public void CreateCommand_NamedPlaceholder_CaseInsensitive()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE Id = {ID}",
                new Dictionary<string, object> { { "id", 1 } });

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal("SELECT * FROM T WHERE Id = @id", cmd.CommandText);
        }

        [Fact]
        [DisplayName("{@Parameters} expands to all parameter placeholders separated by commas")]
        public void CreateCommand_AtParametersToken_ExpandsToCommaList()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery,
                "EXEC sp_test {@Parameters}", "a", "b", "c");

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal("EXEC sp_test @p0,@p1,@p2", cmd.CommandText);
            Assert.Equal(3, cmd.Parameters.Count);
        }

        [Fact]
        [DisplayName("CreateCommand does not resolve placeholders in the CommandText of a StoredProcedure")]
        public void CreateCommand_StoredProcedure_SkipsPlaceholderResolution()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "sp_GetUser")
            {
                CommandType = CommandType.StoredProcedure
            };
            spec.Parameters.Add("UserId", 42);

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal("sp_GetUser", cmd.CommandText);
            Assert.Equal(CommandType.StoredProcedure, cmd.CommandType);
            Assert.Single(cmd.Parameters);
            Assert.Equal("@UserId", cmd.Parameters[0].ParameterName);
        }

        [Fact]
        [DisplayName("CreateCommand passes CommandTimeout to the DbCommand")]
        public void CreateCommand_AppliesCommandTimeout()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "SELECT 1") { CommandTimeout = 45 };

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal(45, cmd.CommandTimeout);
        }

        [Fact]
        [DisplayName("CreateCommand binds a null parameter value as DBNull")]
        public void CreateCommand_NullValue_BindsDBNull()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery,
                "UPDATE T SET A = {0}", new object[] { null! });

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal(DBNull.Value, cmd.Parameters[0].Value);
        }

        [Fact]
        [DisplayName("CreateCommand does not add the prefix again when the parameter name already has it")]
        public void CreateCommand_NameWithPrefix_NotDuplicated()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "SELECT 1");
            spec.Parameters.Add("@X", 1);

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            Assert.Equal("@X", cmd.Parameters[0].ParameterName);
        }

        [Fact]
        [DisplayName("CreateCommand passes DbType, Size, SourceColumn and SourceVersion to the DbParameter")]
        public void CreateCommand_PropagatesParameterMetadata()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "UPDATE T SET A = {0}", "x");
            var p = spec.Parameters[0];
            p.Size = 50;
            p.SourceColumn = "A";
            p.SourceVersion = DataRowVersion.Original;
            p.IsNullable = true;

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn);

            var dp = cmd.Parameters[0];
            Assert.Equal(DbType.String, dp.DbType);
            Assert.Equal(50, dp.Size);
            Assert.Equal("A", dp.SourceColumn);
            Assert.Equal(DataRowVersion.Original, dp.SourceVersion);
            Assert.True(dp.IsNullable);
        }

        [Fact]
        [DisplayName("CreateCommand throws ArgumentNullException for a null connection")]
        public void CreateCommand_NullConnection_Throws()
        {
            var spec = new DbCommandSpec(DbCommandKind.NonQuery, "SELECT 1");

            Assert.Throws<ArgumentNullException>(() =>
                spec.CreateCommand(DatabaseType.SQLServer, null!));
        }

        [Fact]
        [DisplayName("CreateCommand throws InvalidOperationException for an empty CommandText")]
        public void CreateCommand_EmptyCommandText_Throws()
        {
            var spec = new DbCommandSpec();

            using var conn = new SqlConnection();
            Assert.Throws<InvalidOperationException>(() =>
                spec.CreateCommand(DatabaseType.SQLServer, conn));
        }

        [Fact]
        [DisplayName("CreateCommand throws InvalidOperationException for a positional placeholder index out of range")]
        public void CreateCommand_IndexOutOfRange_Throws()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE A = {0} AND B = {5}", "x");

            using var conn = new SqlConnection();
            Assert.Throws<InvalidOperationException>(() =>
                spec.CreateCommand(DatabaseType.SQLServer, conn));
        }

        [Fact]
        [DisplayName("CreateCommand throws InvalidOperationException when a named placeholder has no matching parameter")]
        public void CreateCommand_UnknownNamedKey_Throws()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE Id = {Missing}",
                new Dictionary<string, object> { { "Id", 1 } });

            using var conn = new SqlConnection();
            Assert.Throws<InvalidOperationException>(() =>
                spec.CreateCommand(DatabaseType.SQLServer, conn));
        }

        [Fact]
        [DisplayName("CreateCommand uses the colon parameter prefix for Oracle")]
        public void CreateCommand_Oracle_UsesColonPrefix()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT * FROM T WHERE A = {0}", "x");

            using var conn = new SqlConnection();
            using var cmd = spec.CreateCommand(DatabaseType.Oracle, conn);

            Assert.Equal("SELECT * FROM T WHERE A = :p0", cmd.CommandText);
            Assert.Equal(":p0", cmd.Parameters[0].ParameterName);
        }

        #endregion

        #region ToString

        [Fact]
        [DisplayName("ToString returns the CommandText")]
        public void ToString_ReturnsCommandText()
        {
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1");
            Assert.Equal("SELECT 1", spec.ToString());
        }

        #endregion

        #region Edge-case branches

        [Fact]
        [DisplayName("CreateCommand throws InvalidOperationException when a named placeholder's parameter name is blank")]
        public void CreateCommand_NamedKeyWithBlankName_Throws()
        {
            // Add with a valid name, then blank the Name, to reach the blank-name check of `ResolveNamedKey`.
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT * FROM T WHERE X = {X}");
            spec.Parameters.Add("X", 1);
            spec.Parameters[0].Name = "   ";

            using var conn = new SqlConnection();
            Assert.Throws<InvalidOperationException>(() =>
                spec.CreateCommand(DatabaseType.SQLServer, conn));
        }

        [Fact]
        [DisplayName("CreateCommand throws InvalidOperationException when the parameter name behind a positional placeholder is blank")]
        public void CreateCommand_NumericKeyWithBlankName_Throws()
        {
            // Add with a valid name, then blank the Name, to reach the blank-name check of `ResolveNumericKey`.
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT * FROM T WHERE X = {0}");
            spec.Parameters.Add("p0", 1);
            spec.Parameters[0].Name = "   ";

            using var conn = new SqlConnection();
            Assert.Throws<InvalidOperationException>(() =>
                spec.CreateCommand(DatabaseType.SQLServer, conn));
        }

        #endregion

        #region NormalizeParameterValue (Oracle Guid → byte[] conversion)

        [Fact]
        [DisplayName("NormalizeParameterValue converts a Guid to a 16-byte byte[] on Oracle")]
        public void NormalizeParameterValue_OracleGuid_ConvertedToByteArray()
        {
            var guid = Guid.Parse("12345678-1234-5678-90ab-cdef12345678");

            object? result = DbCommandSpec.NormalizeParameterValue(DatabaseType.Oracle, guid);

            // `Oracle.ManagedDataAccess.OracleParameter` does not accept a Guid as a RAW(16) binding value (it throws
            // "ArgumentException: Value does not fall within the expected range"), so the framework must convert it to
            // a byte[]. The bytes must equal `guid.ToByteArray()`.
            var bytes = Assert.IsType<byte[]>(result);
            Assert.Equal(16, bytes.Length);
            Assert.Equal(guid.ToByteArray(), bytes);
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer)]
        [InlineData(DatabaseType.PostgreSQL)]
        [InlineData(DatabaseType.MySQL)]
        [InlineData(DatabaseType.SQLite)]
        [DisplayName("NormalizeParameterValue returns a Guid unchanged on databases other than Oracle")]
        public void NormalizeParameterValue_NonOracleGuid_PassThrough(DatabaseType dbType)
        {
            var guid = Guid.NewGuid();

            object? result = DbCommandSpec.NormalizeParameterValue(dbType, guid);

            Assert.Equal(guid, result);
        }

        [Fact]
        [DisplayName("NormalizeParameterValue returns non-Guid values (string, int, null) unchanged on Oracle")]
        public void NormalizeParameterValue_OracleNonGuid_PassThrough()
        {
            Assert.Equal("hello", DbCommandSpec.NormalizeParameterValue(DatabaseType.Oracle, "hello"));
            Assert.Equal(42, DbCommandSpec.NormalizeParameterValue(DatabaseType.Oracle, 42));
            Assert.Null(DbCommandSpec.NormalizeParameterValue(DatabaseType.Oracle, null));
        }

        [Fact]
        [DisplayName("NormalizeDbType rewrites DbType.Guid to DbType.Binary on Oracle")]
        public void NormalizeDbType_OracleGuid_RewrittenToBinary()
        {
            // `OracleParameter.DbType` rejects `DbType.Guid`: Oracle has no native UUID type, and the framework maps it
            // to RAW(16), which is `DbType.Binary`.
            Assert.Equal(DbType.Binary, DbCommandSpec.NormalizeDbType(DatabaseType.Oracle, DbType.Guid));
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer)]
        [InlineData(DatabaseType.PostgreSQL)]
        [InlineData(DatabaseType.MySQL)]
        [InlineData(DatabaseType.SQLite)]
        [DisplayName("NormalizeDbType returns DbType.Guid unchanged on databases other than Oracle")]
        public void NormalizeDbType_NonOracleGuid_PassThrough(DatabaseType dbType)
        {
            Assert.Equal(DbType.Guid, DbCommandSpec.NormalizeDbType(dbType, DbType.Guid));
        }

        [Theory]
        [InlineData(DbType.String)]
        [InlineData(DbType.Int32)]
        [InlineData(DbType.Binary)]
        [InlineData(DbType.DateTime)]
        [DisplayName("NormalizeDbType returns non-Guid DbTypes unchanged on Oracle")]
        public void NormalizeDbType_OracleNonGuid_PassThrough(DbType dbType)
        {
            Assert.Equal(dbType, DbCommandSpec.NormalizeDbType(DatabaseType.Oracle, dbType));
        }

        [Fact]
        [DisplayName("NormalizeDbType rewrites DbType.DateTime to DbType.DateTime2 on SQL Server")]
        public void NormalizeDbType_SqlServerDateTime_RewrittenToDateTime2()
        {
            // datetime2 keeps the full range and the 100 ns precision of a .NET DateTime. `DbType.DateTime` rounds to
            // milliseconds at the parameter layer and throws `SqlDateTimeOverflow` before 1753.
            Assert.Equal(DbType.DateTime2, DbCommandSpec.NormalizeDbType(DatabaseType.SQLServer, DbType.DateTime));
        }

        [Theory]
        [InlineData(DatabaseType.MySQL)]
        [InlineData(DatabaseType.SQLite)]
        [InlineData(DatabaseType.Oracle)]
        [DisplayName("NormalizeDbType returns DbType.DateTime unchanged on MySQL, SQLite and Oracle (guards against cross-provider regressions)")]
        public void NormalizeDbType_UnaffectedProvidersDateTime_PassThrough(DatabaseType dbType)
        {
            Assert.Equal(DbType.DateTime, DbCommandSpec.NormalizeDbType(dbType, DbType.DateTime));
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer)]
        [InlineData(DatabaseType.PostgreSQL)]
        [DisplayName("NormalizeDbType promotes DbType.DateTime to DateTime2 on SQL Server and PostgreSQL")]
        public void NormalizeDbType_DateTime_UpgradedToDateTime2(DatabaseType dbType)
        {
            // The two promote for different reasons with the same technique: SQL Server for precision
            // (datetime → datetime2(7)), PostgreSQL to avoid the implicit time zone conversion of timestamptz
            // (ADR-032 D1).
            Assert.Equal(DbType.DateTime2, DbCommandSpec.NormalizeDbType(dbType, DbType.DateTime));
        }

        [Theory]
        [InlineData(DbType.String)]
        [InlineData(DbType.Int32)]
        [InlineData(DbType.Guid)]
        [InlineData(DbType.Decimal)]
        [DisplayName("NormalizeDbType returns non-DateTime DbTypes unchanged on SQL Server")]
        public void NormalizeDbType_SqlServerNonDateTime_PassThrough(DbType dbType)
        {
            Assert.Equal(dbType, DbCommandSpec.NormalizeDbType(DatabaseType.SQLServer, dbType));
        }

        #endregion
    }
}
