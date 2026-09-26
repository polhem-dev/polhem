using System.ComponentModel;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Path validation tests for ApiConnectValidator. Each case restores
    /// <see cref="ApiClientInfo.SupportedConnectTypes"/> with try/finally, and tests within the class run serially
    /// (the xUnit default). The race risk with other test classes: <c>ApiClientInfoTests</c> mutates the same
    /// static, and although both snapshot and restore, they are both in
    /// <c>[Collection("ApiClientInfoState")]</c> and run serially, so parallel classes do not race on the static.
    /// </summary>
    [Collection("ApiClientInfoState")]
    public class ApiConnectValidatorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("ApiConnectValidator.ValidateAsync throws ArgumentException for a blank endpoint")]
        public async Task ValidateAsync_EmptyEndpoint_ThrowsArgumentException(string? endpoint)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => ApiConnectValidator.ValidateAsync(endpoint!));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException for an unrecognized format")]
        public async Task ValidateAsync_UnknownFormat_ThrowsInvalidOperationException(string endpoint)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ApiConnectValidator.ValidateAsync(endpoint));
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException for a local path when Local is not supported")]
        public async Task ValidateAsync_LocalPath_NotSupported_ThrowsInvalidOperationException()
        {
            var original = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => ApiConnectValidator.ValidateAsync(@"C:\FakePath_NoLocalSupport"));
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = original;
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws ArgumentException when the local path does not exist")]
        public async Task ValidateAsync_LocalPath_NotExists_ThrowsArgumentException()
        {
            var original = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                await Assert.ThrowsAsync<ArgumentException>(
                    () => ApiConnectValidator.ValidateAsync(@"C:\NonExistent_polhem_test_abc123"));
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = original;
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws FileNotFoundException when the local path exists but has no SystemSettings.xml")]
        public async Task ValidateAsync_LocalPath_MissingSystemSettings_ThrowsFileNotFoundException()
        {
            // This test relies on Windows-style paths (drive:\) and the real file system, so it is skipped on Linux CI.
            if (!OperatingSystem.IsWindows()) return;

            var tempDir = Path.Combine(Path.GetTempPath(), "polhem_api_client_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var originalSupported = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                await Assert.ThrowsAsync<FileNotFoundException>(
                    () => ApiConnectValidator.ValidateAsync(tempDir));
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = originalSupported;
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync with allowGenerateSettings creates SystemSettings.xml and DatabaseSettings.xml")]
        public async Task ValidateAsync_LocalPath_AllowGenerateSettings_CreatesFiles()
        {
            // This test relies on Windows-style paths and writing to the real file system.
            if (!OperatingSystem.IsWindows()) return;

            var tempDir = Path.Combine(Path.GetTempPath(), "polhem_api_client_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var originalSupported = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                var result = await ApiConnectValidator.ValidateAsync(tempDir, allowGenerateSettings: true);

                Assert.Equal(ConnectType.Local, result);
                Assert.True(File.Exists(Path.Combine(tempDir, "SystemSettings.xml")));
                Assert.True(File.Exists(Path.Combine(tempDir, "DatabaseSettings.xml")));
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = originalSupported;
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException for a URL when Remote is not supported")]
        public async Task ValidateAsync_RemoteUrl_RemoteNotSupported_ThrowsInvalidOperationException()
        {
            var original = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Local;
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => ApiConnectValidator.ValidateAsync("http://example.com/api"));
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = original;
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException reporting endpoint not reachable for an unreachable URL")]
        public async Task ValidateAsync_RemoteUrl_NotReachable_ThrowsEndpointNotReachable()
        {
            var original = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                // Port 1 on 127.0.0.1 is reserved and nothing listens on it locally, so the pre-check fails.
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => ApiConnectValidator.ValidateAsync("http://127.0.0.1:1/jsonrpc/api"));
                Assert.Contains("Endpoint not reachable", ex.Message);
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = original;
            }
        }
    }
}
