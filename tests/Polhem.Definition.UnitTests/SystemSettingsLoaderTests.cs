using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Tests for reading files at startup with SystemSettingsLoader.
    /// Every test that writes files uses its own temp directory (through <see cref="TempDir"/>) and touches no
    /// process-wide state.
    /// </summary>
    public class SystemSettingsLoaderTests
    {
        [Fact]
        [DisplayName("Load(string) returns a SystemSettings instance for a valid file path")]
        public void Load_ValidFile_ReturnsSettings()
        {
            using var temp = TempDir.Create();
            var filePath = Path.Combine(temp.Path, "SystemSettings.xml");
            // The values must be recognizable. Writing a **default** instance, reading it back and asserting only NotNull
            // cannot even detect the read path dropping every field.
            var original = new SystemSettings();
            original.CommonConfiguration.Version = "9.9.9";
            original.CommonConfiguration.DefaultLanguage = "zh-TW";
            original.CommonConfiguration.IsDebugMode = true;
            XmlCodec.SerializeToFile(original, filePath);

            var loaded = SystemSettingsLoader.Load(filePath);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded.BackendConfiguration);
            Assert.NotNull(loaded.CommonConfiguration);
            Assert.Equal("9.9.9", loaded.CommonConfiguration.Version);
            Assert.Equal("zh-TW", loaded.CommonConfiguration.DefaultLanguage);
            Assert.True(loaded.CommonConfiguration.IsDebugMode);
        }

        [Fact]
        [DisplayName("Load(string) throws FileNotFoundException for a file path that does not exist")]
        public void Load_FileNotFound_ThrowsFileNotFoundException()
        {
            using var temp = TempDir.Create();
            var missingPath = Path.Combine(temp.Path, "Nope.xml");

            Assert.Throws<FileNotFoundException>(() => SystemSettingsLoader.Load(missingPath));
        }

        [Fact]
        [DisplayName("Load(string) throws ArgumentNullException for a null path")]
        public void Load_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SystemSettingsLoader.Load((string)null!));
        }

        [Fact]
        [DisplayName("Load(string) throws ArgumentException for a whitespace path")]
        public void Load_WhitespacePath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SystemSettingsLoader.Load("   "));
        }

        [Fact]
        [DisplayName("Load(PathOptions) resolves SystemSettings.xml through PathOptions")]
        public void Load_WithPathOptions_ResolvesViaPathOptions()
        {
            using var temp = TempDir.Create();
            var paths = new PathOptions { DefinePath = temp.Path };
            var filePath = paths.GetSystemSettingsFilePath();
            var original = new SystemSettings();
            XmlCodec.SerializeToFile(original, filePath);

            var loaded = SystemSettingsLoader.Load(paths);

            Assert.NotNull(loaded);
            Assert.Equal(filePath, loaded.ObjectFilePath);
        }

        [Fact]
        [DisplayName("Load(PathOptions) throws ArgumentNullException for null")]
        public void Load_NullPathOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SystemSettingsLoader.Load((PathOptions)null!));
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; }

            private TempDir(string path) { Path = path; }

            public static TempDir Create()
            {
                var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"polhem-loader-{Guid.NewGuid():N}");
                Directory.CreateDirectory(dir);
                return new TempDir(dir);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                        Directory.Delete(Path, recursive: true);
                }
                catch (IOException)
                {
                    // best effort
                }
            }
        }
    }
}
