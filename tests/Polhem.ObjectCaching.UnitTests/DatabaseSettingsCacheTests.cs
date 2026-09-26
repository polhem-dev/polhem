using System.ComponentModel;
using Polhem.Definition;
using Polhem.ObjectCaching.Define;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Pure logic tests of <see cref="DatabaseSettingsCache"/>. The cache instance is constructed with a
    /// <see cref="PathOptions"/> pointing to an empty directory, so no process-wide static state is touched.
    /// </summary>
    public class DatabaseSettingsCacheTests
    {
        [Fact]
        [DisplayName("CreateInstance throws FileNotFoundException when DatabaseSettings.xml does not exist")]
        public void CreateInstance_FileMissing_ThrowsFileNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-dbcache-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var paths = new PathOptions { DefinePath = tempDir };
                // A per-test cache prefix keeps this instance from interfering with the caches of other fixtures.
                var cache = new DatabaseSettingsCache(paths, cachePrefix: $"dbc_{Guid.NewGuid():N}");

                Assert.Throws<FileNotFoundException>(() => cache.Get());
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }
    }
}
