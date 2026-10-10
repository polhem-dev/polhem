using System.ComponentModel;
using Polhem.Api.Core.Messages.System;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the <see cref="ClientInfo.DefineAccess"/> getter:
    /// the caching behavior (two accesses return the same instance) and
    /// the cache being cleared on a sign-in (two accesses return different instances).
    /// It mutates static state, so it runs serially in the <c>ClientInfoState</c> collection.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoDefineAccessTests
    {
        [Fact]
        [DisplayName("DefineAccess getter returns the same cached instance on repeated access")]
        public void DefineAccess_AccessedTwice_ReturnsSameInstance()
        {
            using var preserved = ClientInfoTestState.Preserve();
            ClientInfoTestState.UseClient(null);

            var first = ClientInfo.DefineAccess;
            var second = ClientInfo.DefineAccess;

            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("DefineAccess getter returns a new instance after a sign-in (the cache is cleared)")]
        public void DefineAccess_AfterTokenChange_ReturnsNewInstance()
        {
            using var preserved = ClientInfoTestState.Preserve();
            ClientInfoTestState.UseClient(null);
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
    }
}
