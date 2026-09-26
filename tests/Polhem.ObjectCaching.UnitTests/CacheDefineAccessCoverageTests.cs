using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Gap coverage of <see cref="CacheDefineAccess"/>: the constructor null guards and the Get, Save, GetDefine
    /// and SaveDefine dispatch paths of CurrencySettings and UnitSettings.
    /// Each test is isolated by its own TempDir and a unique cache prefix.
    /// </summary>
    public sealed class CacheDefineAccessCoverageTests
    {
        private sealed class TempDir : IDisposable
        {
            public PathOptions Options { get; }
            private readonly string _path;

            public TempDir()
            {
                _path = Path.Combine(Path.GetTempPath(), $"polhem-cov-{Guid.NewGuid():N}");
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
            var cache = new CacheContainerService(storage, paths, "cov_" + Guid.NewGuid().ToString("N"));
            return new CacheDefineAccess(storage, paths, cache, Array.Empty<byte>());
        }

        // ── Constructor null guards ───────────────────────────────────────────

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null storage")]
        public void Ctor_NullStorage_ThrowsArgumentNullException()
        {
            using var temp = new TempDir();
            var storage = new FileDefineStorage(temp.Options);
            var cache = new CacheContainerService(storage, temp.Options, "cov_" + Guid.NewGuid().ToString("N"));

            Assert.Throws<ArgumentNullException>(() =>
                new CacheDefineAccess(null!, temp.Options, cache, Array.Empty<byte>()));
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for null paths")]
        public void Ctor_NullPaths_ThrowsArgumentNullException()
        {
            using var temp = new TempDir();
            var storage = new FileDefineStorage(temp.Options);
            var cache = new CacheContainerService(storage, temp.Options, "cov_" + Guid.NewGuid().ToString("N"));

            Assert.Throws<ArgumentNullException>(() =>
                new CacheDefineAccess(storage, null!, cache, Array.Empty<byte>()));
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null cache")]
        public void Ctor_NullCache_ThrowsArgumentNullException()
        {
            using var temp = new TempDir();
            var storage = new FileDefineStorage(temp.Options);

            Assert.Throws<ArgumentNullException>(() =>
                new CacheDefineAccess(storage, temp.Options, null!, Array.Empty<byte>()));
        }

        [Fact]
        [DisplayName("Constructor tolerates a null configEncryptionKey by using an empty array and does not throw")]
        public void Ctor_NullConfigEncryptionKey_DoesNotThrow()
        {
            using var temp = new TempDir();
            var storage = new FileDefineStorage(temp.Options);
            var cache = new CacheContainerService(storage, temp.Options, "cov_" + Guid.NewGuid().ToString("N"));

            var exception = Record.Exception(() =>
                new CacheDefineAccess(storage, temp.Options, cache, null!));

            Assert.Null(exception);
        }

        // ── CurrencySettings ──────────────────────────────────────────────────

        [Fact]
        [DisplayName("SaveCurrencySettings writes CurrencySettings.xml through the DefineStorage")]
        public void SaveCurrencySettings_WritesFile()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            access.SaveCurrencySettings([]);

            Assert.True(File.Exists(temp.Options.GetCurrencySettingsFilePath()));
        }

        [Fact]
        [DisplayName("GetCurrencySettings after a save returns a CurrencySettings instance")]
        public void GetCurrencySettings_AfterSave_ReturnsInstance()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SaveCurrencySettings([]);

            var result = access.GetCurrencySettings();

            Assert.NotNull(result);
        }

        [Fact]
        [DisplayName("GetDefine(CurrencySettings) delegates to GetCurrencySettings and returns CurrencySettings")]
        public void GetDefine_CurrencySettings_ReturnsCurrencySettings()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SaveCurrencySettings([]);

            var result = access.GetDefine(DefineType.CurrencySettings);

            Assert.IsType<CurrencySettings>(result);
        }

        [Fact]
        [DisplayName("SaveDefine(CurrencySettings) delegates to SaveCurrencySettings and writes the file")]
        public void SaveDefine_CurrencySettings_DelegatesToSaveCurrencySettings()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            access.SaveDefine(DefineType.CurrencySettings, new CurrencySettings());

            Assert.True(File.Exists(temp.Options.GetCurrencySettingsFilePath()));
        }

        // ── UnitSettings ──────────────────────────────────────────────────────

        [Fact]
        [DisplayName("SaveUnitSettings writes UnitSettings.xml through the DefineStorage")]
        public void SaveUnitSettings_WritesFile()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            access.SaveUnitSettings([]);

            Assert.True(File.Exists(temp.Options.GetUnitSettingsFilePath()));
        }

        [Fact]
        [DisplayName("GetUnitSettings after a save returns a UnitSettings instance")]
        public void GetUnitSettings_AfterSave_ReturnsInstance()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SaveUnitSettings([]);

            var result = access.GetUnitSettings();

            Assert.NotNull(result);
        }

        [Fact]
        [DisplayName("GetDefine(UnitSettings) delegates to GetUnitSettings and returns UnitSettings")]
        public void GetDefine_UnitSettings_ReturnsUnitSettings()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);
            access.SaveUnitSettings([]);

            var result = access.GetDefine(DefineType.UnitSettings);

            Assert.IsType<UnitSettings>(result);
        }

        [Fact]
        [DisplayName("SaveDefine(UnitSettings) delegates to SaveUnitSettings and writes the file")]
        public void SaveDefine_UnitSettings_DelegatesToSaveUnitSettings()
        {
            using var temp = new TempDir();
            var access = CreateAccess(temp.Options);

            access.SaveDefine(DefineType.UnitSettings, new UnitSettings());

            Assert.True(File.Exists(temp.Options.GetUnitSettingsFilePath()));
        }
    }
}
