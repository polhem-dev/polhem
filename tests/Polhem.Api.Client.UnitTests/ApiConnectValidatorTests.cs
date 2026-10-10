using System.ComponentModel;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Path validation tests for ApiConnectValidator.
    /// </summary>
    public class ApiConnectValidatorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("ApiConnectValidator.ValidateAsync throws ArgumentException for a blank endpoint")]
        public async Task ValidateAsync_EmptyEndpoint_ThrowsArgumentException(string? endpoint)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => ApiConnectValidator.ValidateAsync(endpoint!, SupportedConnectTypes.Both));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")]
        [InlineData("file:///srv/define")]
        [InlineData("relative/define")]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException for an unrecognized format")]
        public async Task ValidateAsync_UnknownFormat_ThrowsInvalidOperationException(string endpoint)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ApiConnectValidator.ValidateAsync(endpoint, SupportedConnectTypes.Both));
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException for a local path when Local is not supported")]
        public async Task ValidateAsync_LocalPath_NotSupported_ThrowsInvalidOperationException()
        {
            var supported = SupportedConnectTypes.Remote;
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ApiConnectValidator.ValidateAsync(@"C:\FakePath_NoLocalSupport", supported));
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws ArgumentException when the local path does not exist")]
        public async Task ValidateAsync_LocalPath_NotExists_ThrowsArgumentException()
        {
            var supported = SupportedConnectTypes.Both;
            await Assert.ThrowsAsync<ArgumentException>(
                () => ApiConnectValidator.ValidateAsync(@"C:\NonExistent_polhem_test_abc123", supported));
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync reports a missing Unix absolute path as nonexistent on macOS and Linux instead of unrecognized")]
        public async Task ValidateAsync_UnixAbsolutePath_NotExists_ThrowsArgumentExceptionOffWindows()
        {
            const string endpoint = "/nonexistent_polhem_test_abc123/Define";
            var supported = SupportedConnectTypes.Both;
            // Windows resolves a path rooted at `/` against the current drive, so there it is not a
            // fully qualified path and the endpoint stays unrecognized.
            if (OperatingSystem.IsWindows())
                await Assert.ThrowsAsync<InvalidOperationException>(() => ApiConnectValidator.ValidateAsync(endpoint, supported));
            else
                await Assert.ThrowsAsync<ArgumentException>(() => ApiConnectValidator.ValidateAsync(endpoint, supported));
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync treats the absolute temporary directory of the current operating system as a local path and throws FileNotFoundException without SystemSettings.xml")]
        public async Task ValidateAsync_LocalPath_MissingSystemSettings_ThrowsFileNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "polhem_api_client_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var supported = SupportedConnectTypes.Both;
                await Assert.ThrowsAsync<FileNotFoundException>(
                    () => ApiConnectValidator.ValidateAsync(tempDir, supported));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync with allowGenerateSettings creates SystemSettings.xml and DatabaseSettings.xml")]
        public async Task ValidateAsync_LocalPath_AllowGenerateSettings_CreatesFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "polhem_api_client_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var supported = SupportedConnectTypes.Both;
                var result = await ApiConnectValidator.ValidateAsync(tempDir, supported, allowGenerateSettings: true);

                Assert.Equal(ConnectType.Local, result);
                Assert.True(File.Exists(Path.Combine(tempDir, "SystemSettings.xml")));
                Assert.True(File.Exists(Path.Combine(tempDir, "DatabaseSettings.xml")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException for a URL when Remote is not supported")]
        public async Task ValidateAsync_RemoteUrl_RemoteNotSupported_ThrowsInvalidOperationException()
        {
            var supported = SupportedConnectTypes.Local;
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ApiConnectValidator.ValidateAsync("http://example.com/api", supported));
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync throws InvalidOperationException reporting endpoint not reachable for an unreachable URL")]
        public async Task ValidateAsync_RemoteUrl_NotReachable_ThrowsEndpointNotReachable()
        {
            var supported = SupportedConnectTypes.Both;
            // Port 1 on 127.0.0.1 is reserved and nothing listens on it locally, so the pre-check fails.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ApiConnectValidator.ValidateAsync("http://127.0.0.1:1/jsonrpc/api", supported));
            Assert.Contains("Endpoint not reachable", ex.Message);
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync checks a remote endpoint with the ping alone, sending no HEAD request")]
        public async Task ValidateAsync_RemoteUrl_SendsOnlyThePing()
        {
            await using var server = await LoopbackHttpServer.StartAsync("HTTP/1.1 404 Not Found");
            var supported = SupportedConnectTypes.Both;
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ApiConnectValidator.ValidateAsync(server.BuildUrl("/api"), supported));
            Assert.Equal(["POST"], server.Methods);
        }

        [Fact]
        [DisplayName("ApiConnectValidator.ValidateAsync does not report an endpoint that answered with an HTTP error as unreachable")]
        public async Task ValidateAsync_RemoteUrl_HttpError_IsNotReportedAsUnreachable()
        {
            await using var server = await LoopbackHttpServer.StartAsync("HTTP/1.1 404 Not Found");
            var supported = SupportedConnectTypes.Both;
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ApiConnectValidator.ValidateAsync(server.BuildUrl("/api"), supported));
            Assert.DoesNotContain("Endpoint not reachable", ex.Message);
        }

        [Fact]
        [DisplayName("SupportedConnectTypes.Both equals Local OR Remote")]
        public void SupportedConnectTypes_Both_EqualsLocalOrRemote()
        {
            Assert.Equal(SupportedConnectTypes.Local | SupportedConnectTypes.Remote, SupportedConnectTypes.Both);
            Assert.True(SupportedConnectTypes.Both.HasFlag(SupportedConnectTypes.Local));
            Assert.True(SupportedConnectTypes.Both.HasFlag(SupportedConnectTypes.Remote));
        }
    }
}
