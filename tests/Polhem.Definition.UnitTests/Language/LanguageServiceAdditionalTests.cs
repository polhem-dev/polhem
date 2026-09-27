using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Adds coverage for edge paths of <see cref="LanguageService"/>:
    /// the constructor null guard, the final return-null path of GetLangEnum (lang == defaultLang, or an empty defaultLang),
    /// the null propagation path of GetLangEnumText when langEnum is null,
    /// and the TryGetLangText branch where the resource exists but the subKey does not.
    /// </summary>
    public class LanguageServiceAdditionalTests
    {
        [Fact]
        [DisplayName("LanguageService constructor throws ArgumentNullException for null")]
        public void Constructor_NullDefineAccess_ThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => new LanguageService(null!, null));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        [DisplayName("GetLangEnum returns null without trying a fallback when lang == defaultLang and the Enum does not exist")]
        public void GetLangEnum_LangEqualsDefaultLang_EnumMiss_ReturnsNull()
        {
            var defineAccess = new MinimalLangDefineAccess("zh-TW");
            var svc = new LanguageService(defineAccess, null);

            Assert.Null(svc.GetLangEnum("zh-TW", "Common", "Gender"));
        }

        [Fact]
        [DisplayName("GetLangEnum returns null when defaultLang is an empty string and the Enum does not exist")]
        public void GetLangEnum_EmptyDefaultLang_EnumMiss_ReturnsNull()
        {
            var defineAccess = new MinimalLangDefineAccess("");
            var svc = new LanguageService(defineAccess, null);

            Assert.Null(svc.GetLangEnum("zh-TW", "Common", "Gender"));
        }

        [Fact]
        [DisplayName("GetLangEnumText returns null when GetLangEnum returns null (null propagation)")]
        public void GetLangEnumText_NullLangEnum_ReturnsNull()
        {
            var defineAccess = new MinimalLangDefineAccess("en-US");
            var svc = new LanguageService(defineAccess, null);

            Assert.Null(svc.GetLangEnumText("zh-TW", "Common.NonExistentEnum", "M"));
        }

        [Fact]
        [DisplayName("TryGetLangText returns false and an empty string when the resource exists but the subKey is not in Items")]
        public void TryGetLangText_ResourceExistsButKeyMissing_ReturnsFalseAndEmpty()
        {
            var defineAccess = new MinimalLangDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);

            bool result = svc.TryGetLangText("zh-TW", "Common", "Missing", out string text);

            Assert.False(result);
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        [DisplayName("GetLangText returns the namespace.subKey format when defaultLang is empty and the primary language misses")]
        public void GetLangText_EmptyDefaultLang_PrimaryMiss_ReturnsFallbackKey()
        {
            var defineAccess = new MinimalLangDefineAccess("");
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("Common.Missing", svc.GetLangText("zh-TW", "Common", "Missing"));
        }

        private sealed class MinimalLangDefineAccess : IDefineAccess
        {
            private readonly Dictionary<string, LanguageResource> _resources = [];
            private readonly SystemSettings _systemSettings;

            public MinimalLangDefineAccess(string defaultLang)
            {
                _systemSettings = new SystemSettings();
                _systemSettings.CommonConfiguration.DefaultLanguage = defaultLang;
            }

            public void AddResource(string lang, string ns, params (string Key, string Value)[] items)
            {
                var resource = new LanguageResource { Namespace = ns, Lang = lang };
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
                _resources[$"{lang}.{ns}"] = resource;
            }

            public LanguageResource GetLanguage(string lang, string ns)
                => _resources.TryGetValue($"{lang}.{ns}", out var r) ? r : null!;

            public SystemSettings GetSystemSettings() => _systemSettings;

            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
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
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }
    }
}
