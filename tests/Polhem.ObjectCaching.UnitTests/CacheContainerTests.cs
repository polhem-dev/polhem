using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Behavior tests of <see cref="ICacheContainer"/>. The cache instance is resolved from the fixture's DI
    /// container and does not depend on a process-wide static facade.
    /// </summary>
    public class CacheContainerTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public CacheContainerTests(SharedDbFixture fx) { _fx = fx; }

        private ICacheContainer Cache => _fx.GetRequiredService<ICacheContainer>();

        [Fact]
        [DisplayName("TableSchema.Get(categoryId, tableName) returns the matching schema")]
        public void TableSchema_GetWithCategoryId_ReturnsSchema()
        {
            var schema = Cache.TableSchema.Get("common", "st_user");

            Assert.NotNull(schema);
            Assert.Equal("st_user", schema!.TableName, ignoreCase: true);
        }

        [Fact]
        [DisplayName("TableSchema.Get with DbCategoryIds.Common as the system database returns the schema")]
        public void TableSchema_GetWithCommonDatabase_ReturnsSchema()
        {
            // By framework convention, the `DatabaseItem` whose CategoryId is "common" also has the Id "common".
            var schema = Cache.TableSchema.Get(DbCategoryIds.Common, "st_user");

            Assert.NotNull(schema);
            Assert.Equal("st_user", schema!.TableName, ignoreCase: true);
        }

        [Fact]
        [DisplayName("FormSchema.Get returns the schema of the given progId")]
        public void FormSchema_ExistingProgId_ReturnsSchema()
        {
            var schema = Cache.FormSchema.Get("Department");

            Assert.NotNull(schema);
            Assert.Equal("Department", schema!.ProgId);
        }

        [Fact]
        [DisplayName("DbCategorySettings.Get returns the defined category settings")]
        public void DbCategorySettings_Get_ReturnsSettings()
        {
            var settings = Cache.DbCategorySettings.Get();
            Assert.True(settings!.Categories!.Contains("common"));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SessionInfo.Get returns the same object after Set and null after Remove")]
        public void SessionInfo_SetGetRemove_BehavesCorrectly()
        {
            var token = Guid.NewGuid();
            var info = new SessionInfo
            {
                AccessToken = token,
                UserId = "u1",
                UserName = "User One"
            };

            Cache.SessionInfo.Set(info);
            var fromCache = Cache.SessionInfo.Get(token);
            Assert.NotNull(fromCache);
            Assert.Equal(token, fromCache!.AccessToken);

            Cache.SessionInfo.Remove(token);
            Assert.Null(Cache.SessionInfo.Get(token));
        }

        [Fact]
        [DisplayName("ProgramSettings.Get throws FileNotFoundException when tests/Define has no ProgramSettings.xml")]
        public void ProgramSettings_NoSettingsFile_ThrowsFileNotFound()
        {
            // There is no ProgramSettings.xml under tests/Define, so `ProgramSettingsCache.CreateInstance` throws
            // `FileNotFoundException`. The point is to cover the file-load path of `ProgramSettingsCache.Get`.
            Assert.Throws<FileNotFoundException>(() => Cache.ProgramSettings.Get());
        }

        [Fact]
        [DisplayName("FormLayout.Get returns null for an undefined layoutId (a missing file is a normal case)")]
        public void FormLayout_UnknownLayoutId_ReturnsNull()
        {
            // A missing layout file is not an error: at run time the layout is generated from the FormSchema instead,
            // so the storage returns null and the cache remembers the miss as a negative entry. The point is to cover
            // the file-load path of `FormLayoutCache.Get`.
            Assert.Null(Cache.FormLayout.Get("__non_existent_layout_" + Guid.NewGuid().ToString("N")));
        }
    }
}
