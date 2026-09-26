using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Coverage tests of every Save method of <see cref="CacheDefineAccess"/>.
    /// Each test isolates its <see cref="PathOptions"/> in a local <see cref="TempDir"/> and passes it straight to
    /// the <see cref="CacheDefineAccess"/> constructor, so no process-wide path state is touched.
    /// </summary>
    /// <remarks>
    /// The Save paths call <c>Remove(key)</c> on the cache container to invalidate cached entries, but every key is
    /// specific to this test (such as <c>dbX/t_sample</c> and <c>P_Test</c>), so other tests are not affected.
    /// </remarks>
    public class CacheDefineAccessSaveTests
    {
        private static readonly string[] s_dbViaDefineKeys = { "db_via_define" };

        private static CacheDefineAccess CreateAccess(PathOptions paths)
            => new CacheDefineAccess(new FileDefineStorage(paths), paths);

        [Fact]
        [DisplayName("SaveSystemSettings writes SystemSettings.xml containing the saved values")]
        public void SaveSystemSettings_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var settings = new SystemSettings();
            settings.BackendConfiguration.SecurityKeySettings.MasterKeySource.Value = "saved_id";

            access.SaveSystemSettings(settings);

            var filePath = temp.Options.GetSystemSettingsFilePath();
            Assert.True(File.Exists(filePath));
            Assert.Contains("saved_id", File.ReadAllText(filePath));
        }

        [Fact]
        [DisplayName("SaveDatabaseSettings writes DatabaseSettings.xml")]
        public void SaveDatabaseSettings_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var settings = new DatabaseSettings();

            access.SaveDatabaseSettings(settings);

            Assert.True(File.Exists(temp.Options.GetDatabaseSettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveProgramSettings writes ProgramSettings.xml")]
        public void SaveProgramSettings_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var settings = new ProgramSettings();

            access.SaveProgramSettings(settings);

            Assert.True(File.Exists(temp.Options.GetProgramSettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveDbCategorySettings writes DbCategorySettings.xml through the DefineStorage")]
        public void SaveDbCategorySettings_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var settings = new DbCategorySettings();

            access.SaveDbCategorySettings(settings);

            Assert.True(File.Exists(temp.Options.GetDbCategorySettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveTableSchema writes the TableSchema xml under the matching database folder")]
        public void SaveTableSchema_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var schema = new TableSchema { TableName = "t_sample" };

            access.SaveTableSchema("dbX", schema);

            Assert.True(File.Exists(temp.Options.GetTableSchemaFilePath("dbX", "t_sample")));
        }

        [Fact]
        [DisplayName("SaveFormSchema writes a FormSchema xml named after the ProgId")]
        public void SaveFormSchema_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var schema = new FormSchema { ProgId = "P_Test", CategoryId = "common" };

            access.SaveFormSchema(schema);

            Assert.True(File.Exists(temp.Options.GetFormSchemaFilePath("P_Test")));
        }

        [Fact]
        [DisplayName("SaveFormSchema throws InvalidOperationException when CategoryId is missing")]
        public void SaveFormSchema_ThrowsWhenCategoryIdEmpty()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var schema = new FormSchema { ProgId = "P_NoCategory" };

            var ex = Assert.Throws<InvalidOperationException>(() => access.SaveFormSchema(schema));
            Assert.Contains("P_NoCategory", ex.Message);
            Assert.Contains("CategoryId", ex.Message);
        }

        [Fact]
        [DisplayName("SaveFormLayout writes a FormLayout xml named after the LayoutId")]
        public void SaveFormLayout_WritesFile()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var layout = new FormLayout { LayoutId = "L_Test" };

            access.SaveFormLayout(layout);

            Assert.True(File.Exists(temp.Options.GetFormLayoutFilePath("L_Test")));
        }

        // NOTE: The Save, cache miss and reload round trip through the cache layer is covered by the `*_AfterSave`
        // tests in `CacheDefineAccessMissingCoverageTests`, which give each access its own path and cache prefix.

        [Fact]
        [DisplayName("SaveDefine(SystemSettings) delegates to SaveSystemSettings")]
        public void SaveDefine_SystemSettings_DelegatesToSaveSystemSettings()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            access.SaveDefine(DefineType.SystemSettings, new SystemSettings());
            Assert.True(File.Exists(temp.Options.GetSystemSettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveDefine(DatabaseSettings) delegates to SaveDatabaseSettings")]
        public void SaveDefine_DatabaseSettings_DelegatesToSaveDatabaseSettings()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            access.SaveDefine(DefineType.DatabaseSettings, new DatabaseSettings());
            Assert.True(File.Exists(temp.Options.GetDatabaseSettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveDefine(ProgramSettings) delegates to SaveProgramSettings")]
        public void SaveDefine_ProgramSettings_DelegatesToSaveProgramSettings()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            access.SaveDefine(DefineType.ProgramSettings, new ProgramSettings());
            Assert.True(File.Exists(temp.Options.GetProgramSettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveDefine(DbCategorySettings) delegates to SaveDbCategorySettings")]
        public void SaveDefine_DbCategorySettings_DelegatesToSaveDbCategorySettings()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            access.SaveDefine(DefineType.DbCategorySettings, new DbCategorySettings());
            Assert.True(File.Exists(temp.Options.GetDbCategorySettingsFilePath()));
        }

        [Fact]
        [DisplayName("SaveDefine(TableSchema) with a single key delegates to SaveTableSchema")]
        public void SaveDefine_TableSchema_WithKey_DelegatesToSaveTableSchema()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var schema = new TableSchema { TableName = "t_via_define" };
            access.SaveDefine(DefineType.TableSchema, schema, s_dbViaDefineKeys);
            Assert.True(File.Exists(temp.Options.GetTableSchemaFilePath("db_via_define", "t_via_define")));
        }

        [Fact]
        [DisplayName("SaveDefine(FormLayout) delegates to SaveFormLayout")]
        public void SaveDefine_FormLayout_DelegatesToSaveFormLayout()
        {
            using var temp = TempDir.Create();
            var access = CreateAccess(temp.Options);
            var layout = new FormLayout { LayoutId = "L_via_define" };
            access.SaveDefine(DefineType.FormLayout, layout);
            Assert.True(File.Exists(temp.Options.GetFormLayoutFilePath("L_via_define")));
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }
            public PathOptions Options { get; }

            private TempDir(string path)
            {
                Path = path;
                Options = new PathOptions { DefinePath = path };
            }

            public static TempDir Create()
            {
                var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"polhem-save-{Guid.NewGuid():N}");
                Directory.CreateDirectory(dir);
                return new TempDir(dir);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                        Directory.Delete(Path, recursive: true);
                }
                catch (IOException)
                {
                    // best effort
                }
            }
        }
    }
}
