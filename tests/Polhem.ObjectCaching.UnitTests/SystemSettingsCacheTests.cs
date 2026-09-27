using System.ComponentModel;
using Polhem.Definition;
using Polhem.ObjectCaching.Define;
using Polhem.Tests.Shared;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Pure logic tests of <see cref="SystemSettingsCache"/>. The cache instance is constructed with a
    /// <see cref="PathOptions"/> pointing to an empty directory or the shared fixture path, so no process-wide
    /// static state is touched.
    /// </summary>
    public class SystemSettingsCacheTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public SystemSettingsCacheTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("CreateInstance throws FileNotFoundException when SystemSettings.xml does not exist")]
        public void CreateInstance_FileMissing_ThrowsFileNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-syscache-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var paths = new PathOptions { DefinePath = tempDir };
                var cache = new SystemSettingsCache(paths, cachePrefix: $"sc_{Guid.NewGuid():N}");

                Assert.Throws<FileNotFoundException>(() => cache.Get());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("Get returns a non-null object and runs GetPolicy when SystemSettings.xml exists")]
        public void Get_FileExists_ReturnsSettings()
        {
            // The fixture points to tests/Define/ by default, which contains SystemSettings.xml.
            // This makes sure `GetPolicy`, including its `ChangeMonitorFilePaths` setting, is covered.
            var cache = new SystemSettingsCache(_fx.PathOptions, cachePrefix: $"sc_{Guid.NewGuid():N}");

            var result = cache.Get();

            // Values from `tests/Define/SystemSettings.xml`.
            Assert.Equal("1.0.0", result!.CommonConfiguration.Version);
        }
    }
}
