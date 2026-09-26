using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Tests of the <see cref="CacheDefineAccess"/> read paths. The shared instance is resolved from the fixture's
    /// DI container (path = <c>tests/Define</c>). The cache takes <see cref="PathOptions"/> by injection instead of
    /// reading process-wide static state.
    /// </summary>
    public class CacheDefineAccessTests : IClassFixture<PolhemTestFixture>
    {
        private static readonly string[] s_tableSchemaKeys = { "common", "st_user" };
        private static readonly string[] s_formSchemaKeys = { "Department" };

        private readonly IDefineAccess _access;

        public CacheDefineAccessTests(PolhemTestFixture fx)
        {
            _access = fx.GetRequiredService<IDefineAccess>();
        }

        [Fact]
        [DisplayName("GetDefine(SystemSettings) returns a SystemSettings instance")]
        public void GetDefine_SystemSettings_ReturnsSystemSettings()
        {
            var result = _access.GetDefine(DefineType.SystemSettings);
            Assert.IsType<SystemSettings>(result);
        }

        [Fact]
        [DisplayName("GetDefine(DatabaseSettings) returns a DatabaseSettings instance")]
        public void GetDefine_DatabaseSettings_ReturnsDatabaseSettings()
        {
            var result = _access.GetDefine(DefineType.DatabaseSettings);
            Assert.IsType<DatabaseSettings>(result);
        }

        [Fact]
        [DisplayName("GetDefine(DbCategorySettings) returns a DbCategorySettings instance")]
        public void GetDefine_DbCategorySettings_ReturnsDbCategorySettings()
        {
            var result = _access.GetDefine(DefineType.DbCategorySettings);
            Assert.IsType<DbCategorySettings>(result);
        }

        [Fact]
        [DisplayName("GetDefine(TableSchema) with two keys returns the matching TableSchema")]
        public void GetDefine_TableSchema_WithCorrectKeys_ReturnsTableSchema()
        {
            var result = _access.GetDefine(DefineType.TableSchema, s_tableSchemaKeys);
            var schema = Assert.IsType<TableSchema>(result);
            Assert.Equal("st_user", schema.TableName, ignoreCase: true);
        }

        [Fact]
        [DisplayName("GetDefine(FormSchema) with a single key returns the matching FormSchema")]
        public void GetDefine_FormSchema_WithCorrectKey_ReturnsFormSchema()
        {
            var result = _access.GetDefine(DefineType.FormSchema, s_formSchemaKeys);
            var schema = Assert.IsType<FormSchema>(result);
            Assert.Equal("Department", schema.ProgId);
        }

        [Theory]
        [InlineData(DefineType.TableSchema, null)]
        [InlineData(DefineType.TableSchema, new string[] { "only-one" })]
        [InlineData(DefineType.FormSchema, null)]
        [InlineData(DefineType.FormSchema, new string[] { "a", "b" })]
        [InlineData(DefineType.FormLayout, null)]
        [DisplayName("GetDefine throws ArgumentException when the number of keys does not match the type")]
        public void GetDefine_InvalidKeys_Throws(DefineType defineType, string[]? keys)
        {
            Assert.Throws<ArgumentException>(() => _access.GetDefine(defineType, keys));
        }

        [Fact]
        [DisplayName("GetDefine throws NotSupportedException for an unsupported DefineType")]
        public void GetDefine_UnsupportedType_Throws()
        {
            Assert.Throws<NotSupportedException>(() => _access.GetDefine((DefineType)999));
        }

        [Fact]
        [DisplayName("SaveDefine throws NotSupportedException for an unsupported DefineType")]
        public void SaveDefine_UnsupportedType_Throws()
        {
            Assert.Throws<NotSupportedException>(() =>
                _access.SaveDefine((DefineType)999, new object()));
        }

        [Fact]
        [DisplayName("SaveDefine(TableSchema) throws ArgumentException for invalid keys")]
        public void SaveDefine_TableSchema_InvalidKeys_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                _access.SaveDefine(DefineType.TableSchema, new TableSchema(), null));
        }

        [Fact]
        [DisplayName("GetSystemSettings returns an instance")]
        public void GetSystemSettings_ReturnsInstance() => Assert.NotNull(_access.GetSystemSettings());

        [Fact]
        [DisplayName("GetDatabaseSettings returns an instance")]
        public void GetDatabaseSettings_ReturnsInstance() => Assert.NotNull(_access.GetDatabaseSettings());

        [Fact]
        [DisplayName("GetDbCategorySettings returns an instance")]
        public void GetDbCategorySettings_ReturnsInstance() => Assert.NotNull(_access.GetDbCategorySettings());

        [Fact]
        [DisplayName("GetTableSchema returns an instance")]
        public void GetTableSchema_ReturnsInstance()
        {
            var schema = _access.GetTableSchema("common", "st_user");
            Assert.NotNull(schema);
            Assert.Equal("st_user", schema.TableName, ignoreCase: true);
        }

        [Fact]
        [DisplayName("GetFormSchema returns an instance")]
        public void GetFormSchema_ReturnsInstance()
        {
            var schema = _access.GetFormSchema("Employee");
            Assert.NotNull(schema);
            Assert.Equal("Employee", schema.ProgId);
        }
    }
}
