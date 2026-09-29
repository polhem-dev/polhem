using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Tests for how FileDefineStorage reads and writes XML files.
    /// Each test uses an isolated temp directory as DefinePath (through <c>WithTempDefinePath</c>) and touches no
    /// process-wide state, so it would stay correct if this assembly ran its test classes in parallel.
    /// </summary>
    public class FileDefineStorageTests
    {
        [Fact]
        [DisplayName("SaveFormSchema / GetFormSchema writes and reads back the same structure")]
        public void SaveAndGetFormSchema_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);
                var schema = new FormSchema("Demo", "示範");
                var table = schema.Tables!.Add("Demo", "示範");
                table.Fields!.Add("sys_id", "編號", FieldDbType.String);

                // Act
                storage.SaveFormSchema(schema);
                var restored = storage.GetFormSchema("Demo");

                // Assert
                Assert.NotNull(restored);
                Assert.Equal("Demo", restored!.ProgId);
                Assert.Equal("示範", restored.DisplayName);
            });
        }

        [Fact]
        [DisplayName("GetFormSchemaIds lists the progIds of the stored FormSchema files, and is empty without the folder")]
        public void GetFormSchemaIds_ListsStoredForms()
        {
            WithTempDefinePath(paths =>
            {
                var storage = new FileDefineStorage(paths);
                Assert.Empty(storage.GetFormSchemaIds());

                storage.SaveFormSchema(new FormSchema("Beta", "Beta"));
                storage.SaveFormSchema(new FormSchema("Alpha", "Alpha"));
                File.WriteAllText(Path.Combine(paths.DefinePath, "FormSchema", "notes.txt"), "not a schema");

                Assert.Equal(["Alpha", "Beta"], storage.GetFormSchemaIds());
            });
        }

        [Fact]
        [DisplayName("GetFormSchema throws FileNotFoundException for a missing file")]
        public void GetFormSchema_FileNotFound_Throws()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);

                // Act & Assert
                Assert.Throws<FileNotFoundException>(() => storage.GetFormSchema("nonexistent"));
            });
        }

        [Fact]
        [DisplayName("SaveTableSchema / GetTableSchema writes and reads back")]
        public void SaveAndGetTableSchema_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);
                var schema = new TableSchema { TableName = "ft_demo", DisplayName = "Demo" };
                schema.Fields!.Add("sys_no", "流水號", FieldDbType.AutoIncrement);

                // Act
                storage.SaveTableSchema("common", schema);
                var restored = storage.GetTableSchema("common", "ft_demo");

                // Assert
                Assert.NotNull(restored);
                Assert.Equal("ft_demo", restored!.TableName);
                Assert.Single(restored.Fields!);
            });
        }

        [Fact]
        [DisplayName("GetTableSchema throws FileNotFoundException for a missing file")]
        public void GetTableSchema_FileNotFound_Throws()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);

                // Act & Assert
                Assert.Throws<FileNotFoundException>(() => storage.GetTableSchema("common", "missing"));
            });
        }

        [Fact]
        [DisplayName("SaveFormLayout / GetFormLayout writes and reads back")]
        public void SaveAndGetFormLayout_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);
                var layout = new FormLayout { LayoutId = "DemoLayout", Caption = "示範" };

                // Act
                storage.SaveFormLayout(layout);
                var restored = storage.GetFormLayout("DemoLayout");

                // Assert
                Assert.NotNull(restored);
                Assert.Equal("DemoLayout", restored!.LayoutId);
            });
        }

        [Fact]
        [DisplayName("GetFormLayout returns null for a missing file (honoring the nullable contract declared by the interface)")]
        public void GetFormLayout_FileNotFound_ReturnsNull()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);

                // Act & Assert: a missing layout file is a normal case (the framework generates one from the FormSchema), not an error.
                Assert.Null(storage.GetFormLayout("missing"));
            });
        }

        [Fact]
        [DisplayName("SaveDbCategorySettings / GetDbCategorySettings writes and reads back")]
        public void SaveAndGetDbCategorySettings_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange: saving an empty instance and reading it back only proves that the file exists and deserializes without
                // throwing, not that the content was written, so recognizable data is filled in.
                var storage = new FileDefineStorage(paths);
                var settings = new DbCategorySettings();
                settings.Categories!.Add(new DbCategory { Id = "common", DisplayName = "共用資料庫" });

                // Act
                storage.SaveDbCategorySettings(settings);
                var restored = storage.GetDbCategorySettings();

                // Assert
                Assert.NotNull(restored);
                Assert.Single(restored.Categories!);
                Assert.Equal("共用資料庫", restored.Categories!["common"]!.DisplayName);
            });
        }

        [Fact]
        [DisplayName("GetDbCategorySettings throws FileNotFoundException for a missing file")]
        public void GetDbCategorySettings_FileNotFound_Throws()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);

                // Act & Assert
                Assert.Throws<FileNotFoundException>(() => storage.GetDbCategorySettings());
            });
        }

        [Fact]
        [DisplayName("SaveProgramSettings / GetProgramSettings writes and reads back")]
        public void SaveAndGetProgramSettings_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                // Same reason as in `SaveAndGetDbCategorySettings_RoundTrips`.
                var storage = new FileDefineStorage(paths);
                var settings = new ProgramSettings();
                settings.Items!.Add(new ProgramItem("Employee", "員工資料"));

                storage.SaveProgramSettings(settings);
                var restored = storage.GetProgramSettings();

                Assert.NotNull(restored);
                Assert.Single(restored.Items!);
                Assert.Equal("員工資料", restored.Items!["Employee"]!.DisplayName);
            });
        }

        [Fact]
        [DisplayName("GetProgramSettings throws FileNotFoundException for a missing file")]
        public void GetProgramSettings_FileNotFound_Throws()
        {
            WithTempDefinePath(paths =>
            {
                var storage = new FileDefineStorage(paths);
                Assert.Throws<FileNotFoundException>(() => storage.GetProgramSettings());
            });
        }

        [Fact]
        [DisplayName("SaveLanguage / GetLanguage writes and reads back the same language resource")]
        public void SaveAndGetLanguage_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                var storage = new FileDefineStorage(paths);
                var resource = new LanguageResource { Lang = "en", Namespace = "Core" };

                storage.SaveLanguage(resource);
                var restored = storage.GetLanguage("en", "Core");

                Assert.NotNull(restored);
                Assert.Equal("en", restored!.Lang);
                Assert.Equal("Core", restored.Namespace);
            });
        }

        [Fact]
        [DisplayName("GetLanguage returns null for a missing file (instead of throwing)")]
        public void GetLanguage_FileNotFound_ReturnsNull()
        {
            WithTempDefinePath(paths =>
            {
                var storage = new FileDefineStorage(paths);
                var result = storage.GetLanguage("zh-TW", "Missing");
                Assert.Null(result);
            });
        }

        [Fact]
        [DisplayName("SaveMenuSettings / GetMenuSettings writes and reads back the nested structure")]
        public void SaveAndGetMenuSettings_RoundTrips()
        {
            WithTempDefinePath(paths =>
            {
                // Arrange
                var storage = new FileDefineStorage(paths);
                var settings = new MenuSettings();
                var folder = settings.Items!.AddFolder("sales", "銷售");
                folder.Items!.AddEntry("sales-order", "Order", "訂單");

                // Act
                storage.SaveMenuSettings(settings);
                var restored = storage.GetMenuSettings();

                // Assert
                Assert.NotNull(restored);
                var restoredFolder = Assert.IsType<MenuFolder>(restored!.Items!.Single());
                var entry = Assert.IsType<MenuEntry>(restoredFolder.Items!.Single());
                Assert.Equal("Order", entry.ProgId);
            });
        }

        [Fact]
        [DisplayName("GetMenuSettings returns null when MenuSettings.xml does not exist (a deployment without a menu is normal)")]
        public void GetMenuSettings_FileMissing_ReturnsNull()
        {
            WithTempDefinePath(paths =>
            {
                var storage = new FileDefineStorage(paths);

                Assert.Null(storage.GetMenuSettings());
            });
        }

        [Fact]
        [DisplayName("GetMenuSettings throws at load time when an Id is duplicated anywhere in the MenuSettings tree")]
        public void GetMenuSettings_DuplicateIdAcrossTree_Throws()
        {
            WithTempDefinePath(paths =>
            {
                // Written by hand: the collection would reject the duplicate if it were built in
                // memory, which is precisely why the tree walk has to run at load time.
                File.WriteAllText(paths.GetMenuSettingsFilePath(), """
                    <?xml version="1.0" encoding="utf-8"?>
                    <MenuSettings>
                      <Items>
                        <MenuFolder Id="dup" Caption="資料夾">
                          <Items><MenuEntry Id="dup" ProgId="Order" Caption="訂單" /></Items>
                        </MenuFolder>
                      </Items>
                    </MenuSettings>
                    """);
                var storage = new FileDefineStorage(paths);

                var ex = Assert.Throws<InvalidOperationException>(() => storage.GetMenuSettings());
                Assert.Contains("'dup'", ex.Message, StringComparison.Ordinal);
            });
        }

        [Fact]
        [DisplayName("Flattened registry items (including BusinessObject) write and read back")]
        public void SaveAndGetProgramSettings_FlatItems_RoundTrip()
        {
            WithTempDefinePath(paths =>
            {
                var storage = new FileDefineStorage(paths);
                var settings = new ProgramSettings();
                settings.Items!.Add("Order", "訂單").BusinessObject = "MyErp.OrderBO, MyErp";

                storage.SaveProgramSettings(settings);
                var restored = storage.GetProgramSettings();

                Assert.NotNull(restored);
                Assert.Equal("MyErp.OrderBO, MyErp", restored!.Items!["Order"].BusinessObject);
            });
        }

        /// <summary>
        /// Creates a new temp directory, passes the matching <see cref="PathOptions"/> to <paramref name="action"/>, and
        /// deletes the directory after the test. Tests inject the supplied <see cref="PathOptions"/> directly into
        /// <see cref="FileDefineStorage"/>, so no test shares a define directory with another.
        /// </summary>
        private static void WithTempDefinePath(Action<PathOptions> action)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-define-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                action(new PathOptions { DefinePath = tempDir });
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
