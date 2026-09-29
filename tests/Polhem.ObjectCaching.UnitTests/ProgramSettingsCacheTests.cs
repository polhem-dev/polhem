using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.ObjectCaching.Define;

namespace Polhem.ObjectCaching.UnitTests
{
    public class ProgramSettingsCacheTests
    {
        [Fact]
        [DisplayName("Get deserializes and returns ProgramSettings when the file exists (also covers the GetPolicy path)")]
        public void Get_FileExists_ReturnsProgramSettings()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"polhem-psc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var pathOptions = new PathOptions { DefinePath = tempDir };
                XmlCodec.SerializeToFile(new ProgramSettings(), pathOptions.GetProgramSettingsFilePath());

                // Goes through the file back end of `FileDefineStorage`.
                // A unique prefix avoids sharing cache keys with other tests.
                var storage = new FileDefineStorage(pathOptions);
                string cachePrefix = Guid.NewGuid().ToString("N");
                var cache = new ProgramSettingsCache(storage, cachePrefix);

                var result = cache.Get();

                Assert.NotNull(result);
                cache.Remove();
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { /* best effort */ }
            }
        }

        [Fact]
        [DisplayName("Constructor throws ArgumentNullException for a null storage")]
        public void Constructor_NullStorage_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgramSettingsCache(null!));
        }
    }
}
