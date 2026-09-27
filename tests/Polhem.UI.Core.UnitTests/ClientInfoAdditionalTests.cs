using System.ComponentModel;
using Polhem.Api.Core.Messages.System;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the non-mutating paths of <see cref="ClientInfo"/> not covered elsewhere.
    /// <c>ClientInfoConnectorTests</c> in <c>ClientInfoCoverageTests</c> sets the static field
    /// <c>_systemConnector</c> to null through reflection, which races with the caching tests here,
    /// so this class must run serially in the same collection.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoReadOnlyTests
    {
        [Fact]
        [DisplayName("GetEndpoint returns a string without throwing")]
        public void GetEndpoint_Default_ReturnsStringWithoutThrowing()
        {
            var result = ClientInfo.GetEndpoint();
            Assert.NotNull(result);
        }

        [Fact]
        [DisplayName("SystemApiConnector getter lazily creates a non-null instance")]
        public void SystemApiConnector_LocalConnectType_ReturnsNotNull()
        {
            var connector = ClientInfo.SystemApiConnector;
            Assert.NotNull(connector);
        }

        [Fact]
        [DisplayName("SystemApiConnector getter returns the same cached instance on repeated access")]
        public void SystemApiConnector_AccessedTwice_ReturnsSameInstance()
        {
            var first = ClientInfo.SystemApiConnector;
            var second = ClientInfo.SystemApiConnector;
            Assert.Same(first, second);
        }

        [Fact]
        [DisplayName("DefineAccess getter lazily creates a non-null ClientDefineAccess instance")]
        public void DefineAccess_LocalConnectType_ReturnsNotNull()
        {
            var access = ClientInfo.DefineAccess;
            Assert.NotNull(access);
        }

        [Fact]
        [DisplayName("CreateFormApiConnector returns a non-null FormApiConnector instance")]
        public void CreateFormApiConnector_LocalConnectType_ReturnsNotNull()
        {
            var connector = ClientInfo.CreateFormApiConnector("TestProg");
            Assert.Equal("TestProg", connector.ProgId);
        }
    }

    /// <summary>
    /// Covers the paths of <see cref="ClientInfo"/> that mutate static state.
    /// Shares the <c>ClientInfoState</c> collection with the other ClientInfo tests so they run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoMutatingTests
    {
        [Fact]
        [DisplayName("ApplyLoginResult with a valid LoginResponse sets AccessToken and UserInfo")]
        public void ApplyLoginResult_ValidLoginResponse_SetsAccessTokenAndUserInfo()
        {
            var token = Guid.NewGuid();
            var response = new LoginResponse
            {
                AccessToken = token,
                UserId = "u001",
                UserName = "測試使用者"
            };
            try
            {
                ClientInfo.ApplyLoginResult(response);
                Assert.Equal(token, ClientInfo.AccessToken);
                Assert.NotNull(ClientInfo.UserInfo);
                Assert.Equal("u001", ClientInfo.UserInfo!.UserId);
                Assert.Equal("測試使用者", ClientInfo.UserInfo.UserName);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
            }
        }

        [Fact]
        [DisplayName("ApplyLoginResult called twice with different tokens keeps the last result")]
        public void ApplyLoginResult_CalledTwice_LastResultWins()
        {
            var tokenA = Guid.NewGuid();
            var tokenB = Guid.NewGuid();
            try
            {
                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = tokenA,
                    UserId = "userA",
                    UserName = "A"
                });
                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = tokenB,
                    UserId = "userB",
                    UserName = "B"
                });
                Assert.Equal(tokenB, ClientInfo.AccessToken);
                Assert.Equal("userB", ClientInfo.UserInfo!.UserId);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
            }
        }
    }
}
