using System.ComponentModel;
using Polhem.Api.Core.Dispatch;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Tests for the replay scope <see cref="PolhemPayloadPolicy"/> gives a call.
    /// </summary>
    /// <remarks>
    /// The anonymous case cannot be reached through the dispatcher with the framework's own methods: every method
    /// that declares a sequence check also requires authentication, so the access filter refuses an anonymous call
    /// first. The policy is therefore asked directly.
    /// </remarks>
    public class PolhemPayloadPolicyTests
    {
        private static readonly Guid s_token = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        private static JsonRpcRequestContext Context(Guid accessToken, bool isLocalCall)
        {
            var kind = isLocalCall ? JsonRpcTransportKind.InProcess : JsonRpcTransportKind.Http;
            var context = new JsonRpcRequestContext(
                new JsonRpcRequest("System.Ping", null, JsonRpcId.Null), new JsonRpcTransportInfo(kind), CancellationToken.None);
            var state = PolhemCallState.Create(context);
            state.AccessToken = accessToken;
            state.IsLocalCall = isLocalCall;
            return context;
        }

        [Fact]
        [DisplayName("GetReplayScope gives a remote call with a session its access token")]
        public void GetReplayScope_RemoteWithSession_ReturnsAccessToken()
        {
            Assert.Equal(s_token.ToString("N"), PolhemPayloadPolicy.Instance.GetReplayScope(Context(s_token, isLocalCall: false)));
        }

        [Fact]
        [DisplayName("GetReplayScope gives an anonymous remote call no scope")]
        public void GetReplayScope_RemoteAnonymous_ReturnsNull()
        {
            Assert.Null(PolhemPayloadPolicy.Instance.GetReplayScope(Context(Guid.Empty, isLocalCall: false)));
        }

        [Fact]
        [DisplayName("GetReplayScope gives a local call no scope even with a session")]
        public void GetReplayScope_LocalWithSession_ReturnsNull()
        {
            Assert.Null(PolhemPayloadPolicy.Instance.GetReplayScope(Context(s_token, isLocalCall: true)));
        }
    }
}
