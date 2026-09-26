using System.ComponentModel;
using Polhem.UI.Avalonia.Storage;

namespace Polhem.UI.Avalonia.UnitTests.Storage
{
    /// <summary>
    /// Verifies <see cref="FileEndpointStorage"/>'s caching contract: the constructor
    /// resolves the per-user file path, <see cref="FileEndpointStorage.SetEndpoint"/>
    /// mutates the in-memory cache only, and <see cref="FileEndpointStorage.SaveEndpoint"/>
    /// is the single method that touches the disk.
    /// </summary>
    public class FileEndpointStorageTests
    {
        // Each test uses a unique app name so parallel runs never collide, and the
        // created folder under LocalApplicationData is removed in a finally block.
        private static string NewAppName() => $"polhem-avalonia-tests-{Guid.NewGuid():N}";

        private static string AppDirectory(string appName) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName);

        private static void Cleanup(string appName)
        {
            try
            {
                Directory.Delete(AppDirectory(appName), recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // The test never wrote to disk; nothing to clean up.
            }
        }

        [Fact]
        [DisplayName("ApiKeyFilePath is LocalApplicationData/<appName>/apikey.txt")]
        public void ApiKeyFilePath_CombinesLocalAppDataAndAppName()
        {
            var appName = NewAppName();
            var storage = new FileEndpointStorage(appName);

            Assert.Equal(Path.Combine(AppDirectory(appName), "apikey.txt"), storage.ApiKeyFilePath);
        }

        [Fact]
        [DisplayName("SetApiKey changes only the in-memory cache without writing; SaveApiKey writes the file")]
        public void SetApiKey_DoesNotTouchDisk_SaveApiKeyDoes()
        {
            var appName = NewAppName();
            try
            {
                var storage = new FileEndpointStorage(appName);

                storage.SetApiKey("cached-only.secret");
                Assert.Equal("cached-only.secret", storage.LoadApiKey());
                Assert.False(File.Exists(storage.ApiKeyFilePath));

                storage.SaveApiKey("persisted.secret");
                Assert.True(File.Exists(storage.ApiKeyFilePath));
                Assert.Equal("persisted.secret", File.ReadAllText(storage.ApiKeyFilePath));
            }
            finally
            {
                Cleanup(appName);
            }
        }

        [Fact]
        [DisplayName("After SaveApiKey, a new instance reads back the same key, independent of the endpoint")]
        public void SaveApiKey_RoundTripsIndependentlyOfEndpoint()
        {
            var appName = NewAppName();
            try
            {
                var writer = new FileEndpointStorage(appName);
                writer.SaveEndpoint("http://host:5100/api");
                writer.SaveApiKey("app-id.secret");

                var reader = new FileEndpointStorage(appName);
                Assert.Equal("http://host:5100/api", reader.LoadEndpoint());
                Assert.Equal("app-id.secret", reader.LoadApiKey());
            }
            finally
            {
                Cleanup(appName);
            }
        }

        [Fact]
        [DisplayName("LoadApiKey returns an empty string when nothing has been written yet")]
        public void LoadApiKey_NoFile_ReturnsEmpty()
        {
            var appName = NewAppName();
            try
            {
                Assert.Equal(string.Empty, new FileEndpointStorage(appName).LoadApiKey());
            }
            finally
            {
                Cleanup(appName);
            }
        }

        [Fact]
        [DisplayName("The constructor throws when appName is null or whitespace")]
        public void Constructor_NullOrWhitespaceAppName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FileEndpointStorage(null!));
            Assert.Throws<ArgumentException>(() => new FileEndpointStorage("   "));
        }

        [Fact]
        [DisplayName("FilePath is LocalApplicationData/<appName>/endpoint.txt")]
        public void FilePath_CombinesLocalAppDataAndAppName()
        {
            var appName = NewAppName();
            var storage = new FileEndpointStorage(appName);

            Assert.Equal(Path.Combine(AppDirectory(appName), "endpoint.txt"), storage.FilePath);
        }

        [Fact]
        [DisplayName("LoadEndpoint returns an empty string when the file does not exist")]
        public void LoadEndpoint_MissingFile_ReturnsEmpty()
        {
            var storage = new FileEndpointStorage(NewAppName());

            Assert.Equal(string.Empty, storage.LoadEndpoint());
        }

        [Fact]
        [DisplayName("SetEndpoint updates only the in-memory cache without writing to disk")]
        public void SetEndpoint_CachesInMemoryWithoutTouchingDisk()
        {
            var appName = NewAppName();
            try
            {
                var storage = new FileEndpointStorage(appName);

                storage.SetEndpoint("https://api.example.com");

                Assert.Equal("https://api.example.com", storage.LoadEndpoint());
                Assert.False(File.Exists(storage.FilePath));
            }
            finally
            {
                Cleanup(appName);
            }
        }

        [Fact]
        [DisplayName("SaveEndpoint creates the directory and writes the file, and a new instance reads it back")]
        public void SaveEndpoint_WritesFile_NewInstanceReadsItBack()
        {
            var appName = NewAppName();
            try
            {
                var storage = new FileEndpointStorage(appName);

                storage.SaveEndpoint("https://api.example.com/jsonrpc");

                Assert.True(File.Exists(storage.FilePath));
                var reloaded = new FileEndpointStorage(appName);
                Assert.Equal("https://api.example.com/jsonrpc", reloaded.LoadEndpoint());
            }
            finally
            {
                Cleanup(appName);
            }
        }

        [Fact]
        [DisplayName("LoadEndpoint trims leading and trailing whitespace from the file content")]
        public void LoadEndpoint_TrimsFileContent()
        {
            var appName = NewAppName();
            try
            {
                var storage = new FileEndpointStorage(appName);
                Directory.CreateDirectory(AppDirectory(appName));
                File.WriteAllText(storage.FilePath, "  https://api.example.com \n");

                Assert.Equal("https://api.example.com", storage.LoadEndpoint());
            }
            finally
            {
                Cleanup(appName);
            }
        }
    }
}
