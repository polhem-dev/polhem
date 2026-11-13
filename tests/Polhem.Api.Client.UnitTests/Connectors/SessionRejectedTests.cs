using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Core.Exceptions;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// <see cref="PolhemApiClient.SessionRejected"/> tells a front end that the server ended the session it is using.
    /// </summary>
    public class SessionRejectedTests
    {
        private sealed class TestApiConnector(PolhemApiClient client) : ApiConnector(client)
        {
            public new Task<T> ExecuteAsync<T>(string progId, string action, object value, PayloadFormat format,
                CancellationToken cancellationToken = default)
                => base.ExecuteAsync<T>(progId, action, value, format, cancellationToken);
        }

        private static FakeApiTransport Unauthorized(Action? beforeAnswer = null) => new(_ =>
        {
            beforeAnswer?.Invoke();
            throw new JsonRpcErrorException((int)JsonRpcErrorCode.Unauthorized, "Session has expired.");
        });

        [Fact]
        [DisplayName("A call refused as unauthorized while it carried the current token raises SessionRejected once")]
        public async Task Unauthorized_CurrentSignIn_RaisesTheEvent()
        {
            var client = TestClients.Fake(Unauthorized());
            int raised = 0;
            client.SessionRejected += (_, _) => raised++;

            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => new TestApiConnector(client).ExecuteAsync<string>("Order", "GetList", new object(), PayloadFormat.Plain));

            Assert.Equal(1, raised);
        }

        [Fact]
        [DisplayName("A call refused after the client signed in again does not raise SessionRejected for the new session")]
        public async Task Unauthorized_AfterANewSignIn_DoesNotRaiseTheEvent()
        {
            PolhemApiClient? client = null;
            client = TestClients.Fake(Unauthorized(
                () => client!.Session.SignIn(new ApiSessionCredentials(Guid.NewGuid(), [], string.Empty))));
            int raised = 0;
            client.SessionRejected += (_, _) => raised++;

            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => new TestApiConnector(client).ExecuteAsync<string>("Order", "GetList", new object(), PayloadFormat.Plain));

            Assert.Equal(0, raised);
        }

        [Fact]
        [DisplayName("ExecFuncAnonymous refusing a function that needs a signed-in caller does not raise SessionRejected")]
        public async Task Unauthorized_AnonymousAction_DoesNotRaiseTheEvent()
        {
            var client = TestClients.Fake(Unauthorized());
            int raised = 0;
            client.SessionRejected += (_, _) => raised++;

            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => client.System.ExecFuncAnonymousAsync(new ExecFuncRequest { FuncId = "Secret" }));

            Assert.Equal(0, raised);
        }

        [Fact]
        [DisplayName("Logging out a session the server already ended signs the client out without throwing")]
        public async Task LogoutAsync_SessionAlreadyEnded_SignsOut()
        {
            var client = TestClients.Fake(Unauthorized());

            var exception = await Record.ExceptionAsync(() => client.System.LogoutAsync());

            Assert.Null(exception);
            Assert.Equal(Guid.Empty, client.Session.Credentials.AccessToken);
        }

        [Fact]
        [DisplayName("A call with no token, such as a sign-in, does not raise SessionRejected when it is refused")]
        public async Task Unauthorized_Anonymous_DoesNotRaiseTheEvent()
        {
            var client = TestClients.Fake(Unauthorized());
            client.SignOut();
            int raised = 0;
            client.SessionRejected += (_, _) => raised++;

            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => new TestApiConnector(client).ExecuteAsync<string>("System", "Login", new object(), PayloadFormat.Plain));

            Assert.Equal(0, raised);
        }
    }
}
