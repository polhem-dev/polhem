using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.Messages.System;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the <see cref="ClientInfo.DefineAccess"/> getter:
    /// the caching behavior (two accesses return the same instance) and
    /// the cache being cleared when AccessToken changes (two accesses return different instances).
    /// It mutates static state, so it runs serially in the <c>ClientInfoState</c> collection.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoDefineAccessTests
    {
        private static readonly FieldInfo s_defineAccessField =
            typeof(ClientInfo).GetField("s_defineAccess", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static readonly FieldInfo s_systemConnectorField =
            typeof(ClientInfo).GetField("s_systemConnector", BindingFlags.NonPublic | BindingFlags.Static)!;

        [Fact]
        [DisplayName("DefineAccess getter returns the same cached instance on repeated access")]
        public void DefineAccess_AccessedTwice_ReturnsSameInstance()
        {
            var originalDefineAccess = s_defineAccessField.GetValue(null);
            var originalSystemConnector = s_systemConnectorField.GetValue(null);
            try
            {
                s_defineAccessField.SetValue(null, null);
                s_systemConnectorField.SetValue(null, null);
                var first = ClientInfo.DefineAccess;
                var second = ClientInfo.DefineAccess;
                Assert.Same(first, second);
            }
            finally
            {
                s_defineAccessField.SetValue(null, originalDefineAccess);
                s_systemConnectorField.SetValue(null, originalSystemConnector);
            }
        }

        [Fact]
        [DisplayName("DefineAccess getter returns a new instance after AccessToken changes (the cache is cleared)")]
        public void DefineAccess_AfterTokenChange_ReturnsNewInstance()
        {
            var originalDefineAccess = s_defineAccessField.GetValue(null);
            var originalSystemConnector = s_systemConnectorField.GetValue(null);
            try
            {
                s_defineAccessField.SetValue(null, null);
                s_systemConnectorField.SetValue(null, null);
                var firstAccess = ClientInfo.DefineAccess;

                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = Guid.NewGuid(),
                    UserId = "u_test",
                    UserName = "Test"
                });

                var secondAccess = ClientInfo.DefineAccess;
                Assert.NotSame(firstAccess, secondAccess);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
                s_defineAccessField.SetValue(null, originalDefineAccess);
                s_systemConnectorField.SetValue(null, originalSystemConnector);
            }
        }
    }
}
