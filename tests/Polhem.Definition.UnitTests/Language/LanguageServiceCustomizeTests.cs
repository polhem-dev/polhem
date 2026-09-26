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
    /// Tenant customization overlay tests for <see cref="LanguageService"/>: a key in cust gives the cust value, a key missing from cust gives the base value,
    /// a missing cust resource gives the base for everything, and an empty customizeId or no reader short-circuits to the base alone (zero reader calls).
    /// </summary>
    public class LanguageServiceCustomizeTests
    {
        private static readonly string[] s_custOnlyStatusCodes = ["1", "4"];
        private static readonly string[] s_custOnlyStatusTexts = ["待簽核", "註銷"];
        private static readonly string[] s_custOrderCodes = ["9", "0"];
        private static readonly string[] s_custOrderTexts = ["客製狀態", "初稿"];

        [Fact]
        [DisplayName("Returns the cust value when cust has the key (overriding the base)")]
        public void TryGetLangText_CustHasKey_ReturnsCustValue()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Common", ("OK", "客製確定"));
            var svc = new LanguageService(defineAccess, reader);

            Assert.Equal("客製確定", svc.GetLangText("acme", "zh-TW", "Common", "OK"));
        }

        [Fact]
        [DisplayName("Falls back to the base value when the cust resource exists but lacks the key")]
        public void TryGetLangText_CustMissesKey_ReturnsBaseValue()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"), ("Cancel", "取消"));
            var reader = new SpyCustomizeReader();
            // The cust resource overrides only OK and has no Cancel.
            reader.AddLanguage("acme", "zh-TW", "Common", ("OK", "客製確定"));
            var svc = new LanguageService(defineAccess, reader);

            Assert.Equal("取消", svc.GetLangText("acme", "zh-TW", "Common", "Cancel"));
        }

        [Fact]
        [DisplayName("Returns base values for everything when the cust resource does not exist")]
        public void TryGetLangText_NoCustResource_ReturnsBaseValue()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var reader = new SpyCustomizeReader(); // acme has no customization at all.
            var svc = new LanguageService(defineAccess, reader);

            Assert.Equal("確定", svc.GetLangText("acme", "zh-TW", "Common", "OK"));
        }

        [Fact]
        [DisplayName("The overload without a customizeId short-circuits to the base alone with zero reader calls")]
        public void EmptyCustomizeId_ShortCircuits_ReaderNotCalled()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Common", ("OK", "客製確定"));
            var svc = new LanguageService(defineAccess, reader);

            // Through the base overload that takes no customizeId.
            Assert.Equal("確定", svc.GetLangText("zh-TW", "Common.OK"));
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        [Fact]
        [DisplayName("Without an injected reader the behavior matches the base alone (backward compatible)")]
        public void NoReader_BehavesAsBase()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var svc = new LanguageService(defineAccess); // No reader.

            // Even with a customizeId, having no reader degrades to the base.
            Assert.Equal("確定", svc.GetLangText("acme", "zh-TW", "Common", "OK"));
        }

        [Fact]
        [DisplayName("Base and customization loaded for the same namespace: of 20 keys only 5 are customized, the rest come from the base, and a customization-only key also works")]
        public void TryGetLangText_PartialOverride_MergesPerKeyAtLookupTime()
        {
            var baseItems = Enumerable.Range(1, 20)
                .Select(i => ($"Key{i:00}", $"套裝{i:00}"))
                .ToArray();
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer", baseItems);

            // The customization language file changes only 5 of the keys and adds 1 key the base does not have.
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer",
                ("Key03", "客製03"), ("Key07", "客製07"), ("Key11", "客製11"),
                ("Key15", "客製15"), ("Key20", "客製20"),
                ("KeyOnlyInCustomize", "客製獨有"));
            var svc = new LanguageService(defineAccess, reader);

            var overridden = new[] { "Key03", "Key07", "Key11", "Key15", "Key20" };
            foreach (var (key, _) in baseItems)
            {
                string expected = overridden.Contains(key, StringComparer.Ordinal)
                    ? $"客製{key.Substring(3)}"   // Both sides have the key, so the customization wins.
                    : $"套裝{key.Substring(3)}";  // The customization lacks it, so the base value is used.
                Assert.Equal(expected, svc.GetLangText("acme", "zh-TW", "Customer", key));
            }

            // A customization-only key is found too (added when the base lacks it).
            Assert.Equal("客製獨有", svc.GetLangText("acme", "zh-TW", "Customer", "KeyOnlyInCustomize"));

            // The same base resource is entirely unaffected by a lookup without a customizeId (another company, or one without customization).
            Assert.Equal("套裝03", svc.GetLangText("zh-TW", "Customer", "Key03"));
        }

        [Fact]
        [DisplayName("Tenant isolation: company A's customization does not affect company B's lookup results")]
        public void TryGetLangText_DifferentCustomizeIds_AreIsolated()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Customer", ("Key01", "套裝01"));
            var reader = new SpyCustomizeReader();
            reader.AddLanguage("acme", "zh-TW", "Customer", ("Key01", "acme 客製"));
            reader.AddLanguage("globex", "zh-TW", "Customer", ("Key01", "globex 客製"));
            var svc = new LanguageService(defineAccess, reader);

            Assert.Equal("acme 客製", svc.GetLangText("acme", "zh-TW", "Customer", "Key01"));
            Assert.Equal("globex 客製", svc.GetLangText("globex", "zh-TW", "Customer", "Key01"));
            // A company without a customization file still gets the base value.
            Assert.Equal("套裝01", svc.GetLangText("initech", "zh-TW", "Customer", "Key01"));
        }

        [Fact]
        [DisplayName("Enum overlay: when cust overrides every entry, each entry returns the cust text")]
        public void GetLangEnum_CustOverridesEveryEntry_ReturnsCustText()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"), ("F", "女"));
            var reader = new SpyCustomizeReader();
            reader.AddEnum("acme", "zh-TW", "Common", "Gender", ("M", "先生"), ("F", "小姐"));
            var svc = new LanguageService(defineAccess, reader);

            var result = svc.GetLangEnum("acme", "zh-TW", "Common", "Gender");

            Assert.NotNull(result);
            Assert.Equal("先生", result!.GetText("M"));
            Assert.Equal("小姐", result.GetText("F"));
        }

        [Fact]
        [DisplayName("An enum is replaced as a whole, not overlaid per entry: entries the base has but the customization lacks are not kept")]
        public void GetLangEnum_CustHasEnum_ReplacesWholeSetWithoutMergingBaseEntries()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Order", "OrderStatus",
                ("0", "草稿"), ("1", "待審"), ("2", "已審"), ("3", "出貨"), ("4", "作廢"));
            var reader = new SpyCustomizeReader();
            // The customization lists only 2 entries, so the result has only those 2 and the other 3 base entries are not merged in.
            reader.AddEnum("acme", "zh-TW", "Order", "OrderStatus", ("1", "待簽核"), ("4", "註銷"));
            var svc = new LanguageService(defineAccess, reader);

            var result = svc.GetLangEnum("acme", "zh-TW", "Order", "OrderStatus");

            Assert.NotNull(result);
            Assert.Equal(s_custOnlyStatusCodes, result!.Entries.Select(e => e.Code).ToArray());
            Assert.Equal(s_custOnlyStatusTexts, result.Entries.Select(e => e.Text).ToArray());
            Assert.Null(result.GetText("0"));
            Assert.Null(result.GetText("2"));
        }

        [Fact]
        [DisplayName("Enum replaced as a whole: the entry order of the customization file is the result order")]
        public void GetLangEnum_CustHasEnum_PreservesCustomizeDocumentOrder()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Order", "OrderStatus", ("0", "草稿"), ("1", "待審"));
            var reader = new SpyCustomizeReader();
            reader.AddEnum("acme", "zh-TW", "Order", "OrderStatus", ("9", "客製狀態"), ("0", "初稿"));
            var svc = new LanguageService(defineAccess, reader);

            var result = svc.GetLangEnum("acme", "zh-TW", "Order", "OrderStatus");

            Assert.NotNull(result);
            Assert.Equal(s_custOrderCodes, result!.Entries.Select(e => e.Code).ToArray());
            Assert.Equal(s_custOrderTexts, result.Entries.Select(e => e.Text).ToArray());
        }

        [Fact]
        [DisplayName("Enum overlay: returns the whole cust enum when the base does not have the enum")]
        public void GetLangEnum_BaseMissesEnum_ReturnsCustEnum()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Order", ("OK", "確定")); // The resource exists but has no enum.
            var reader = new SpyCustomizeReader();
            reader.AddEnum("acme", "zh-TW", "Order", "OrderStatus", ("0", "客製草稿"));
            var svc = new LanguageService(defineAccess, reader);

            var result = svc.GetLangEnum("acme", "zh-TW", "Order", "OrderStatus");

            Assert.NotNull(result);
            Assert.Equal("客製草稿", result!.GetText("0"));
        }

        [Fact]
        [DisplayName("Enum replacement must not pollute the base cached instance (definition data is immutable after init)")]
        public void GetLangEnum_Overlay_DoesNotMutateBaseCachedInstance()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Order", "OrderStatus", ("0", "草稿"), ("1", "待審"));
            var reader = new SpyCustomizeReader();
            reader.AddEnum("acme", "zh-TW", "Order", "OrderStatus", ("0", "初稿"), ("9", "客製狀態"));
            var svc = new LanguageService(defineAccess, reader);

            svc.GetLangEnum("acme", "zh-TW", "Order", "OrderStatus");

            // A lookup without a customizeId (another company without customization) must see no trace of the customization.
            var baseResult = svc.GetLangEnum("zh-TW", "Order", "OrderStatus");
            Assert.NotNull(baseResult);
            Assert.Equal(2, baseResult!.Entries.Count);
            Assert.Equal("草稿", baseResult.GetText("0"));
            Assert.Null(baseResult.GetText("9"));
        }

        [Fact]
        [DisplayName("Regression guard: without a customizeId the base cached instance is returned directly (not copied)")]
        public void GetLangEnum_NoCustomizeId_ReturnsBaseCachedInstance()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Order", "OrderStatus", ("0", "草稿"));
            var reader = new SpyCustomizeReader();
            reader.AddEnum("acme", "zh-TW", "Order", "OrderStatus", ("0", "初稿"));
            var svc = new LanguageService(defineAccess, reader);

            Assert.Same(
                svc.GetLangEnum("zh-TW", "Order", "OrderStatus"),
                svc.GetLangEnum("zh-TW", "Order", "OrderStatus"));
            Assert.Equal(0, reader.GetCustomizeLanguageCallCount);
        }

        [Fact]
        [DisplayName("Regression guard: when cust lacks the enum the base cached instance is returned directly (not copied)")]
        public void GetLangEnum_CustMissesEnum_ReturnsBaseCachedInstance()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Order", "OrderStatus", ("0", "草稿"));
            var reader = new SpyCustomizeReader();
            // The cust resource exists but overrides only text keys and has no OrderStatus enum.
            reader.AddLanguage("acme", "zh-TW", "Order", ("OK", "客製確定"));
            var svc = new LanguageService(defineAccess, reader);

            Assert.Same(
                svc.GetLangEnum("acme", "zh-TW", "Order", "OrderStatus"),
                svc.GetLangEnum("acme", "zh-TW", "Order", "OrderStatus"));
        }

        [Fact]
        [DisplayName("Enum overlay: returns the base enum when cust lacks the enum")]
        public void GetLangEnum_CustMissesEnum_ReturnsBaseEnum()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"), ("F", "女"));
            var reader = new SpyCustomizeReader();
            // The cust resource exists but overrides only text keys and has no Gender enum.
            reader.AddLanguage("acme", "zh-TW", "Common", ("OK", "客製確定"));
            var svc = new LanguageService(defineAccess, reader);

            var result = svc.GetLangEnum("acme", "zh-TW", "Common", "Gender");

            Assert.NotNull(result);
            Assert.Equal("男", result!.GetText("M"));
        }

        // ---- Test doubles ----

        private sealed class SpyCustomizeReader : ICustomizeDefineReader
        {
            private readonly Dictionary<string, LanguageResource> _languages = [];

            public int GetCustomizeLanguageCallCount { get; private set; }

            public void AddLanguage(string customizeId, string lang, string ns, params (string Key, string Value)[] items)
            {
                var resource = GetOrCreate(customizeId, lang, ns);
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
            }

            public void AddEnum(string customizeId, string lang, string ns, string enumName, params (string Code, string Text)[] entries)
            {
                var resource = GetOrCreate(customizeId, lang, ns);
                var langEnum = new LanguageEnum { Name = enumName };
                foreach (var (code, text) in entries)
                    langEnum.Entries.Add(code, text);
                resource.Enums.Add(langEnum);
            }

            private LanguageResource GetOrCreate(string customizeId, string lang, string ns)
            {
                string key = $"{customizeId}.{lang}.{ns}";
                if (!_languages.TryGetValue(key, out var resource))
                {
                    resource = new LanguageResource { Namespace = ns, Lang = lang };
                    _languages[key] = resource;
                }
                return resource;
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
                var resource = GetOrCreate(lang, ns);
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
            }

            public void AddEnum(string lang, string ns, string enumName, params (string Code, string Text)[] entries)
            {
                var resource = GetOrCreate(lang, ns);
                var langEnum = new LanguageEnum { Name = enumName };
                foreach (var (code, text) in entries)
                    langEnum.Entries.Add(code, text);
                resource.Enums.Add(langEnum);
            }

            private LanguageResource GetOrCreate(string lang, string ns)
            {
                string key = $"{lang}.{ns}";
                if (!_resources.TryGetValue(key, out var resource))
                {
                    resource = new LanguageResource { Namespace = ns, Lang = lang };
                    _resources[key] = resource;
                }
                return resource;
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
