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
    /// Behavior tests for <see cref="LanguageService"/>: key parsing, hits, default language fallback, and returning the key as the last resort.
    /// Uses an in-memory stub <see cref="IDefineAccess"/>, so no file system is needed.
    /// </summary>
    public class LanguageServiceTests
    {
        [Fact]
        [DisplayName("GetLangText returns the value on a hit in the current language")]
        public void GetLangText_HitInRequestedLang_ReturnsValue()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("確定", svc.GetLangText("zh-TW", "Common.OK"));
        }

        [Fact]
        [DisplayName("GetLangText splits namespace and subKey at the first '.'")]
        public void GetLangText_SplitsOnFirstDot()
        {
            var defineAccess = new StubDefineAccess("en-US");
            // The subKey contains '.', so the split must happen at the first '.'.
            defineAccess.AddResource("zh-TW", "Customer", ("Field.Name.Caption", "客戶名稱"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("客戶名稱", svc.GetLangText("zh-TW", "Customer.Field.Name.Caption"));
        }

        [Fact]
        [DisplayName("GetLangText falls back to the default language when a translation is missing")]
        public void GetLangText_FallsBackToDefaultLang()
        {
            var defineAccess = new StubDefineAccess("en-US");
            // Missing in zh-TW, present in en-US.
            defineAccess.AddResource("en-US", "Common", ("OK", "OK"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("OK", svc.GetLangText("zh-TW", "Common.OK"));
        }

        [Fact]
        [DisplayName("GetLangText returns the fullKey when the default language is missing the translation too")]
        public void GetLangText_BothLanguagesMiss_ReturnsFullKey()
        {
            var defineAccess = new StubDefineAccess("en-US");
            // Neither language has this key.
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("Common.OK", svc.GetLangText("zh-TW", "Common.OK"));
        }

        [Fact]
        [DisplayName("GetLangText with an explicit namespace + subKey gives the same result as the fullKey form")]
        public void GetLangText_ExplicitNamespace_MatchesFullKey()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "Customer", ("Field.Name.Caption", "客戶名稱"));
            var svc = new LanguageService(defineAccess, null);

            string viaFullKey = svc.GetLangText("zh-TW", "Customer.Field.Name.Caption");
            string viaExplicit = svc.GetLangText("zh-TW", "Customer", "Field.Name.Caption");

            Assert.Equal(viaFullKey, viaExplicit);
            Assert.Equal("客戶名稱", viaExplicit);
        }

        [Fact]
        [DisplayName("TryGetLangText returns true and the value on a hit")]
        public void TryGetLangText_Hit_ReturnsTrue()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);

            bool ok = svc.TryGetLangText("zh-TW", "Common.OK", out string text);

            Assert.True(ok);
            Assert.Equal("確定", text);
        }

        [Fact]
        [DisplayName("TryGetLangText returns false and an empty string on a miss (no fallback)")]
        public void TryGetLangText_Miss_ReturnsFalseAndEmpty_NoFallback()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddResource("en-US", "Common", ("OK", "OK")); // Only en-US has it.
            var svc = new LanguageService(defineAccess, null);

            bool ok = svc.TryGetLangText("zh-TW", "Common.OK", out string text);

            Assert.False(ok);
            Assert.Equal(string.Empty, text);
            // `TryGetLangText` does no default language fallback, because the fallback is the job of `GetLangText`.
        }

        [Fact]
        [DisplayName("GetLangText does not throw for a namespace that does not exist and returns the fullKey")]
        public void GetLangText_MissingNamespace_ReturnsFullKey()
        {
            var defineAccess = new StubDefineAccess("en-US"); // No resources at all.
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("Nonexistent.Foo", svc.GetLangText("zh-TW", "Nonexistent.Foo"));
        }

        [Fact]
        [DisplayName("GetLangText does not look up twice when the default language equals the current language")]
        public void GetLangText_LangEqualsDefault_NoDoubleLookup()
        {
            var defineAccess = new StubDefineAccess("zh-TW"); // The default is zh-TW.
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("確定", svc.GetLangText("zh-TW", "Common.OK"));
            Assert.Equal(1, defineAccess.GetLanguageCallCount);
        }

        [Fact]
        [DisplayName("GetLangEnum returns the matching LanguageEnum on a hit")]
        public void GetLangEnum_Hit_ReturnsEnum()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"), ("F", "女"));
            var svc = new LanguageService(defineAccess, null);

            var langEnum = svc.GetLangEnum("zh-TW", "Common.Gender");

            Assert.NotNull(langEnum);
            Assert.Equal("Gender", langEnum!.Name);
            Assert.Equal(2, langEnum.Entries.Count);
            Assert.Equal("男", langEnum.GetText("M"));
        }

        [Fact]
        [DisplayName("GetLangEnum falls back to the default language on a miss")]
        public void GetLangEnum_FallsBackToDefaultLang()
        {
            var defineAccess = new StubDefineAccess("en-US");
            // Missing in zh-TW, present in en-US.
            defineAccess.AddEnum("en-US", "Common", "Gender", ("M", "Male"), ("F", "Female"));
            var svc = new LanguageService(defineAccess, null);

            var langEnum = svc.GetLangEnum("zh-TW", "Common.Gender");

            Assert.NotNull(langEnum);
            Assert.Equal("Male", langEnum!.GetText("M"));
        }

        [Fact]
        [DisplayName("GetLangEnum returns null when both languages are missing it")]
        public void GetLangEnum_AllMiss_ReturnsNull()
        {
            var defineAccess = new StubDefineAccess("en-US");
            var svc = new LanguageService(defineAccess, null);

            Assert.Null(svc.GetLangEnum("zh-TW", "Common.Gender"));
        }

        [Fact]
        [DisplayName("GetLangEnum with an explicit namespace / enumName gives the same result as the fullName")]
        public void GetLangEnum_ExplicitNamespace_MatchesFullName()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"), ("F", "女"));
            var svc = new LanguageService(defineAccess, null);

            var viaFullName = svc.GetLangEnum("zh-TW", "Common.Gender");
            var viaExplicit = svc.GetLangEnum("zh-TW", "Common", "Gender");

            Assert.NotNull(viaExplicit);
            Assert.Equal(viaFullName!.Name, viaExplicit!.Name);
            Assert.Equal(viaFullName.Entries.Count, viaExplicit.Entries.Count);
        }

        [Fact]
        [DisplayName("GetLangEnumText returns the text of the matching entry on a hit")]
        public void GetLangEnumText_Hit_ReturnsText()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"), ("F", "女"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("男", svc.GetLangEnumText("zh-TW", "Common.Gender", "M"));
        }

        [Fact]
        [DisplayName("GetLangEnumText returns null on a miss")]
        public void GetLangEnumText_Miss_ReturnsNull()
        {
            var defineAccess = new StubDefineAccess("en-US");
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Null(svc.GetLangEnumText("zh-TW", "Common.Gender", "X"));
        }

        [Fact]
        [DisplayName("GetLangText treats a fullKey without a dot as the namespace, and returns the namespace. format on a miss")]
        public void GetLangText_FullKeyWithNoDot_ReturnsFallbackKey()
        {
            var defineAccess = new StubDefineAccess("en-US");
            var svc = new LanguageService(defineAccess, null);
            var result = svc.GetLangText("zh-TW", "NoDotKey");
            Assert.Equal("NoDotKey.", result);
        }

        [Fact]
        [DisplayName("GetLangEnum returns null without any lookup when namespace is whitespace")]
        public void GetLangEnum_BlankNamespace_ReturnsNull()
        {
            var defineAccess = new StubDefineAccess("en-US");
            var svc = new LanguageService(defineAccess, null);
            Assert.Null(svc.GetLangEnum("zh-TW", "  ", "Gender"));
        }

        [Fact]
        [DisplayName("GetLangEnum returns null without any lookup when enumName is whitespace")]
        public void GetLangEnum_BlankEnumName_ReturnsNull()
        {
            var defineAccess = new StubDefineAccess("en-US");
            var svc = new LanguageService(defineAccess, null);
            Assert.Null(svc.GetLangEnum("zh-TW", "Common", " "));
        }

        [Fact]
        [DisplayName("GetLangText skips the default-language lookup and returns the fullKey when the system default language is an empty string")]
        public void GetLangText_EmptyDefaultLang_ReturnsFallbackKeyWithoutFallbackLookup()
        {
            var defineAccess = new StubDefineAccess("");
            var svc = new LanguageService(defineAccess, null);
            var result = svc.GetLangText("zh-TW", "Common.OK");
            Assert.Equal("Common.OK", result);
            // zh-TW and its parent zh; no third lookup for a default language.
            Assert.Equal(2, defineAccess.GetLanguageCallCount);
        }

        [Fact]
        [DisplayName("GetLangText resolves from a parent culture before the default language")]
        public void GetLangText_ParentCulture_BeatsDefaultLang()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("en", "Common", ("OK", "Okay"));
            defineAccess.AddResource("zh-TW", "Common", ("OK", "確定"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("Okay", svc.GetLangText("en-GB", "Common.OK"));
        }

        [Fact]
        [DisplayName("GetLangEnum resolves from a parent culture before the default language")]
        public void GetLangEnum_ParentCulture_BeatsDefaultLang()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddEnum("en", "Common", "Gender", ("M", "Male"));
            defineAccess.AddEnum("zh-TW", "Common", "Gender", ("M", "男"));
            var svc = new LanguageService(defineAccess, null);

            Assert.Equal("Male", svc.GetLangEnumText("en-GB", "Common.Gender", "M"));
        }

        [Fact]
        [DisplayName("TryResolveLangText reports a miss instead of returning the key when no culture of the chain declares it")]
        public void TryResolveLangText_MissEverywhere_ReturnsFalse()
        {
            var defineAccess = new StubDefineAccess("zh-TW");
            defineAccess.AddResource("zh-TW", "Common", ("Cancel", "取消"));
            var svc = new LanguageService(defineAccess, null);

            Assert.False(svc.TryResolveLangText("", "fr-FR", "Common", "OK", out string text));
            Assert.Empty(text);
            Assert.True(svc.TryResolveLangText("", "fr-FR", "Common", "Cancel", out text));
            Assert.Equal("取消", text);
        }

        [Fact]
        [DisplayName("DefaultLanguage reads CommonConfiguration.DefaultLanguage")]
        public void DefaultLanguage_ReadsCommonConfiguration()
        {
            var svc = new LanguageService(new StubDefineAccess("de-DE"), null);

            Assert.Equal("de-DE", svc.DefaultLanguage);
        }

        private sealed class StubDefineAccess : IDefineAccess
        {
            private readonly Dictionary<string, LanguageResource> _resources = [];
            private readonly SystemSettings _systemSettings;

            public StubDefineAccess(string defaultLang)
            {
                _systemSettings = new SystemSettings();
                _systemSettings.CommonConfiguration.DefaultLanguage = defaultLang;
            }

            public int GetLanguageCallCount { get; private set; }

            public void AddResource(string lang, string ns, params (string Key, string Value)[] items)
            {
                var resource = new LanguageResource { Namespace = ns, Lang = lang };
                foreach (var (key, value) in items)
                    resource.Items.Add(key, value);
                _resources[$"{lang}.{ns}"] = resource;
            }

            public void AddEnum(string lang, string ns, string enumName, params (string Code, string Text)[] entries)
            {
                string key = $"{lang}.{ns}";
                if (!_resources.TryGetValue(key, out var resource))
                {
                    resource = new LanguageResource { Namespace = ns, Lang = lang };
                    _resources[key] = resource;
                }
                var langEnum = new LanguageEnum { Name = enumName };
                foreach (var (code, text) in entries)
                    langEnum.Entries.Add(code, text);
                resource.Enums.Add(langEnum);
            }

            public LanguageResource GetLanguage(string lang, string ns)
            {
                GetLanguageCallCount++;
                return _resources.TryGetValue($"{lang}.{ns}", out var r) ? r : null!;
            }

            public SystemSettings GetSystemSettings() => _systemSettings;

            // Members we don't exercise in these tests:
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
