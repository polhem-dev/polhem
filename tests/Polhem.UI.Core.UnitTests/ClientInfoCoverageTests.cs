using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the private method <c>ClientInfo.ParseCommandLineArgs</c> (a private member cannot be a cross-assembly cref).
    /// This class only reads, so it does not need the ClientInfoState collection.
    /// </summary>
    public class ClientInfoParseArgsTests
    {
        [Fact]
        [DisplayName("ParseCommandLineArgs called through reflection returns a non-null dictionary")]
        public void ParseCommandLineArgs_InvokedViaReflection_ReturnsNonNull()
        {
            var method = typeof(ClientInfo).GetMethod(
                "ParseCommandLineArgs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var result = method!.Invoke(null, null);
            Assert.IsType<Dictionary<string, string>>(result);
        }

        [Fact]
        [DisplayName("ParseCommandLineArgs returns a dictionary with case-insensitive key lookup (OrdinalIgnoreCase)")]
        public void ParseCommandLineArgs_Result_SupportsCaseInsensitiveKeys()
        {
            var method = typeof(ClientInfo).GetMethod(
                "ParseCommandLineArgs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var result = (Dictionary<string, string>)method!.Invoke(null, null)!;
            result["TestKey"] = "value";
            Assert.True(result.ContainsKey("testkey"));
            Assert.True(result.ContainsKey("TESTKEY"));
        }
    }

    /// <summary>
    /// Covers the private method <c>SetConnectType</c> of <see cref="ClientInfo"/> and the remote connector caching path.
    /// It mutates static state, so it shares the ClientInfoState collection with the other ClientInfo tests to run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoConnectorTests
    {
        private static readonly Type[] s_setConnectTypeParams = [typeof(ConnectType), typeof(string)];
        private static readonly object[] s_remoteConnectTypeArgs = [ConnectType.Remote, "http://remote.example.com"];
        private static readonly object[] s_localConnectTypeArgs = [ConnectType.Local, string.Empty];

        private static MethodInfo GetSetConnectTypeMethod()
        {
            var method = typeof(ClientInfo).GetMethod(
                "SetConnectType",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_setConnectTypeParams,
                null);
            Assert.NotNull(method);
            return method!;
        }

        [Fact]
        [DisplayName("SetConnectType Local sets ClientInfo.ConnectType to Local")]
        public void SetConnectType_LocalEndpoint_SetsConnectTypeToLocal()
        {
            var method = GetSetConnectTypeMethod();
            using var preserved = ClientInfoTestState.Preserve();
            method.Invoke(null, s_localConnectTypeArgs);
            Assert.Equal(ConnectType.Local, ClientInfo.ConnectType);
            Assert.Equal(string.Empty, ClientInfo.ApiClient.Endpoint);
        }

        [Fact]
        [DisplayName("SetConnectType Remote sets ClientInfo.ConnectType to Remote and updates Endpoint")]
        public void SetConnectType_RemoteEndpoint_SetsConnectTypeAndEndpoint()
        {
            var method = GetSetConnectTypeMethod();
            using var preserved = ClientInfoTestState.Preserve();
            method.Invoke(null, s_remoteConnectTypeArgs);
            Assert.Equal(ConnectType.Remote, ClientInfo.ConnectType);
            Assert.Equal("http://remote.example.com", ClientInfo.ApiClient.Endpoint);
        }

        [Fact]
        [DisplayName("CreateFormApiConnector returns a connector for the form, bound to ApiClient")]
        public void CreateFormApiConnector_ReturnsConnectorOfApiClient()
        {
            using var preserved = ClientInfoTestState.Preserve();
            var client = PolhemApiClient.CreateRemote("http://remote.example.com", string.Empty);
            ClientInfoTestState.UseClient(client);

            var connector = ClientInfo.CreateFormApiConnector("TestProg");

            Assert.Equal("TestProg", connector.ProgId);
            Assert.Same(client, connector.Client);
        }

        [Fact]
        [DisplayName("SystemApiConnector and CreateAuditLogApiConnector return the connectors of ApiClient")]
        public void Connectors_AreThoseOfApiClient()
        {
            using var preserved = ClientInfoTestState.Preserve();
            var client = PolhemApiClient.CreateRemote("http://remote.example.com", string.Empty);
            ClientInfoTestState.UseClient(client);

            Assert.Same(client.System, ClientInfo.SystemApiConnector);
            Assert.Same(client.AuditLog, ClientInfo.CreateAuditLogApiConnector());
        }

        [Fact]
        [DisplayName("SetConnectType Remote creates a client with the current API key and an anonymous session")]
        public void SetConnectType_Remote_NewClientCarriesKeyAndIsAnonymous()
        {
            var method = GetSetConnectTypeMethod();
            using var preserved = ClientInfoTestState.Preserve();
            ClientInfoTestState.ApiKey = "app.key";
            ClientInfo.ApiClient.Session.SignIn(new ApiSessionCredentials(Guid.NewGuid(), [1], "Asia/Tokyo"));

            method.Invoke(null, s_remoteConnectTypeArgs);

            Assert.Equal("app.key", ClientInfo.ApiClient.ApiKey);
            Assert.Same(ApiSessionCredentials.Anonymous, ClientInfo.ApiClient.Session.Credentials);
            Assert.Equal(Guid.Empty, ClientInfo.AccessToken);
        }
    }
}
