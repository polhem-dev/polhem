using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Tests for the default interface methods of <see cref="IDefineAccess"/> that route the settings types through
    /// <see cref="IDefineAccess.GetDefine"/> and <see cref="IDefineAccess.SaveDefine"/>, and for the defaults of
    /// <see cref="IDefineStorage"/>. The stubs implement only the required members, so the defaults are what runs.
    /// </summary>
    public class DefineInterfaceDefaultSettingsTests
    {
        [Fact]
        [DisplayName("GetMenuSettings, with or without a customization code, reads the MenuSettings define")]
        public void GetMenuSettings_DefaultImpl_ReadsMenuSettingsDefine()
        {
            IDefineAccess access = new RecordingDefineAccess();

            Assert.IsType<MenuSettings>(access.GetMenuSettings());
            Assert.IsType<MenuSettings>(access.GetMenuSettings("acme"));
        }

        [Fact]
        [DisplayName("GetPluginSettings reads the PluginSettings define")]
        public void GetPluginSettings_DefaultImpl_ReadsPluginSettingsDefine()
        {
            IDefineAccess access = new RecordingDefineAccess();

            Assert.IsType<PluginSettings>(access.GetPluginSettings());
        }

        [Fact]
        [DisplayName("GetCurrencySettings reads the CurrencySettings define")]
        public void GetCurrencySettings_DefaultImpl_ReadsCurrencySettingsDefine()
        {
            IDefineAccess access = new RecordingDefineAccess();

            Assert.IsType<CurrencySettings>(access.GetCurrencySettings());
        }

        [Fact]
        [DisplayName("GetUnitSettings reads the UnitSettings define")]
        public void GetUnitSettings_DefaultImpl_ReadsUnitSettingsDefine()
        {
            IDefineAccess access = new RecordingDefineAccess();

            Assert.IsType<UnitSettings>(access.GetUnitSettings());
        }

        [Fact]
        [DisplayName("SaveMenuSettings writes through SaveDefine with the MenuSettings type")]
        public void SaveMenuSettings_DefaultImpl_SavesMenuSettingsDefine()
        {
            var stub = new RecordingDefineAccess();

            ((IDefineAccess)stub).SaveMenuSettings(new MenuSettings());

            Assert.Equal(DefineType.MenuSettings, stub.LastSaved);
        }

        [Fact]
        [DisplayName("SavePluginSettings writes through SaveDefine with the PluginSettings type")]
        public void SavePluginSettings_DefaultImpl_SavesPluginSettingsDefine()
        {
            var stub = new RecordingDefineAccess();

            ((IDefineAccess)stub).SavePluginSettings(new PluginSettings());

            Assert.Equal(DefineType.PluginSettings, stub.LastSaved);
        }

        [Fact]
        [DisplayName("SaveCurrencySettings writes through SaveDefine with the CurrencySettings type")]
        public void SaveCurrencySettings_DefaultImpl_SavesCurrencySettingsDefine()
        {
            var stub = new RecordingDefineAccess();

            ((IDefineAccess)stub).SaveCurrencySettings([]);

            Assert.Equal(DefineType.CurrencySettings, stub.LastSaved);
        }

        [Fact]
        [DisplayName("SaveUnitSettings writes through SaveDefine with the UnitSettings type")]
        public void SaveUnitSettings_DefaultImpl_SavesUnitSettingsDefine()
        {
            var stub = new RecordingDefineAccess();

            ((IDefineAccess)stub).SaveUnitSettings([]);

            Assert.Equal(DefineType.UnitSettings, stub.LastSaved);
        }

        [Fact]
        [DisplayName("FindFormLayout reports no customization override by default")]
        public void FindFormLayout_DefaultImpl_ReturnsNull()
        {
            IDefineAccess access = new RecordingDefineAccess();

            Assert.Null(access.FindFormLayout("acme", "Order"));
        }

        [Fact]
        [DisplayName("A storage that does not list form schemas reports an empty list by default")]
        public void GetFormSchemaIds_DefaultImpl_ReturnsEmpty()
        {
            IDefineStorage storage = new MinimalDefineStorage();

            Assert.Empty(storage.GetFormSchemaIds());
        }

        [Fact]
        [DisplayName("A storage that offers no change signal reports none by default")]
        public void GetChangeSource_DefaultImpl_ReturnsNone()
        {
            IDefineStorage storage = new MinimalDefineStorage();

            Assert.Equal(DefineChangeSource.None, storage.GetChangeSource(DefineType.FormSchema, "Order"));
        }

        private sealed class RecordingDefineAccess : IDefineAccess
        {
            public DefineType? LastSaved { get; private set; }

            public object GetDefine(DefineType defineType, string[]? keys = null) => defineType switch
            {
                DefineType.MenuSettings => new MenuSettings(),
                DefineType.PluginSettings => new PluginSettings(),
                DefineType.CurrencySettings => new CurrencySettings(),
                DefineType.UnitSettings => new UnitSettings(),
                _ => throw new NotSupportedException()
            };

            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null)
                => LastSaved = defineType;

            public SystemSettings GetSystemSettings() => throw new NotSupportedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotSupportedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotSupportedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotSupportedException();
            public ProgramSettings GetProgramSettings() => throw new NotSupportedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotSupportedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotSupportedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotSupportedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotSupportedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotSupportedException();
            public FormSchema GetFormSchema(string progId) => throw new NotSupportedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotSupportedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotSupportedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotSupportedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotSupportedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotSupportedException();
        }

        private sealed class MinimalDefineStorage : IDefineStorage
        {
            public DbCategorySettings? GetDbCategorySettings() => null;
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotSupportedException();
            public CurrencySettings? GetCurrencySettings() => null;
            public void SaveCurrencySettings(CurrencySettings settings) => throw new NotSupportedException();
            public UnitSettings? GetUnitSettings() => null;
            public void SaveUnitSettings(UnitSettings settings) => throw new NotSupportedException();
            public ProgramSettings? GetProgramSettings() => null;
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotSupportedException();
            public MenuSettings? GetMenuSettings() => null;
            public void SaveMenuSettings(MenuSettings settings) => throw new NotSupportedException();
            public PluginSettings? GetPluginSettings() => null;
            public void SavePluginSettings(PluginSettings settings) => throw new NotSupportedException();
            public TableSchema? GetTableSchema(string categoryId, string tableName) => null;
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotSupportedException();
            public FormSchema? GetFormSchema(string progId) => null;
            public void SaveFormSchema(FormSchema formSchema) => throw new NotSupportedException();
            public FormLayout? GetFormLayout(string layoutId) => null;
            public void SaveFormLayout(FormLayout formLayout) => throw new NotSupportedException();
            public LanguageResource? GetLanguage(string lang, string ns) => null;
            public void SaveLanguage(LanguageResource resource) => throw new NotSupportedException();
        }
    }
}
