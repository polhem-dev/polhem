using System.ComponentModel;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Verifies <see cref="FileEndpointStorage"/>'s caching contract: the constructor resolves the per-user file
    /// paths, the <c>Set</c> methods change the in-memory cache only, and the <c>Save</c> methods are the only ones
    /// that touch the disk.
    /// </summary>
    /// <remarks>
    /// Every test that writes uses its own temporary root through the internal constructor, so nothing lands in the
    /// real user profile and parallel runs never collide.
    /// </remarks>
    public sealed class FileEndpointStorageTests : IDisposable
    {
        private const string AppName = "polhem-ui-core-tests";
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-endpoint-storage-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // The test never wrote to disk; nothing to clean up.
            }
        }

        private FileEndpointStorage NewStorage() => new(AppName, _root);

        private string AppDirectory => Path.Combine(_root, AppName);

        [Fact]
        [DisplayName("The public constructor places both files in LocalApplicationData/<appName>")]
        public void Constructor_AppName_UsesLocalApplicationData()
        {
            var appDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

            var storage = new FileEndpointStorage(AppName);

            Assert.Equal(Path.Combine(appDirectory, "endpoint.txt"), storage.FilePath);
            Assert.Equal(Path.Combine(appDirectory, "apikey.txt"), storage.ApiKeyFilePath);
        }

        [Fact]
        [DisplayName("The constructor throws when appName is null or whitespace")]
        public void Constructor_NullOrWhitespaceAppName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FileEndpointStorage(null!));
            Assert.Throws<ArgumentException>(() => new FileEndpointStorage("   "));
        }

        [Fact]
        [DisplayName("LoadEndpoint returns an empty string when the file does not exist")]
        public void LoadEndpoint_MissingFile_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, NewStorage().LoadEndpoint());
        }

        [Fact]
        [DisplayName("SetEndpoint updates only the in-memory cache without writing to disk")]
        public void SetEndpoint_CachesInMemoryWithoutTouchingDisk()
        {
            var storage = NewStorage();

            storage.SetEndpoint("https://api.example.com");

            Assert.Equal("https://api.example.com", storage.LoadEndpoint());
            Assert.False(File.Exists(storage.FilePath));
        }

        [Fact]
        [DisplayName("SaveEndpoint creates the directory and writes the file, and a new instance reads it back")]
        public void SaveEndpoint_WritesFile_NewInstanceReadsItBack()
        {
            NewStorage().SaveEndpoint("https://api.example.com/jsonrpc");

            var reloaded = NewStorage();
            Assert.True(File.Exists(reloaded.FilePath));
            Assert.Equal("https://api.example.com/jsonrpc", reloaded.LoadEndpoint());
        }

        [Fact]
        [DisplayName("LoadEndpoint trims leading and trailing whitespace from the file content")]
        public void LoadEndpoint_TrimsFileContent()
        {
            var storage = NewStorage();
            Directory.CreateDirectory(AppDirectory);
            File.WriteAllText(storage.FilePath, "  https://api.example.com \n");

            Assert.Equal("https://api.example.com", storage.LoadEndpoint());
        }

        [Fact]
        [DisplayName("LoadApiKey returns an empty string when nothing has been written yet")]
        public void LoadApiKey_NoFile_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, NewStorage().LoadApiKey());
        }

        [Fact]
        [DisplayName("SetApiKey changes only the in-memory cache without writing; SaveApiKey writes the file")]
        public void SetApiKey_DoesNotTouchDisk_SaveApiKeyDoes()
        {
            var storage = NewStorage();

            storage.SetApiKey("cached-only.secret");
            Assert.Equal("cached-only.secret", storage.LoadApiKey());
            Assert.False(File.Exists(storage.ApiKeyFilePath));

            storage.SaveApiKey("persisted.secret");
            Assert.Equal("persisted.secret", File.ReadAllText(storage.ApiKeyFilePath));
        }

        [Fact]
        [DisplayName("SaveApiKey writes apikey.txt readable by its owner only, even over a world-readable file")]
        public void SaveApiKey_WritesOwnerOnlyFile()
        {
            var storage = NewStorage();
            Directory.CreateDirectory(AppDirectory);
            File.WriteAllText(storage.ApiKeyFilePath, "old.secret");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(storage.ApiKeyFilePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }

            storage.SaveApiKey("app-id.secret");

            Assert.Equal("app-id.secret", File.ReadAllText(storage.ApiKeyFilePath));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(storage.ApiKeyFilePath));
            }
        }

        [Fact]
        [DisplayName("After saving both values, a new instance reads each back from its own file")]
        public void SaveEndpointAndApiKey_RoundTripIndependently()
        {
            var writer = NewStorage();
            writer.SaveEndpoint("http://host:5100/api");
            writer.SaveApiKey("app-id.secret");

            var reader = NewStorage();
            Assert.Equal("http://host:5100/api", reader.LoadEndpoint());
            Assert.Equal("app-id.secret", reader.LoadApiKey());
            Assert.Equal("http://host:5100/api", File.ReadAllText(reader.FilePath));
            Assert.Equal("app-id.secret", File.ReadAllText(reader.ApiKeyFilePath));
        }
    }
}
