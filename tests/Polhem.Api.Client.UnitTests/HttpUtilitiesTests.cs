using System.ComponentModel;
using System.Net;
using System.Net.Sockets;

namespace Polhem.Api.Client.UnitTests
{
    public class HttpUtilitiesTests
    {
        [Theory]
        [InlineData("http://example.com", true)]
        [InlineData("https://example.com/path?x=1", true)]
        [InlineData("HTTP://example.com", true)]
        [InlineData("ftp://example.com", false)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData("/local/path", false)]
        [DisplayName("IsUrl returns true only for absolute http/https URLs")]
        public void IsUrl_RecognizesHttpSchemes(string input, bool expected)
        {
            Assert.Equal(expected, HttpUtilities.IsUrl(input));
        }

        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, true, true)]
        [DisplayName("UsesPlatformDefaultHandler is false only for desktop and server hosts")]
        public void UsesPlatformDefaultHandler_ByPlatform_ReturnsExpected(bool isBrowser, bool isAndroid, bool isAppleMobile, bool expected)
        {
            Assert.Equal(expected, HttpUtilities.UsesPlatformDefaultHandler(isBrowser, isAndroid, isAppleMobile));
        }

        [Fact]
        [DisplayName("UsesPlatformDefaultHandler keeps SocketsHttpHandler on the desktop test host")]
        public void UsesPlatformDefaultHandler_DesktopHost_ReturnsFalse()
        {
            Assert.False(HttpUtilities.UsesPlatformDefaultHandler());
        }

        [Fact]
        [DisplayName("IsEndpointReachableAsync returns true for a running endpoint")]
        public async Task IsEndpointReachableAsync_RunningServer_ReturnsTrue()
        {
            await using var server = await LoopbackHttpServer.StartAsync();

            bool result = await HttpUtilities.IsEndpointReachableAsync(server.BuildUrl("/probe"));

            Assert.True(result);
        }

        [Fact]
        [DisplayName("IsEndpointReachableAsync treats a 4xx response as reachable and returns true")]
        public async Task IsEndpointReachableAsync_NotFoundStatus_ReturnsTrue()
        {
            await using var server = await LoopbackHttpServer.StartAsync(statusLine: "HTTP/1.1 404 Not Found", body: string.Empty);

            bool result = await HttpUtilities.IsEndpointReachableAsync(server.BuildUrl("/missing"));

            Assert.True(result);
        }

        [Fact]
        [DisplayName("IsEndpointReachableAsync returns false when the connection is refused")]
        public async Task IsEndpointReachableAsync_ConnectionRefused_ReturnsFalse()
        {
            // Port 1 on 127.0.0.1 is reserved and nothing listens on it locally, so the connection is refused.
            bool result = await HttpUtilities.IsEndpointReachableAsync("http://127.0.0.1:1/probe");

            Assert.False(result);
        }

        [Fact]
        [DisplayName("IsEndpointReachableAsync returns false when the probe times out")]
        public async Task IsEndpointReachableAsync_Timeout_ReturnsFalse()
        {
            await using var stall = await StallingServer.StartAsync();

            bool result = await HttpUtilities.IsEndpointReachableAsync(
                stall.BuildUrl("/never"),
                timeout: TimeSpan.FromMilliseconds(200));

            Assert.False(result);
        }

        /// <summary>
        /// Loopback TCP listener that accepts connections but never reads or writes,
        /// used to deterministically trigger client-side timeouts.
        /// </summary>
        private sealed class StallingServer : IAsyncDisposable
        {
            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cts = new();
            private readonly Task _acceptLoop;
            private readonly List<TcpClient> _accepted = [];

            public int Port { get; }

            private StallingServer(TcpListener listener, int port)
            {
                _listener = listener;
                Port = port;
                _acceptLoop = Task.Run(AcceptLoopAsync);
            }

            private async Task AcceptLoopAsync()
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        TcpClient client;
                        try
                        {
                            client = await _listener.AcceptTcpClientAsync(_cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        lock (_accepted)
                        {
                            _accepted.Add(client);
                        }
                        // Intentionally never read or respond — the connection stalls until disposal.
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped concurrently.
                }
                catch (SocketException)
                {
                    // Listener was stopped while accepting.
                }
            }

            public static Task<StallingServer> StartAsync()
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                return Task.FromResult(new StallingServer(listener, port));
            }

            public string BuildUrl(string path) => $"http://127.0.0.1:{Port}{path}";

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                _listener.Stop();
                try
                {
                    await _acceptLoop;
                }
                catch (OperationCanceledException)
                {
                    // Expected during cancellation.
                }
                lock (_accepted)
                {
                    foreach (var client in _accepted)
                    {
                        try { client.Dispose(); } catch (ObjectDisposedException) { }
                    }
                }
                _cts.Dispose();
            }
        }
    }
}
