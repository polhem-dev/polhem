using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;
using Polhem.Api.Core.Messages.System;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers how <see cref="ClientInfo.ApplyLoginResult"/> updates the session of <see cref="ClientInfo.ApiClient"/>.
    /// It mutates static state, so it shares the collection with the other ClientInfoState tests to run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoAccessTokenTests
    {
        private static readonly FieldInfo s_defineAccessField =
            typeof(ClientInfo).GetField("s_defineAccess", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static PolhemApiClient UseFreshClient()
        {
            var client = PolhemApiClient.CreateRemote("http://remote.example.com", string.Empty);
            ClientInfoTestState.UseClient(client);
            return client;
        }

        [Fact]
        [DisplayName("ApplyLoginResult keeps the transmission key exchanged for the same token")]
        public void ApplyLoginResult_SameToken_KeepsKey()
        {
            using var preserved = ClientInfoTestState.Preserve();
            var client = UseFreshClient();
            var token = Guid.NewGuid();
            client.Session.SignIn(new ApiSessionCredentials(token, [1, 2, 3], string.Empty));

            ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = token, TimeZone = "Asia/Tokyo" });

            Assert.Equal(token, ClientInfo.AccessToken);
            Assert.Equal([1, 2, 3], client.Session.Credentials.ApiEncryptionKey);
            Assert.Equal("Asia/Tokyo", client.Session.Credentials.UserTimeZoneId);
        }

        [Fact]
        [DisplayName("ApplyLoginResult for a token the session does not hold signs in without a key")]
        public void ApplyLoginResult_OtherToken_DropsKey()
        {
            using var preserved = ClientInfoTestState.Preserve();
            var client = UseFreshClient();
            client.Session.SignIn(new ApiSessionCredentials(Guid.NewGuid(), [1, 2, 3], string.Empty));
            var other = Guid.NewGuid();

            ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = other });

            Assert.Equal(other, ClientInfo.AccessToken);
            Assert.Empty(client.Session.Credentials.ApiEncryptionKey);
        }

        [Fact]
        [DisplayName("ApplyLoginResult discards the definition cache of the previous identity")]
        public void ApplyLoginResult_DiscardsDefinitionCache()
        {
            using var preserved = ClientInfoTestState.Preserve();
            UseFreshClient();
            var cached = ClientInfo.DefineAccess;

            ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.NewGuid() });

            Assert.Null(s_defineAccessField.GetValue(null));
            Assert.NotSame(cached, ClientInfo.DefineAccess);
        }
    }
}
