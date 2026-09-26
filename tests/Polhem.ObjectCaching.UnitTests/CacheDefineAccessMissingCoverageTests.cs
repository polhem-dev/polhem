using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Paths of <see cref="CacheDefineAccess"/> not covered elsewhere: the GetDefine dispatch for ProgramSettings,
    /// MenuSettings, PermissionModels, Language and FormLayout, and the write paths of SaveMenuSettings,
    /// SavePermissionModels and SaveLanguage. Each test is isolated by its own TempDir and a unique cache prefix.
    /// </summary>
    public class CacheDefineAccessMissingCoverageTests
    {
        private static readonly string[] s_zhTwCommonKeys = { "zh-TW", "Common" };
        private static readonly string[] s_testLayoutKey = { "TestLayout" };

        private sealed class TempDir : IDisposable
        {
            public PathOptions Options { get; }
            private readonly string _path;

            public TempDir()
            {
                _path = Path.Combine(Path.GetTempPath(), $"polhem-mcov-{Guid.NewGuid():N}");
                Directory.CreateDirectory(_path);
                Options = new PathOptions { DefinePath = _path };
            }

            public void Dispose()
            {
                try { Directory.Delete(_path, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        private static CacheDefineAccess CreateAccess(PathOptions paths)
        {
            var storage = new FileDefineStorage(paths);
            var cache = new CacheContainerService(storage, paths, "mcov_" + Guid.NewGuid().ToString("N"));
            return new CacheDefineAccess(storage, paths, cache, Array.Empty<byte>());
        }

        // ── ProgramSettings ──────────────────────────────────────────────────

        [Fact]
        [DisplayName("GetProgramSettings after a save returns a ProgramSettings instance")]
        public void GetProgramSettings_AfterSave_ReturnsInstance()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SaveProgramSettings(new ProgramSettings());

            var result = access.GetProgramSettings();

            Assert.NotNull(result);
        }

        [Fact]
        [DisplayName("GetDefine(ProgramSettings) delegates to GetProgramSettings and returns ProgramSettings")]
        public void GetDefine_ProgramSettings_ReturnsProgramSettings()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SaveProgramSettings(new ProgramSettings());

            var result = access.GetDefine(DefineType.ProgramSettings);

            Assert.IsType<ProgramSettings>(result);
        }

        // ── MenuSettings ─────────────────────────────────────────────────────

        [Fact]
        [DisplayName("SaveMenuSettings writes MenuSettings.xml and GetDefine returns a MenuSettings instance")]
        public void SaveAndGetMenuSettings_RoundTrips()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var settings = new MenuSettings();
            settings.Items!.AddFolder("sales", "銷售").Items!.AddEntry("order", "Order", "訂單");

            access.SaveMenuSettings(settings);

            Assert.True(File.Exists(temp.Options.GetMenuSettingsFilePath()));
            var result = Assert.IsType<MenuSettings>(access.GetDefine(DefineType.MenuSettings));
            Assert.NotNull(result.FindNode("order"));
        }

        [Fact]
        [DisplayName("GetMenuSettings returns an empty menu rather than null when MenuSettings.xml does not exist")]
        public void GetMenuSettings_NoFile_ReturnsEmptyMenu()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            var result = access.GetMenuSettings();

            Assert.NotNull(result);
            Assert.Empty(result.Items!);
        }

        [Fact]
        [DisplayName("GetMenuSettings(customizeId) without a customization code returns the base menu")]
        public void GetMenuSettings_EmptyCustomizeId_ReturnsBase()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var settings = new MenuSettings();
            settings.Items!.AddEntry("order", "Order", "訂單");
            access.SaveMenuSettings(settings);

            var result = access.GetMenuSettings(string.Empty);

            Assert.NotNull(result.FindNode("order"));
        }

        // ── PermissionModels ─────────────────────────────────────────────────

        [Fact]
        [DisplayName("SavePermissionModels writes PermissionModels.xml")]
        public void SavePermissionModels_WritesFile()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            access.SavePermissionModels(new PermissionModels());

            Assert.True(File.Exists(temp.Options.GetPermissionModelsFilePath()));
        }

        [Fact]
        [DisplayName("GetPermissionModels after a save returns a PermissionModels instance")]
        public void GetPermissionModels_AfterSave_ReturnsInstance()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SavePermissionModels(new PermissionModels());

            var result = access.GetPermissionModels();

            Assert.NotNull(result);
        }

        [Fact]
        [DisplayName("GetDefine(PermissionModels) delegates to GetPermissionModels and returns PermissionModels")]
        public void GetDefine_PermissionModels_ReturnsPermissionModels()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SavePermissionModels(new PermissionModels());

            var result = access.GetDefine(DefineType.PermissionModels);

            Assert.IsType<PermissionModels>(result);
        }

        [Fact]
        [DisplayName("SaveDefine(PermissionModels) delegates to SavePermissionModels and writes the file")]
        public void SaveDefine_PermissionModels_DelegatesToSavePermissionModels()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            access.SaveDefine(DefineType.PermissionModels, new PermissionModels());

            Assert.True(File.Exists(temp.Options.GetPermissionModelsFilePath()));
        }

        // ── Language ─────────────────────────────────────────────────────────

        [Fact]
        [DisplayName("SaveLanguage writes the Language xml under the matching language folder")]
        public void SaveLanguage_WritesFile()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var resource = new LanguageResource { Lang = "zh-TW", Namespace = "Common" };

            access.SaveLanguage(resource);

            Assert.True(File.Exists(temp.Options.GetLanguageFilePath("zh-TW", "Common")));
        }

        [Fact]
        [DisplayName("GetLanguage after a save returns a LanguageResource instance")]
        public void GetLanguage_AfterSave_ReturnsLanguageResource()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var resource = new LanguageResource { Lang = "zh-TW", Namespace = "Common" };
            access.SaveLanguage(resource);

            var result = access.GetLanguage("zh-TW", "Common");

            Assert.NotNull(result);
        }

        [Fact]
        [DisplayName("GetDefine(Language) with valid keys delegates to GetLanguage and returns a LanguageResource")]
        public void GetDefine_Language_WithValidKeys_ReturnsLanguageResource()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var resource = new LanguageResource { Lang = "zh-TW", Namespace = "Common" };
            access.SaveLanguage(resource);

            var result = access.GetDefine(DefineType.Language, s_zhTwCommonKeys);

            Assert.IsType<LanguageResource>(result);
        }

        [Fact]
        [DisplayName("SaveDefine(Language) delegates to SaveLanguage and writes the language xml")]
        public void SaveDefine_Language_DelegatesToSaveLanguage()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var resource = new LanguageResource { Lang = "en-US", Namespace = "Sys" };

            access.SaveDefine(DefineType.Language, resource);

            Assert.True(File.Exists(temp.Options.GetLanguageFilePath("en-US", "Sys")));
        }

        // ── FormLayout dispatch ───────────────────────────────────────────────

        [Fact]
        [DisplayName("GetDefine(FormLayout) with a valid key delegates to GetFormLayout and returns a FormLayout")]
        public void GetDefine_FormLayout_WithValidKey_ReturnsFormLayout()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            var layout = new FormLayout { LayoutId = "TestLayout" };
            access.SaveFormLayout(layout);

            var result = access.GetDefine(DefineType.FormLayout, s_testLayoutKey);

            Assert.IsType<FormLayout>(result);
        }
    }
}
