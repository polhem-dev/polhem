using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Text;

namespace Polhem.Api.Client
{
    /// <summary>
    /// Utility library for HTTP operations.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in <c>Polhem.Core</c>: every project in the framework inherits that
    /// assembly — including UI heads that only read definitions and tools that never open a socket —
    /// so a network primitive there put an unused capability on every consumer's public surface.
    /// Its callers have always been in this package. See <c>rules/dependency-boundary.md</c>:
    /// <c>Polhem.Core</c> takes abstractions that are genuinely shared across layers, not everything
    /// that happens to have no dependencies.
    /// </remarks>
    internal static class HttpUtilities
    {
        private static readonly ConcurrentDictionary<string, HttpClient> s_clientMap = new ConcurrentDictionary<string, HttpClient>();

        /// <summary>
        /// Creates or retrieves a <see cref="HttpClient"/> instance for the given host.
        /// </summary>
        /// <param name="fullUrl">The full URL of the API, e.g. https://api.example.com/v1/login.</param>
        /// <returns>A shared <see cref="HttpClient"/> instance that reuses the same connection pool.</returns>
        /// <remarks>
        /// This method creates a unique cache key based on the URL's <c>Schema + Host + Port</c> to avoid
        /// creating too many <c>HttpClient</c> instances for the same host, preventing Socket Exhaustion and DNS cache issues.
        /// </remarks>
        private static HttpClient GetOrCreateClient(string fullUrl)
        {
            var baseUri = new Uri(fullUrl);
            string cacheKey = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}";

            return s_clientMap.GetOrAdd(cacheKey, _ =>
            {
                var baseAddress = new Uri($"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}/");
                var timeout = TimeSpan.FromSeconds(30);
                if (UsesPlatformDefaultHandler())
                {
                    return new HttpClient { BaseAddress = baseAddress, Timeout = timeout };
                }

                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                };
                return new HttpClient(handler) { BaseAddress = baseAddress, Timeout = timeout };
            });
        }

        /// <summary>
        /// Gets whether the current platform builds its shared clients on the runtime's default handler
        /// rather than on an explicit <see cref="SocketsHttpHandler"/>.
        /// </summary>
        internal static bool UsesPlatformDefaultHandler()
            => UsesPlatformDefaultHandler(
                OperatingSystem.IsBrowser(),
                OperatingSystem.IsAndroid(),
                OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst());

        /// <summary>
        /// Decides whether a platform builds its shared clients on the runtime's default handler.
        /// </summary>
        /// <param name="isBrowser">Whether the host is browser-wasm.</param>
        /// <param name="isAndroid">Whether the host is Android.</param>
        /// <param name="isAppleMobile">Whether the host is iOS, tvOS or Mac Catalyst.</param>
        /// <returns>True for the browser and the mobile platforms; false for desktop and server hosts.</returns>
        /// <remarks>
        /// <para>
        /// On the browser, <see cref="SocketsHttpHandler"/> is not implemented; the default handler goes
        /// through the fetch API, and the browser owns connection pooling and DNS refresh.
        /// </para>
        /// <para>
        /// On iOS, tvOS, Mac Catalyst and Android, the default handler is the native one, built on
        /// NSURLSession on Apple platforms and on the Android network stack. It honours the device's TLS stack,
        /// App Transport Security, system proxy and VPN, user-installed certificates and the network
        /// security configuration, none of which an explicit <see cref="SocketsHttpHandler"/> goes through,
        /// so the app would behave differently from other apps on a managed device. Connection lifetime and
        /// DNS refresh are left to the native stack there; on desktop
        /// <see cref="SocketsHttpHandler.PooledConnectionLifetime"/> covers them.
        /// </para>
        /// </remarks>
        internal static bool UsesPlatformDefaultHandler(bool isBrowser, bool isAndroid, bool isAppleMobile)
            => isBrowser || isAndroid || isAppleMobile;

        /// <summary>
        /// Determines whether the specified input is a valid URL.
        /// </summary>
        /// <param name="input">The input URL to check.</param>
        public static bool IsUrl(string input)
        {
            Uri? uriResult;
            return Uri.TryCreate(input, UriKind.Absolute, out uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Asynchronously probes whether the specified endpoint is reachable over the network.
        /// </summary>
        /// <param name="endpoint">The endpoint URL to probe.</param>
        /// <param name="timeout">The probe timeout. Defaults to 5 seconds.</param>
        /// <param name="cancellationToken">A token that cancels the probe; cancelling it throws rather than reporting unreachable.</param>
        /// <returns>
        /// True if the server returns any HTTP response (including 4xx/5xx status codes);
        /// false on DNS failure, connection refused, or timeout.
        /// </returns>
        /// <remarks>
        /// Uses an HTTP HEAD request as a low-cost probe. Any HTTP response — including 404 or 405 —
        /// proves the host is alive and routing requests, so it is treated as "reachable".
        /// Use this only for transport-level reachability; verifying that the endpoint actually
        /// implements the expected service contract is the caller's responsibility.
        /// </remarks>
        public static async Task<bool> IsEndpointReachableAsync(string endpoint, TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));
            try
            {
                HttpClient client = GetOrCreateClient(endpoint);
                using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
                using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
                return true;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        /// <summary>
        /// Asynchronously sends a POST request.
        /// </summary>
        /// <param name="endpoint">The service endpoint.</param>
        /// <param name="body">The JSON string to pass in the request body.</param>
        /// <param name="headers">Custom request headers.</param>
        /// <param name="cancellationToken">A token that cancels the request.</param>
        public static Task<string> PostAsync(string endpoint, string body, NameValueCollection? headers = null,
            CancellationToken cancellationToken = default)
        {
            return PostAsync(GetOrCreateClient(endpoint), endpoint, body, headers, cancellationToken);
        }

        /// <summary>
        /// Asynchronously sends a POST request through the given client.
        /// </summary>
        /// <param name="client">The client to send with.</param>
        /// <param name="endpoint">The service endpoint.</param>
        /// <param name="body">The JSON string to pass in the request body.</param>
        /// <param name="headers">Custom request headers.</param>
        /// <param name="cancellationToken">A token that cancels the request.</param>
        public static async Task<string> PostAsync(HttpClient client, string endpoint, string body, NameValueCollection? headers,
            CancellationToken cancellationToken)
        {
            // Send POST request
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                if (headers != null)
                {
                    foreach (string key in headers)
                    {
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                    }
                }

                using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();  // Verify success
                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); // Read response content
                }
            }
        }
    }
}
