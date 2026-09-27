using System.ComponentModel;
using System.Globalization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Behavior tests for <see cref="LanguageResourceStringLocalizer{T}"/>: resolving the namespace through typeof(T).Name,
    /// hits and missing translations through ILanguageService, the format overload, and the ResourceNotFound flag.
    /// </summary>
    public class LanguageResourceStringLocalizerTests
    {
        // Marker type whose name maps to the "CommonResources" language namespace.
        // Avoids the BCL "Common" / System.Data.Common collision flagged by CA1724.
        public sealed class CommonResources { }

        [Fact]
        [DisplayName("Indexer hit gives the translation as LocalizedString.Value and ResourceNotFound=false")]
        public void Indexer_Hit_ReturnsLocalizedString()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);
            var localizer = new LanguageResourceStringLocalizer<CommonResources>(svc, () => "zh-TW");

            var result = localizer["OK"];

            Assert.Equal("OK", result.Name);
            Assert.Equal("確定", result.Value);
            Assert.False(result.ResourceNotFound);
        }

        [Fact]
        [DisplayName("Indexer miss gives the fullKey as LocalizedString.Value and ResourceNotFound=true")]
        public void Indexer_Miss_ReturnsResourceNotFound()
        {
            var defineAccess = new StubDefineAccess("en-US"); // Neither language has it.
            var svc = new LanguageService(defineAccess, null);
            var localizer = new LanguageResourceStringLocalizer<CommonResources>(svc, () => "zh-TW");

            var result = localizer["Nonexistent"];

            Assert.Equal("Nonexistent", result.Name);
            Assert.Equal("CommonResources.Nonexistent", result.Value);
            Assert.True(result.ResourceNotFound);
        }

        [Fact]
        [DisplayName("Indexer hit through the default language fallback gives ResourceNotFound=false")]
        public void Indexer_FallbackHit_ReturnsLocalizedString()
        {
            var defineAccess = new StubDefineAccess("en-US");
            // Missing in zh-TW, present in en-US.
            defineAccess.AddResource("en-US", "CommonResources", ("OK", "OK"));
            var svc = new LanguageService(defineAccess, null);
            var localizer = new LanguageResourceStringLocalizer<CommonResources>(svc, () => "zh-TW");

            var result = localizer["OK"];

            Assert.Equal("OK", result.Value);
            Assert.False(result.ResourceNotFound);
        }

        [Fact]
        [DisplayName("Indexer with arguments applies them with string.Format")]
        public void Indexer_WithArguments_FormatsValue()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "CommonResources", ("Greeting", "你好，{0}！"));
            var svc = new LanguageService(defineAccess, null);
            var localizer = new LanguageResourceStringLocalizer<CommonResources>(svc, () => "zh-TW");

            var result = localizer["Greeting", "Jeff"];

            Assert.Equal("你好，Jeff！", result.Value);
        }

        [Fact]
        [DisplayName("Default ctor takes lang from CultureInfo.CurrentUICulture")]
        public void DefaultCtor_UsesCurrentUICulture()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("ja-JP", "CommonResources", ("OK", "確認"));
            var svc = new LanguageService(defineAccess, null);
            var localizer = new LanguageResourceStringLocalizer<CommonResources>(svc);

            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
                var result = localizer["OK"];
                Assert.Equal("確認", result.Value);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        [Fact]
        [DisplayName("GetAllStrings returns an empty collection (language resources do not support enumerating every key)")]
        public void GetAllStrings_ReturnsEmpty()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);
            var localizer = new LanguageResourceStringLocalizer<CommonResources>(svc, () => "zh-TW");

            Assert.Empty(localizer.GetAllStrings(includeParentCultures: false));
        }

        // Inline copy of the stub used by LanguageServiceTests — keeps each test
        // file self-contained without coupling on test-shared fakes.
        private sealed class StubDefineAccess : IDefineAccess
        {
            private readonly Dictionary<string, LanguageResource> _resources = [];
            private readonly SystemSettings _systemSettings;

            public StubDefineAccess(string defaultLang)
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
