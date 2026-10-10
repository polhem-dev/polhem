using System.ComponentModel;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Tests for <see cref="PolhemApiClient"/>: how it is created, the connectors it hands out, and that a sign-in
    /// through it is what every one of its connectors then calls with.
    /// </summary>
    public class PolhemApiClientTests
    {
        /// <summary>
        /// A client over <paramref name="transport"/> that records the access token each call's transport was created for.
        /// </summary>
        private static PolhemApiClient Recording(IJsonRpcTransport transport, List<Guid> tokens)
            => PolhemApiClient.CreateWithTransport(token =>
            {
                tokens.Add(token);
                return transport;
            }, payloadOptions: TestClients.PayloadOptions);

        private static FakeApiTransport LoginServer(Guid token, string timeZone)
            => new(call => call.Method.EndsWith(".Login", StringComparison.Ordinal)
                ? FakeApiTransport.Answer(call, new LoginResponse { AccessToken = token, TimeZone = timeZone })
                : FakeApiTransport.Answer(call, new GetListResponse()));

        [Fact]
        [DisplayName("CreateLocal returns an in-process client with no endpoint")]
        public void CreateLocal_ReturnsLocalClient()
        {
            var client = PolhemApiClient.CreateLocal(Polhem.Tests.Shared.EmptyServiceProvider.Instance);

            Assert.True(client.IsLocal);
            Assert.Equal(string.Empty, client.Endpoint);
            Assert.Same(ApiSessionCredentials.Anonymous, client.Session.Credentials);
        }

        [Fact]
        [DisplayName("CreateLocal throws ArgumentNullException for a null service provider")]
        public void CreateLocal_NullServices_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PolhemApiClient.CreateLocal(null!));
        }

        [Fact]
        [DisplayName("CreateRemote returns an HTTP client holding the endpoint and the API key")]
        public void CreateRemote_SetsEndpointAndApiKey()
        {
            var client = PolhemApiClient.CreateRemote("http://example.com/api", "app.key");

            Assert.False(client.IsLocal);
            Assert.Equal("http://example.com/api", client.Endpoint);
            Assert.Equal("app.key", client.ApiKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("CreateRemote throws ArgumentException for a blank endpoint")]
        public void CreateRemote_BlankEndpoint_ThrowsArgumentException(string? endpoint)
        {
            Assert.Throws<ArgumentException>(() => PolhemApiClient.CreateRemote(endpoint!, string.Empty));
        }

        [Fact]
        [DisplayName("CreateRemote throws ArgumentNullException for a null API key")]
        public void CreateRemote_NullApiKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PolhemApiClient.CreateRemote("http://example.com/api", null!));
        }

        [Fact]
        [DisplayName("ApiKey can be replaced and rejects null")]
        public void ApiKey_Set_ReplacesValueAndRejectsNull()
        {
            var client = PolhemApiClient.CreateRemote("http://example.com/api", "first.key");

            client.ApiKey = "second.key";

            Assert.Equal("second.key", client.ApiKey);
            Assert.Throws<ArgumentNullException>(() => client.ApiKey = null!);
        }

        [Fact]
        [DisplayName("A client created without payload options gets its own instance")]
        public void Create_WithoutPayloadOptions_GetsOwnInstance()
        {
            var a = PolhemApiClient.CreateRemote("http://example.com/api", string.Empty);
            var b = PolhemApiClient.CreateRemote("http://example.com/api", string.Empty);

            Assert.NotSame(a.PayloadOptions, b.PayloadOptions);
        }

        [Fact]
        [DisplayName("System and AuditLog are one instance each, bound to the client")]
        public void SystemAndAuditLog_AreSingleInstancesOfTheClient()
        {
            var client = PolhemApiClient.CreateRemote("http://example.com/api", string.Empty);

            Assert.Same(client.System, client.System);
            Assert.Same(client.AuditLog, client.AuditLog);
            Assert.Same(client, client.System.Client);
            Assert.Same(client, client.AuditLog.Client);
        }

        [Fact]
        [DisplayName("Form returns a new connector for the form, bound to the client")]
        public void Form_ReturnsNewConnectorBoundToClient()
        {
            var client = PolhemApiClient.CreateRemote("http://example.com/api", string.Empty);

            var first = client.Form("Employee");
            var second = client.Form("Employee");

            Assert.NotSame(first, second);
            Assert.Equal("Employee", first.ProgId);
            Assert.Same(client, first.Client);
        }

        [Fact]
        [DisplayName("Signing in through System signs in the whole client: a form connector then calls with the new token and zone")]
        public async Task LoginAsync_SignsInClient_FormCallsCarryToken()
        {
            var token = Guid.NewGuid();
            var tokens = new List<Guid>();
            var client = Recording(LoginServer(token, "Asia/Tokyo"), tokens);
            var form = client.Form("Employee");

            await client.System.LoginAsync("001", "secret");
            await form.GetListAsync();

            Assert.Equal(token, client.Session.Credentials.AccessToken);
            Assert.Equal("Asia/Tokyo", client.Session.Credentials.UserTimeZoneId);
            Assert.Equal([Guid.Empty, token], tokens);
        }

        [Fact]
        [DisplayName("A connector taken before a new sign-in calls with the new identity afterwards")]
        public async Task Connector_HeldAcrossSignIn_FollowsNewIdentity()
        {
            var tokens = new List<Guid>();
            var client = Recording(new FakeApiTransport(call => FakeApiTransport.Answer(call, new GetListResponse())), tokens);
            var form = client.Form("Employee");
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            client.Session.SignIn(new ApiSessionCredentials(first, [], string.Empty));
            await form.GetListAsync();
            client.Session.SignIn(new ApiSessionCredentials(second, [], string.Empty));
            await form.GetListAsync();

            Assert.Equal([first, second], tokens);
        }

        [Fact]
        [DisplayName("A sign-in during a call does not change the token that call sends")]
        public async Task ExecuteAsync_SignInDuringCall_KeepsTokenOfCallStart()
        {
            var tokens = new List<Guid>();
            var first = Guid.NewGuid();
            PolhemApiClient? client = null;
            var transport = new FakeApiTransport(call =>
            {
                // The server sees the request after the client has already taken its credentials; signing in now
                // must not reach this call.
                client!.Session.SignIn(new ApiSessionCredentials(Guid.NewGuid(), [], string.Empty));
                return FakeApiTransport.Answer(call, new GetListResponse());
            });
            client = Recording(transport, tokens);
            client.Session.SignIn(new ApiSessionCredentials(first, [], string.Empty));

            await client.Form("Employee").GetListAsync();

            Assert.Equal([first], tokens);
        }

        [Fact]
        [DisplayName("LogoutAsync signs the client out after the server confirms")]
        public async Task LogoutAsync_SignsClientOut()
        {
            var client = TestClients.Fake(new FakeApiTransport(call => FakeApiTransport.Answer(call, new LogoutResponse())));

            await client.System.LogoutAsync();

            Assert.Same(ApiSessionCredentials.Anonymous, client.Session.Credentials);
        }

        [Fact]
        [DisplayName("Signing in one client leaves another client anonymous")]
        public async Task LoginAsync_OneClient_DoesNotSignInAnother()
        {
            var signedIn = TestClients.Fake(LoginServer(Guid.NewGuid(), "Asia/Tokyo"));
            var other = PolhemApiClient.CreateWithTransport(_ => new FakeApiTransport(), payloadOptions: TestClients.PayloadOptions);

            await signedIn.System.LoginAsync("001", "secret");

            Assert.Same(ApiSessionCredentials.Anonymous, other.Session.Credentials);
        }

        [Fact]
        [DisplayName("SignOut returns the client's session to anonymous")]
        public void SignOut_ReturnsSessionToAnonymous()
        {
            var client = TestClients.Fake(new FakeApiTransport());

            client.SignOut();

            Assert.Same(ApiSessionCredentials.Anonymous, client.Session.Credentials);
        }

        [Fact]
        [DisplayName("An in-process client outside debug mode sends Plain, whatever format was asked for")]
        public async Task ExecuteAsync_LocalOutsideDebug_SendsPlain()
        {
            var transport = new FakeApiTransport(call => FakeApiTransport.Answer(call, new GetListResponse()));
            var client = PolhemApiClient.CreateWithTransport(_ => transport, isLocal: true, payloadOptions: TestClients.PayloadOptions);
            var wasDebug = Polhem.Core.SysInfo.IsDebugMode;
            Polhem.Core.SysInfo.IsDebugMode = false;
            try
            {
                await client.Form("Employee").ExecuteAsync<GetListResponse>("GetList", new GetListRequest(), PayloadFormat.Encoded);
            }
            finally
            {
                Polhem.Core.SysInfo.IsDebugMode = wasDebug;
            }

            Assert.Equal(PayloadFormat.Plain, transport.LastCall!.Params.Format);
        }
    }
}
