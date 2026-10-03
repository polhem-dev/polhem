using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Minimal HTTP loopback server that answers every request with the same response, used to exercise the HTTP
    /// client code without external infrastructure or mocking HttpClient internals.
    /// </summary>
    internal sealed class LoopbackHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly string _statusLine;
        private readonly string _body;
        private string _lastRequest = string.Empty;
        private readonly ConcurrentQueue<string> _methods = new();

        public int Port { get; }
        public string LastRequest => Volatile.Read(ref _lastRequest);

        /// <summary>Gets the HTTP method of every request received, in order.</summary>
        public IReadOnlyCollection<string> Methods => _methods;

        private LoopbackHttpServer(TcpListener listener, int port, string statusLine, string body)
        {
            _listener = listener;
            _statusLine = statusLine;
            _body = body;
            Port = port;

            _acceptLoop = Task.Run(RunAcceptLoopAsync);
        }

        private async Task RunAcceptLoopAsync()
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

                    await HandleClientAsync(client);
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

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var ns = client.GetStream())
            {
                var buffer = new byte[4096];
                var sb = new StringBuilder();
                int headerEnd = -1;
                int contentLength = 0;

                while (headerEnd < 0)
                {
                    int read = await ns.ReadAsync(buffer);
                    if (read == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    int idx = sb.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        headerEnd = idx + 4;
                        foreach (var line in sb.ToString(0, idx).Split("\r\n"))
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                _ = int.TryParse(line.AsSpan("Content-Length:".Length).Trim(), out contentLength);
                                break;
                            }
                        }
                    }
                }

                int bodyRead = Math.Max(0, sb.Length - Math.Max(headerEnd, 0));
                while (headerEnd >= 0 && bodyRead < contentLength)
                {
                    int read = await ns.ReadAsync(buffer);
                    if (read == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    bodyRead += read;
                }

                var request = sb.ToString();
                Volatile.Write(ref _lastRequest, request);
                _methods.Enqueue(request.Split(' ', 2)[0]);

                byte[] bodyBytes = Encoding.UTF8.GetBytes(_body);
                string response =
                    $"{_statusLine}\r\n" +
                    "Content-Type: text/plain; charset=utf-8\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    "Connection: close\r\n\r\n";
                byte[] header = Encoding.UTF8.GetBytes(response);
                await ns.WriteAsync(header);
                await ns.WriteAsync(bodyBytes);
                await ns.FlushAsync();
            }
        }

        public static Task<LoopbackHttpServer> StartAsync(
            string statusLine = "HTTP/1.1 200 OK",
            string body = "pong")
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return Task.FromResult(new LoopbackHttpServer(listener, port, statusLine, body));
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
            catch (IOException)
            {
                // Stream torn down during shutdown.
            }
            _cts.Dispose();
        }
    }
}
