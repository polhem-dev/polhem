using System.ComponentModel;
using Polhem.Api.Client.Definitions;
using Polhem.Definition.Language;

namespace Polhem.Api.Client.UnitTests.Definitions
{
    /// <summary>
    /// Tests for <see cref="SnapshotLanguageService"/>: the client picks values and falls back to the default
    /// language over the two-layer snapshot it already fetched, and must behave like the server-side
    /// <c>LanguageService</c> (both go through <c>CustomizeOverlay</c>).
    /// </summary>
    public class SnapshotLanguageServiceTests
    {
        [Fact]
        [DisplayName("Returns the customized value when the customization has the key")]
        public void GetLangText_CustomizeHasKey_ReturnsCustomizeValue()
        {
            var svc = Build(("zh-TW", "Common", Res(("OK", "確定")), Res(("OK", "送出"))));

            Assert.Equal("送出", svc.GetLangText("zh-TW", "Common", "OK"));
        }

        [Fact]
        [DisplayName("Falls back to the base value when the customization does not have the key")]
        public void GetLangText_CustomizeMissesKey_FallsBackToBase()
        {
            var svc = Build(("zh-TW", "Common", Res(("OK", "確定"), ("Cancel", "取消")), Res(("OK", "送出"))));

            Assert.Equal("取消", svc.GetLangText("zh-TW", "Common", "Cancel"));
        }

        [Fact]
        [DisplayName("Falls back to the default language when the requested language has no match")]
        public void GetLangText_MissingInRequestedLang_FallsBackToDefaultLang()
        {
            var svc = Build(
                defaultLang: "en-US",
                ("zh-TW", "Common", Res(), null),
                ("en-US", "Common", Res(("OK", "OK")), null));

            Assert.Equal("OK", svc.GetLangText("zh-TW", "Common", "OK"));
        }

        [Fact]
        [DisplayName("The customization layer of the default language also takes part in the fallback")]
        public void GetLangText_DefaultLangFallback_AlsoOverlaysCustomize()
        {
            var svc = Build(
                defaultLang: "en-US",
                ("zh-TW", "Common", Res(), null),
                ("en-US", "Common", Res(("OK", "OK")), Res(("OK", "Submit"))));

            Assert.Equal("Submit", svc.GetLangText("zh-TW", "Common", "OK"));
        }

        [Fact]
        [DisplayName("Returns fullKey when both layers and the fallback miss (the same last resort as the server)")]
        public void GetLangText_AllMiss_ReturnsFullKey()
        {
            var svc = Build(("zh-TW", "Common", Res(), null));

            Assert.Equal("Common.Nope", svc.GetLangText("zh-TW", "Common", "Nope"));
        }

        [Fact]
        [DisplayName("A namespace missing from the snapshot does not throw and counts as a miss")]
        public void GetLangText_NamespaceNotInSnapshot_TreatedAsMiss()
        {
            var svc = Build(("zh-TW", "Common", Res(("OK", "確定")), null));

            Assert.False(svc.TryGetLangText("zh-TW", "Unknown", "OK", out _));
            Assert.Equal("Unknown.OK", svc.GetLangText("zh-TW", "Unknown", "OK"));
        }

        [Fact]
        [DisplayName("The fullKey overload splits on the first dot")]
        public void GetLangText_FullKey_SplitsOnFirstDot()
        {
            var svc = Build(("zh-TW", "Customer", Res(("Field.sys_name.Caption", "客戶名稱")), null));

            Assert.Equal("客戶名稱", svc.GetLangText("zh-TW", "Customer.Field.sys_name.Caption"));
        }

        [Fact]
        [DisplayName("Enum: a customized enum with the same name replaces the whole set")]
        public void GetLangEnum_CustomizeHasEnum_ReplacesWholeSet()
        {
            var svc = Build(("zh-TW", "Common",
                ResEnum("Gender", ("M", "男"), ("F", "女")),
                ResEnum("Gender", ("M", "先生"))));

            var result = svc.GetLangEnum("zh-TW", "Common", "Gender");

            Assert.NotNull(result);
            Assert.Single(result!.Entries);
            Assert.Equal("先生", result.GetText("M"));
        }

