using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers that the <c>AccessToken</c> setter does not reset the connector cache when given the same token.
    /// It mutates static state, so it shares the collection with the other ClientInfoState tests to run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoAccessTokenTests
    {
        private static readonly FieldInfo s_systemConnectorField =
            typeof(ClientInfo).GetField("s_systemConnector", BindingFlags.NonPublic | BindingFlags.Static)!;

        [Fact]
        [DisplayName("AccessToken setter does not reset the _systemConnector cache when given the same token")]
        public void AccessToken_SameTokenSetTwice_ConnectorCachePreserved()
        {
            var token = Guid.NewGuid();
            var originalType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            var originalConnectorCached = s_systemConnectorField.GetValue(null);

            try
            {
                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = token,
                    UserId = "u1",
                    UserName = "U1"
                });

                var connector = ClientInfo.SystemApiConnector;
                Assert.NotNull(connector);

                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = token,
                    UserId = "u1",
                    UserName = "U1"
                });

                var connectorAfter = s_systemConnectorField.GetValue(null);
                Assert.Same(connector, (SystemApiConnector?)connectorAfter);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
                ApiClientInfo.ConnectType = originalType;
                ApiClientInfo.Endpoint = originalEndpoint;
                s_systemConnectorField.SetValue(null, originalConnectorCached);
            }
        }
    }
}
