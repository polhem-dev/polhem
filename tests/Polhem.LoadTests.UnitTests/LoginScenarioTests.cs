using System.ComponentModel;
using Polhem.LoadTests.Configuration;
using Polhem.LoadTests.Scenarios;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="LoginScenario"/>'s account selection.
    /// </summary>
    public class LoginScenarioTests
    {
        private static AuthOptions Auth(TokenStrategy strategy, int poolSize) => new()
        {
            TokenStrategy = strategy,
            UserPoolSize = poolSize,
            UserIdPrefix = "loadtest_user_"
        };

        [Fact]
        [DisplayName("ResolveUserId gives each virtual user its own account under the PerUser strategy")]
        public void ResolveUserId_PerUser_GivesEachVirtualUserItsOwn()
        {
            var scenario = new LoginScenario(Auth(TokenStrategy.PerUser, 10));

            Assert.Equal("loadtest_user_0", scenario.ResolveUserId(0));
            Assert.Equal("loadtest_user_3", scenario.ResolveUserId(3));
        }

        [Fact]
        [DisplayName("ResolveUserId wraps around when there are more virtual users than accounts, so the run still works")]
        public void ResolveUserId_MoreUsersThanAccounts_WrapsAround()
        {
            var scenario = new LoginScenario(Auth(TokenStrategy.PerUser, 3));

            Assert.Equal("loadtest_user_0", scenario.ResolveUserId(3));
            Assert.Equal("loadtest_user_1", scenario.ResolveUserId(7));
        }

        [Fact]
        [DisplayName("ResolveUserId gives every virtual user the first account under the Shared strategy")]
        public void ResolveUserId_Shared_AlwaysFirstAccount()
        {
            var scenario = new LoginScenario(Auth(TokenStrategy.Shared, 10));

            Assert.Equal("loadtest_user_0", scenario.ResolveUserId(0));
            Assert.Equal("loadtest_user_0", scenario.ResolveUserId(9));
        }

        [Fact]
        [DisplayName("ResolveUserId does not divide by zero when the account pool size is 0")]
        public void ResolveUserId_ZeroPoolSize_DoesNotDivideByZero()
        {
            var scenario = new LoginScenario(Auth(TokenStrategy.PerUser, 0));

            Assert.Equal("loadtest_user_0", scenario.ResolveUserId(5));
        }

        [Fact]
        [DisplayName("LoginScenario name matches the key in the configuration file")]
        public void Name_MatchesConfigurationKey()
        {
            Assert.Equal("Login", new LoginScenario(Auth(TokenStrategy.PerUser, 1)).Name);
        }
    }
}