        [Fact]
        [DisplayName("Enum: falls back to the default language when the requested language has no match")]
        public void GetLangEnum_MissingInRequestedLang_FallsBackToDefaultLang()
        {
            var svc = Build(
                defaultLang: "en-US",
                ("zh-TW", "Common", Res(), null),
                ("en-US", "Common", ResEnum("Gender", ("M", "Male")), null));

            Assert.Equal("Male", svc.GetLangEnum("zh-TW", "Common", "Gender")!.GetText("M"));
        }

        // ---- Fixtures ----

        private static SnapshotLanguageService Build(
            params (string Lang, string Ns, LanguageResource? Base, LanguageResource? Customize)[] entries)
            => Build(string.Empty, entries);

        [Fact]
        [DisplayName("A parent culture in the snapshot answers before the default language, for text and enums alike")]
        public void Resolve_ParentCulture_BeatsDefaultLang()
        {
            var english = ResEnum("Gender", ("M", "Male"));
            english.Items.Add("OK", "Okay");
            var chinese = ResEnum("Gender", ("M", "男"));
            chinese.Items.Add("OK", "確定");
            var svc = Build(
                defaultLang: "zh-TW",
                ("en", "Common", english, null),
                ("zh-TW", "Common", chinese, null));

            Assert.Equal("Okay", svc.GetLangText("en-GB", "Common", "OK"));
            Assert.True(svc.TryResolveLangText("", "en-GB", "Common", "OK", out string text));
            Assert.Equal("Okay", text);
            Assert.Equal("Male", svc.GetLangEnumText("en-GB", "Common.Gender", "M"));
        }

        [Fact]
        [DisplayName("With only zh-TW in the snapshot, en-GB misses (keeps base text) while fr-FR and zh-TW resolve zh-TW")]
        public void TryResolveLangText_EnglishStopsBeforeDefault()
        {
            var svc = Build(defaultLang: "zh-TW", ("zh-TW", "Common", Res(("OK", "確定")), null));

            Assert.False(svc.TryResolveLangText("", "en-GB", "Common", "OK", out _));
            Assert.True(svc.TryResolveLangText("", "fr-FR", "Common", "OK", out string fr));
            Assert.Equal("確定", fr);
            Assert.True(svc.TryResolveLangText("", "zh-TW", "Common", "OK", out string zh));
            Assert.Equal("確定", zh);
        }

        [Fact]
        [DisplayName("An enum missing in the requested culture and its parent resolves in the default language")]
        public void GetLangEnum_ParentMissing_FallsBackToDefaultLang()
        {
            var svc = Build(
                defaultLang: "zh-TW",
                ("zh-TW", "Common", ResEnum("Gender", ("M", "男")), null));

            Assert.Equal("男", svc.GetLangEnumText("fr-FR", "Common.Gender", "M"));
        }

        private static SnapshotLanguageService Build(
            string defaultLang,
            params (string Lang, string Ns, LanguageResource? Base, LanguageResource? Customize)[] entries)
        {
            var snapshot = new Dictionary<string, LanguageLayers>(StringComparer.Ordinal);
            foreach (var (lang, ns, @base, customize) in entries)
                snapshot[SnapshotLanguageService.BuildKey(lang, ns)] = new LanguageLayers(@base, customize);
            return new SnapshotLanguageService(snapshot, defaultLang);
        }

        private static LanguageResource Res(params (string Key, string Value)[] items)
        {
            var resource = new LanguageResource { Namespace = "Common", Lang = "zh-TW" };
            foreach (var (key, value) in items)
                resource.Items.Add(key, value);
            return resource;
        }

        private static LanguageResource ResEnum(string enumName, params (string Code, string Text)[] entries)
        {
            var resource = Res();
            var langEnum = new LanguageEnum { Name = enumName };
            foreach (var (code, text) in entries)
                langEnum.Entries.Add(code, text);
            resource.Enums.Add(langEnum);
            return resource;
        }
    }
}
