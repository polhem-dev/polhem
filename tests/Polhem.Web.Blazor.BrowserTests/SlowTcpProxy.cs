using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// Forwards TCP connections to a server and delays the bytes in both directions, in order, as a slow network does.
    /// </summary>
    public sealed class SlowTcpProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly IPEndPoint _target;
        private readonly TimeSpan _delay;
        private readonly Task _accepting;

        /// <summary>
        /// Initializes a new instance of <see cref="SlowTcpProxy"/> and starts listening.
        /// </summary>
        /// <param name="target">The server.</param>
        /// <param name="delay">The delay in each direction.</param>
        public SlowTcpProxy(IPEndPoint target, TimeSpan delay)
        {
            _target = target;
            _delay = delay;
            _listener.Start();
            _accepting = AcceptAsync();
        }

        /// <summary>Gets the address to connect to instead of the server.</summary>
        public string Address => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                    _ = ForwardConnectionAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped by DisposeAsync.
            }
        }

        private async Task ForwardConnectionAsync(TcpClient client)
        {
            using (client)
            using (var server = new TcpClient())
            {
                await server.ConnectAsync(_target, _stop.Token).ConfigureAwait(false);
                await Task.WhenAny(
                    ForwardAsync(client.GetStream(), server.GetStream()),
                    ForwardAsync(server.GetStream(), client.GetStream())).ConfigureAwait(false);
            }
        }

        private async Task ForwardAsync(NetworkStream from, NetworkStream to)
        {
            var chunks = Channel.CreateUnbounded<(byte[] Bytes, DateTime Due)>();
            var writing = WriteAsync(chunks.Reader, to);
            var buffer = new byte[16 * 1024];
            try
            {
                int read;
                while ((read = await from.ReadAsync(buffer, _stop.Token).ConfigureAwait(false)) > 0)
                    chunks.Writer.TryWrite((buffer[..read], DateTime.UtcNow + _delay));
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                // The connection ended.
            }
            chunks.Writer.TryComplete();
            await writing.ConfigureAwait(false);
        }

        private async Task WriteAsync(ChannelReader<(byte[] Bytes, DateTime Due)> chunks, NetworkStream to)
        {
            try
            {
                await foreach (var (bytes, due) in chunks.ReadAllAsync(_stop.Token).ConfigureAwait(false))
                {
                    var wait = due - DateTime.UtcNow;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, _stop.Token).ConfigureAwait(false);
                    await to.WriteAsync(bytes, _stop.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // The connection ended.
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            await _accepting.ConfigureAwait(false);
            _stop.Dispose();
        }
    }
}
