using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Drives <see cref="ApiConnector"/> through one complete call with a fake <see cref="IJsonRpcProvider"/>, so the
    /// tests can observe the actual behavior before the request is sent and after the response arrives, without a
    /// real server.
    /// </summary>
    /// <remarks>
    /// <c>FinalizeResponse</c> and <c>PrepareRequest</c> are both private and are deliberately not called through
    /// reflection: the tests check behavior the caller can see, and the order between steps can only be checked by
    /// going through the whole call path.
    /// </remarks>
    internal static class ApiConnectorTestHost
    {
        private const string TestProgId = "Unit";
        private const string TestAction = "Echo";

        private sealed class TestApiConnector : ApiConnector
        {
            public TestApiConnector(Guid accessToken) : base(accessToken) { }

            public TestApiConnector(Guid accessToken, ApiSessionContext session) : base(accessToken, session) { }

            public new Task<T> ExecuteAsync<T>(string progId, string action, object value, PayloadFormat format)
                => base.ExecuteAsync<T>(progId, action, value, format);
        }

        private sealed class FakeJsonRpcProvider : IJsonRpcProvider
        {
            public Func<JsonRpcRequest, JsonRpcResponse> ResponseFactory { get; set; }
                = req => new JsonRpcResponse(req) { Result = new JsonRpcResult { Value = "ok" } };

            public Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request)
                => Task.FromResult(ResponseFactory(request));
        }

        private static TestApiConnector CreateConnector(IJsonRpcProvider provider, ApiSessionContext? session = null)
        {
            // A dedicated session keeps tests off ApiSessionContext.Ambient, which is process-wide.
            var connector = session == null
                ? new TestApiConnector(Guid.NewGuid())
                : new TestApiConnector(Guid.NewGuid(), session);
            var prop = typeof(ApiConnector).GetProperty(nameof(ApiConnector.Provider),
                BindingFlags.Public | BindingFlags.Instance)!;
            prop.SetValue(connector, provider);
            return connector;
        }

        /// <summary>
        /// Runs one call to which the server responds with the given error code and message.
        /// </summary>
        /// <param name="code">The JSON-RPC error code the server returns.</param>
        /// <param name="message">The message the server returns.</param>
        public static Task<string> ExecuteWithErrorAsync(JsonRpcErrorCode code, string message)
        {
            var provider = new FakeJsonRpcProvider
            {
                ResponseFactory = req => new JsonRpcResponse(req)
                {
                    Error = new JsonRpcError((int)code, message)
                }
            };
            return CreateConnector(provider).ExecuteAsync<string>(
                TestProgId, TestAction, new object(), PayloadFormat.Plain);
        }

        /// <summary>
        /// Runs one call to which the server responds with a successful result (the default value "ok").
        /// </summary>
        public static Task<string> ExecuteWithResultAsync()
        {
            return CreateConnector(new FakeJsonRpcProvider()).ExecuteAsync<string>(
                TestProgId, TestAction, new object(), PayloadFormat.Plain);
        }

        /// <summary>
        /// Sends one request with the given user time zone; the server responds with a successful result (the
        /// default value "ok").
        /// </summary>
        /// <param name="value">The payload value of the request.</param>
        /// <param name="userTimeZoneId">The user's IANA time zone ID; an empty string means not logged in, with no time zone conversion.</param>
        public static Task<string> ExecuteAsUserAsync(object value, string userTimeZoneId)
            => ExecuteAsUserAsync(value, userTimeZoneId, _ => { });

        /// <summary>
        /// Sends one request with the given user time zone and runs <paramref name="onServer"/> when the "server"
        /// receives it.
        /// </summary>
        /// <param name="value">The payload value of the request.</param>
        /// <param name="userTimeZoneId">The user's IANA time zone ID; an empty string means not logged in, with no time zone conversion.</param>
        /// <param name="onServer">
        /// The action run inside the provider on the received request. The provider does not serialize, so it
        /// receives the very object the Connector handed over, the same shape as an in-process call.
        /// </param>
        public static Task<string> ExecuteAsUserAsync(object value, string userTimeZoneId, Action<JsonRpcRequest> onServer)
        {
            var session = new ApiSessionContext { UserTimeZoneId = userTimeZoneId };
            var provider = new FakeJsonRpcProvider
            {
                ResponseFactory = req =>
                {
                    onServer(req);
                    return new JsonRpcResponse(req) { Result = new JsonRpcResult { Value = "ok" } };
                }
            };
            return CreateConnector(provider, session).ExecuteAsync<string>(
                TestProgId, TestAction, value, PayloadFormat.Plain);
        }
    }
}
