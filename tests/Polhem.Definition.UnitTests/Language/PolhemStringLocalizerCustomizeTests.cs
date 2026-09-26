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
    /// Tenant customization pipeline tests for <see cref="PolhemStringLocalizer{T}"/>: a customizeIdProvider hit gives the cust value,
    /// a key missing from cust gives the base value, and the existing 1-arg / 2-arg constructors short-circuit to the base alone (zero reader calls, bit-for-bit the same as before).
    /// </summary>
    public class PolhemStringLocalizerCustomizeTests
    {
        // Marker type whose name maps to the "CommonResources" language namespace.
        // Avoids the BCL "Common" / System.Data.Common collision flagged by CA1724.
        public sealed class CommonResources { }

        [Fact]
        [DisplayName("Returns the cust value when customizeIdProvider has a value and cust has the key")]
        public void Indexer_CustHasKey_ReturnsCustValue()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "CommonResources", ("OK", "送出"));
            var svc = new LanguageService(defineAccess, reader);
            var localizer = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW", () => "acme");

            var result = localizer["OK"];

            Assert.Equal("送出", result.Value);
            Assert.False(result.ResourceNotFound);
        }

        [Fact]
        [DisplayName("Falls back to the base value when cust lacks the key")]
        public void Indexer_CustMissesKey_ReturnsBaseValue()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"), ("Cancel", "取消"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "CommonResources", ("OK", "送出"));
            var svc = new LanguageService(defineAccess, reader);
            var localizer = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW", () => "acme");

            Assert.Equal("取消", localizer["Cancel"].Value);
        }

        [Fact]
        [DisplayName("ResourceNotFound=true with the fullKey as the value when both cust and base lack the key")]
        public void Indexer_BothMiss_ReturnsResourceNotFound()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            var reader = new SpyCustomizeReader();
            var svc = new LanguageService(defineAccess, reader);
            var localizer = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW", () => "acme");

            var result = localizer["Nonexistent"];

            Assert.Equal("CommonResources.Nonexistent", result.Value);
            Assert.True(result.ResourceNotFound);
        }

        [Fact]
        [DisplayName("A null from customizeIdProvider is treated as an empty string and uses the base alone without throwing")]
        public void Indexer_NullCustomizeId_TreatedAsEmpty()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "CommonResources", ("OK", "送出"));
            var svc = new LanguageService(defineAccess, reader);
            var localizer = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW", () => null!);

            Assert.Equal("確定", localizer["OK"].Value);
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        [Fact]
        [DisplayName("Passing a null customizeIdProvider throws ArgumentNullException")]
        public void Ctor_NullCustomizeIdProvider_Throws()
        {
            var svc = new LanguageService(new StubDefineAccess("zh-TW"));

            Assert.Throws<ArgumentNullException>(() =>
                new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW", null!));
        }

        // ---- Regression guard: a deployment without a CustomizeId must behave bit-for-bit as before ----

        [Fact]
        [DisplayName("Regression guard: the 2-arg constructor never touches the customization layer (zero reader calls)")]
        public void Indexer_LangProviderOnlyCtor_NeverTouchesCustomizeLayer()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "CommonResources", ("OK", "送出"));
            var svc = new LanguageService(defineAccess, reader);
            var localizer = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW");

            Assert.Equal("確定", localizer["OK"].Value);
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        [Fact]
        [DisplayName("Regression guard: an empty string from customizeIdProvider is equivalent to the 2-arg constructor")]
        public void Indexer_EmptyCustomizeId_MatchesLangProviderOnlyCtor()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "CommonResources", ("OK", "確定"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "CommonResources", ("OK", "送出"));
            var svc = new LanguageService(defineAccess, reader);

            var legacy = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW")["OK"];
            var explicitEmpty = new PolhemStringLocalizer<CommonResources>(svc, () => "zh-TW", () => string.Empty)["OK"];

            Assert.Equal(legacy.Value, explicitEmpty.Value);
            Assert.Equal(legacy.ResourceNotFound, explicitEmpty.ResourceNotFound);
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        // ---- Test doubles ----

        private sealed class SpyCustomizeReader : ICustomizeDefineReader
        {
            private readonly Dictionary<string, LanguageResource> _languages = [];

            public int GetCustomizeLanguageCallCount { get; private set; }

            public void AddLanguage(string customizeId, string lang, string ns, params (string Key, string Value)[] items)
            {
                var resource = new LanguageResource { Namespace = ns, Lang = lang };
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
                _languages[$"{customizeId}.{lang}.{ns}"] = resource;
            }

            public LanguageResource? GetCustomizeLanguage(string customizeId, string lang, string ns)
            {
                GetCustomizeLanguageCallCount++;
                return _languages.TryGetValue($"{customizeId}.{lang}.{ns}", out var r) ? r : null;
            }

            public ProgramSettings? GetCustomizeProgramSettings(string customizeId) => null;
            public FormLayout? GetCustomizeFormLayout(string customizeId, string layoutId) => null;
            public MenuSettings? GetCustomizeMenuSettings(string customizeId) => null;
            public PluginSettings? GetCustomizePluginSettings(string customizeId) => null;
        }

        private sealed class StubDefineAccess : IDefineAccess
        {
            private readonly Dictionary<string, LanguageResource> _resources = [];
            private readonly SystemSettings _systemSettings;

            public StubDefineAccess(string defaultLang)
            {
                _systemSettings = new SystemSettings();
                _systemSettings.CommonConfiguration.DefaultLang = defaultLang;
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
