using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the private method <see cref="ClientInfo.ParseCommandLineArgs"/>.
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
            Assert.NotNull(result);
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
    [Collection("ClientInfoState")]
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
        [DisplayName("SetConnectType Local sets ApiClientInfo.ConnectType to Local")]
        public void SetConnectType_LocalEndpoint_SetsConnectTypeToLocal()
        {
            var method = GetSetConnectTypeMethod();
            var originalType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            try
            {
                method.Invoke(null, s_localConnectTypeArgs);
                Assert.Equal(ConnectType.Local, ApiClientInfo.ConnectType);
                Assert.Equal(string.Empty, ApiClientInfo.Endpoint);
            }
            finally
            {
                ApiClientInfo.ConnectType = originalType;
                ApiClientInfo.Endpoint = originalEndpoint;
            }
        }

        [Fact]
        [DisplayName("SetConnectType Remote sets ApiClientInfo.ConnectType to Remote and updates Endpoint")]
        public void SetConnectType_RemoteEndpoint_SetsConnectTypeAndEndpoint()
        {
            var method = GetSetConnectTypeMethod();
            var originalType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            try
            {
                method.Invoke(null, s_remoteConnectTypeArgs);
                Assert.Equal(ConnectType.Remote, ApiClientInfo.ConnectType);
                Assert.Equal("http://remote.example.com", ApiClientInfo.Endpoint);
            }
            finally
            {
                ApiClientInfo.ConnectType = originalType;
                ApiClientInfo.Endpoint = originalEndpoint;
            }
        }

        [Fact]
        [DisplayName("CreateFormApiConnector with the Remote connect type returns a non-null FormApiConnector")]
        public void CreateFormApiConnector_RemoteConnectType_ReturnsNonNullConnector()
        {
            var originalType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            try
            {
                ApiClientInfo.ConnectType = ConnectType.Remote;
                ApiClientInfo.Endpoint = "http://remote.example.com";
                var connector = ClientInfo.CreateFormApiConnector("TestProg");
                Assert.NotNull(connector);
            }
            finally
            {
                ApiClientInfo.ConnectType = originalType;
                ApiClientInfo.Endpoint = originalEndpoint;
            }
        }

        [Fact]
        [DisplayName("SystemApiConnector getter with the Remote connect type creates and returns a non-null remote connector")]
        public void SystemApiConnector_RemoteConnectType_ReturnsNonNullConnector()
        {
            var originalType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            var sysConnField = typeof(ClientInfo).GetField(
                "s_systemConnector", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(sysConnField);
            var originalConnector = sysConnField!.GetValue(null);
            try
            {
                ApiClientInfo.ConnectType = ConnectType.Remote;
                ApiClientInfo.Endpoint = "http://remote.example.com";
                sysConnField.SetValue(null, null);
                var connector = ClientInfo.SystemApiConnector;
                Assert.NotNull(connector);
            }
            finally
            {
                ApiClientInfo.ConnectType = originalType;
                ApiClientInfo.Endpoint = originalEndpoint;
                sysConnField.SetValue(null, originalConnector);
            }
        }
    }
}
