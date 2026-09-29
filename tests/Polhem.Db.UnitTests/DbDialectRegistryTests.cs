using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Manager;
using Polhem.Db.Providers.SqlServer;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class DbDialectRegistryTests : IClassFixture<SharedDbFixture>
    {
        public DbDialectRegistryTests(SharedDbFixture _) { }

        [Fact]
        [DisplayName("Register and Get return the matching factory")]
        public void RegisterAndGet_ReturnsSameFactory()
        {
            var factory = new SqlDialectFactory();
            DbDialectRegistry.Register(DatabaseType.SQLServer, factory);

            Assert.Same(factory, DbDialectRegistry.Get(DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("IsRegistered returns true once registered")]
        public void IsRegistered_Registered_ReturnsTrue()
        {
            DbDialectRegistry.Register(DatabaseType.SQLServer, new SqlDialectFactory());

            Assert.True(DbDialectRegistry.IsRegistered(DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("Register throws ArgumentNullException for null")]
        public void Register_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DbDialectRegistry.Register(DatabaseType.SQLServer, null!));
        }

        [Fact]
        [DisplayName("Get throws KeyNotFoundException for an unregistered type")]
        public void Get_Unregistered_Throws()
        {
            // `SharedDatabaseState.EnsureRegistered` registers every defined `DatabaseType`, so no enum value is naturally unregistered.
            // An integer outside the enum range serves as a placeholder that is never registered.
            Assert.Throws<KeyNotFoundException>(() => DbDialectRegistry.Get((DatabaseType)9999));
        }
    }

    public class SqlDialectFactoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SqlDialectFactoryTests(SharedDbFixture fx) { _fx = fx; }

        private readonly SqlDialectFactory _factory = new();

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("CreateTableSchemaProvider returns a SqlTableSchemaProvider")]
        public void CreateTableSchemaProvider_ReturnsSqlImpl()
        {
            // The provider's constructor builds a `DbAccess` for `common_sqlserver` through the injected
            // `IDbConnectionManager`, which knows that database only when POLHEM_TEST_CONNSTR_SQLSERVER is set.
            // Without it `[DbFact]` skips the test.
            var provider = _factory.CreateTableSchemaProvider("common_sqlserver", _fx.GetRequiredService<IDbConnectionManager>());

            Assert.IsType<SqlTableSchemaProvider>(provider);
            Assert.Equal("common_sqlserver", provider.DatabaseId);
        }

        [Fact]
        [DisplayName("CreateCreateTableCommandBuilder returns a SqlCreateTableCommandBuilder")]
        public void CreateCreateTableCommandBuilder_ReturnsSqlImpl()
        {
            Assert.IsType<SqlCreateTableCommandBuilder>(_factory.CreateCreateTableCommandBuilder());
        }

        [Fact]
        [DisplayName("CreateTableAlterCommandBuilder returns a SqlTableAlterCommandBuilder")]
        public void CreateTableAlterCommandBuilder_ReturnsSqlImpl()
        {
            Assert.IsType<SqlTableAlterCommandBuilder>(_factory.CreateTableAlterCommandBuilder());
        }

        [Fact]
        [DisplayName("CreateTableRebuildCommandBuilder returns an ITableRebuildCommandBuilder")]
        public void CreateTableRebuildCommandBuilder_ImplementsInterface()
        {
            var rebuildBuilder = _factory.CreateTableRebuildCommandBuilder();

            Assert.IsType<ITableRebuildCommandBuilder>(rebuildBuilder, exactMatch: false);
        }

[Fact]
        [DisplayName("GetDefaultValueExpression returns SQL Server specific defaults (such as getdate and newid)")]
        public void GetDefaultValueExpression_SqlServerDefaults()
        {
            Assert.Equal("getutcdate()", _factory.GetDefaultValueExpression(FieldDbType.DateTime));
            Assert.Equal("newid()", _factory.GetDefaultValueExpression(FieldDbType.Guid));
            Assert.Equal("0", _factory.GetDefaultValueExpression(FieldDbType.Integer));
            Assert.Equal(string.Empty, _factory.GetDefaultValueExpression(FieldDbType.String));
        }

        [Fact]
        [DisplayName("The instance returned by CreateTableRebuildCommandBuilder handles a diff (smoke test)")]
        public void CreateTableRebuildCommandBuilder_CanProduceSql()
        {
            var define = new TableSchema { TableName = "st_sample" };
            define.Fields!.Add("id", "Id", FieldDbType.Guid);
            var real = new TableSchema { TableName = "st_sample" };
            real.Fields!.Add("id", "Id", FieldDbType.Guid);
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();
            // Force one change so that the rebuild produces non-empty SQL.
            diff.ChangeList.Add(new AddFieldChange(new DbField("note", "Note", FieldDbType.String) { Length = 10 }));

            var builder = _factory.CreateTableRebuildCommandBuilder();
            var sql = builder.GetCommandText(diff);

            Assert.Contains("tmp_st_sample", sql);
        }

        [Fact]
        [DisplayName("CreateFormCommandBuilder returns a SqlFormCommandBuilder")]
        public void CreateFormCommandBuilder_ReturnsSqlImpl()
        {
            var schema = new FormSchema("Foo", "Foo");
            var defineAccess = _fx.GetRequiredService<IDefineAccess>();

            var builder = _factory.CreateFormCommandBuilder(schema, defineAccess);

            Assert.IsType<SqlFormCommandBuilder>(builder);
        }
    }
}
