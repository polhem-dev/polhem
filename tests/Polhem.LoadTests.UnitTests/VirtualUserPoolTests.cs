using System.ComponentModel;
using Polhem.LoadTests.Configuration;
using Polhem.LoadTests.Scenarios;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="VirtualUserPool"/>'s up-front sign-in.
    /// </summary>
    /// <remarks>
    /// These run with no backend hosted, so signing in cannot succeed. That is the case under
    /// test: what matters is that the failure is reported as a failed precondition naming the
    /// account, rather than escaping to be counted against whichever scenario asked for a session.
    /// </remarks>
    public class VirtualUserPoolTests
    {
        private static AuthOptions Auth() => new()
        {
            UserIdPrefix = "loadtest_user_",
            UserPoolSize = 4,
            Password = "irrelevant",
        };

        [Fact]
        [DisplayName("SignInAllAsync throws a message naming the virtual user and account when sign-in fails, instead of letting the failure land on a scenario")]
        public async Task SignInAllAsync_SignInFails_MessageNamesTheVirtualUserAndAccount()
        {
            var pool = new VirtualUserPool(Auth());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => pool.SignInAllAsync(virtualUsers: 1));

            // Virtual user 0 maps onto the first account in the pool. Both halves are asserted
            // because the index alone does not tell an operator which credentials to check.
            Assert.Contains("Virtual user 0", exception.Message, StringComparison.Ordinal);
            Assert.Contains("loadtest_user_0", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAllAsync keeps the original exception as the inner exception when sign-in fails")]
        public async Task SignInAllAsync_SignInFails_PreservesTheUnderlyingException()
        {
            var pool = new VirtualUserPool(Auth());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => pool.SignInAllAsync(virtualUsers: 1));

            Assert.NotNull(exception.InnerException);
        }

        [Theory]
        [InlineData(0, "loadtest_user_0")]
        [InlineData(3, "loadtest_user_3")]
        [InlineData(4, "loadtest_user_0")]
        [InlineData(9, "loadtest_user_1")]
        [DisplayName("ResolveUserId wraps virtual users beyond the pool size back onto the pooled accounts")]
        public void ResolveUserId_MoreVirtualUsersThanAccounts_WrapsOntoThePool(int virtualUserIndex, string expected)
        {
            // Tested directly: without a backend `SignInAllAsync` stops at virtual user 0, so the wrap is never
            // reached through it. The failure message names this same account, so an operator is never sent
            // looking for "loadtest_user_4", which does not exist in a pool of four.
            var pool = new VirtualUserPool(Auth());

            Assert.Equal(expected, pool.ResolveUserId(virtualUserIndex));
        }

        [Fact]
        [DisplayName("ResolveUserId maps every virtual user to the first account under the Shared token strategy")]
        public void ResolveUserId_SharedTokenStrategy_AlwaysFirstAccount()
        {
            var auth = Auth();
            auth.TokenStrategy = TokenStrategy.Shared;
            var pool = new VirtualUserPool(auth);

            Assert.Equal("loadtest_user_0", pool.ResolveUserId(2));
            Assert.Equal("loadtest_user_0", pool.ResolveUserId(7));
        }

        [Fact]
        [DisplayName("SignInAllAsync does not attempt to sign in when there are no virtual users")]
        public async Task SignInAllAsync_ZeroVirtualUsers_DoesNotAttemptSignIn()
        {
            // Guards the loop bound: a zero-user run has nothing to prepare, and reporting a
            // sign-in failure for a run that drives nobody would be a false alarm.
            var pool = new VirtualUserPool(Auth());

            var exception = await Record.ExceptionAsync(() => pool.SignInAllAsync(virtualUsers: 0));

            Assert.Null(exception);
        }
    }
}
