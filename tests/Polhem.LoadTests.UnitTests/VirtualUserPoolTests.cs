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

        [Fact]
        [DisplayName("SignInAllAsync with more virtual users than accounts names a pooled account in the failure message")]
        public async Task SignInAllAsync_MoreVirtualUsersThanAccounts_WrapsOntoThePool()
        {
            // The pool holds four accounts, so the fifth virtual user reuses the first. The
            // message must name the account actually used, not the virtual user index — an
            // operator checking "loadtest_user_4" would find no such row.
            var pool = new VirtualUserPool(Auth());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => pool.SignInAllAsync(virtualUsers: 5));

            Assert.Contains("Virtual user 0", exception.Message, StringComparison.Ordinal);
            Assert.Contains("loadtest_user_0", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("SignInAllAsync under the Shared token strategy names the first account in the failure message")]
        public async Task SignInAllAsync_SharedTokenStrategy_UsesTheFirstAccount()
        {
            var auth = Auth();
            auth.TokenStrategy = TokenStrategy.Shared;
            var pool = new VirtualUserPool(auth);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => pool.SignInAllAsync(virtualUsers: 3));

            Assert.Contains("loadtest_user_0", exception.Message, StringComparison.Ordinal);
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
