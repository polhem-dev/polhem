using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableSchemaBuilderTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public TableSchemaBuilderTests(SharedDbFixture fx) { _fx = fx; }
        private TableSchemaBuilder NewBuilder(string databaseId)
            => new(databaseId, _fx.GetRequiredService<IDefineAccess>(), _fx.GetRequiredService<IDbConnectionManager>());

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("TableSchemaBuilder returns None for a table whose structure matches")]
        public void Compare_UpToDateTable_ReturnsNoneAction()
        {
            var builder = NewBuilder("common_sqlserver");
            var result = builder.Compare("common", "st_user");

            Assert.NotNull(result);
            Assert.Equal(DbUpgradeAction.None, result.UpgradeAction);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("TableSchemaBuilder returns an empty command text when the structure is in sync")]
        public void GetCommandText_UpToDateTable_ReturnsEmpty()
        {
            var builder = NewBuilder("common_sqlserver");
            string sql = builder.GetCommandText("common", "st_user");

            Assert.Equal(string.Empty, sql);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("TableSchemaBuilder Execute returns false when the structure is in sync")]
        public void Execute_UpToDateTable_ReturnsFalse()
        {
            var builder = NewBuilder("common_sqlserver");
            bool upgraded = builder.Execute("common", "st_user");

            Assert.False(upgraded);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("TableSchemaBuilder GetCommandText returns an empty string for the ft_project already created in the company category")]
        public void GetCommandText_CompanyCategoryUpToDate_SqlServer_ReturnsEmpty()
        {
            // `SharedDbFixture` has already created ft_project of the company category on SQL Server through
            // `SharedDatabaseState`, so the diff is empty.
            var builder = NewBuilder(TestDbConventions.GetDatabaseId(DatabaseType.SQLServer, "company"));
            string sql = builder.GetCommandText("company", "ft_project");
            Assert.Equal(string.Empty, sql);
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("TableSchemaBuilder GetCommandText on PostgreSQL returns an empty string for the ft_project already created in the company category")]
        public void GetCommandText_CompanyCategoryUpToDate_PostgreSql_ReturnsEmpty()
        {
            var builder = NewBuilder(TestDbConventions.GetDatabaseId(DatabaseType.PostgreSQL, "company"));
            string sql = builder.GetCommandText("company", "ft_project");
            Assert.Equal(string.Empty, sql);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("TableSchemaBuilder GetCommandText returns non-empty ALTER SQL when a field differs (SQL Server)")]
        public void GetCommandText_OutOfSyncTable_SqlServer_ReturnsNonEmptyAlterSql()
        {
            var cm = _fx.GetRequiredService<IDbConnectionManager>();
            var builder = new TableSchemaBuilder("common_sqlserver", new ExtraColumnDefineAccess(_fx.GetRequiredService<IDefineAccess>()), cm);

            string sql = builder.GetCommandText("common", "st_user");

            Assert.NotEmpty(sql);
            Assert.Contains("zz_test_col", sql, StringComparison.OrdinalIgnoreCase);
        }

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("TableSchemaBuilder GetCommandText returns non-empty ALTER SQL when a field differs (PostgreSQL)")]
        public void GetCommandText_OutOfSyncTable_PostgreSql_ReturnsNonEmptyAlterSql()
        {
            var cm = _fx.GetRequiredService<IDbConnectionManager>();
            var builder = new TableSchemaBuilder(TestDbConventions.GetDatabaseId(DatabaseType.PostgreSQL), new ExtraColumnDefineAccess(_fx.GetRequiredService<IDefineAccess>()), cm);

            string sql = builder.GetCommandText("common", "st_user");

            Assert.NotEmpty(sql);
            Assert.Contains("zz_test_col", sql, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ExtraColumnDefineAccess : IDefineAccess
        {
            private readonly IDefineAccess _inner;
            public ExtraColumnDefineAccess(IDefineAccess inner) => _inner = inner;

            public TableSchema GetTableSchema(string categoryId, string tableName)
            {
                var schema = _inner.GetTableSchema(categoryId, tableName).Clone();
                schema.Fields!.Add("zz_test_col", "Test Column", FieldDbType.String, 10);
                return schema;
            }

            public object GetDefine(DefineType defineType, string[]? keys = null) => _inner.GetDefine(defineType, keys);
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => _inner.SaveDefine(defineType, defineObject, keys);
            public SystemSettings GetSystemSettings() => _inner.GetSystemSettings();
            public void SaveSystemSettings(SystemSettings settings) => _inner.SaveSystemSettings(settings);
            public DatabaseSettings GetDatabaseSettings() => _inner.GetDatabaseSettings();
            public void SaveDatabaseSettings(DatabaseSettings settings) => _inner.SaveDatabaseSettings(settings);
            public ProgramSettings GetProgramSettings() => _inner.GetProgramSettings();
            public void SaveProgramSettings(ProgramSettings settings) => _inner.SaveProgramSettings(settings);
            public DbCategorySettings GetDbCategorySettings() => _inner.GetDbCategorySettings();
            public void SaveDbCategorySettings(DbCategorySettings settings) => _inner.SaveDbCategorySettings(settings);
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => _inner.SaveTableSchema(categoryId, tableSchema);
            public FormSchema GetFormSchema(string progId) => _inner.GetFormSchema(progId);
            public void SaveFormSchema(FormSchema formSchema) => _inner.SaveFormSchema(formSchema);
            public FormLayout GetFormLayout(string layoutId) => _inner.GetFormLayout(layoutId);
            public void SaveFormLayout(FormLayout formLayout) => _inner.SaveFormLayout(formLayout);
            public LanguageResource GetLanguage(string lang, string ns) => _inner.GetLanguage(lang, ns);
            public void SaveLanguage(LanguageResource resource) => _inner.SaveLanguage(resource);
        }
    }
}
