using System.Globalization;
using Polhem.Api.Client;
using Polhem.LoadTests.Configuration;
using Polhem.LoadTests.Running;

namespace Polhem.LoadTests.Scenarios
{
    /// <summary>
    /// Signs an account in, measuring the only path that creates a session.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: every call creates its own <see cref="PolhemApiClient"/>. A client holds one
    /// signed-in identity, so concurrent sign-ins through a shared one would overwrite one another's
    /// token and transmission key; the symptom is not a clear error but decryption failures
    /// elsewhere that read like framework instability under load.
    /// </remarks>
    public sealed class LoginScenario : IScenario
    {
        private readonly AuthOptions _auth;
        private readonly Func<PolhemApiClient>? _createClient;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="auth">Authentication configuration.</param>
        /// <param name="createClient">Creates a new client for one sign-in; required to execute.</param>
        public LoginScenario(AuthOptions auth, Func<PolhemApiClient>? createClient = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _createClient = createClient;
        }

        /// <inheritdoc/>
        public string Name => "Login";

        /// <inheritdoc/>
        public async Task ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
        {
            var userId = ResolveUserId(context.VirtualUserIndex);
            var client = (_createClient ?? throw new InvalidOperationException("Signing in needs a client factory."))();
            await client.System.LoginAsync(userId, _auth.Password).ConfigureAwait(false);
        }

        /// <summary>
        /// Maps a virtual user to the account it signs in as.
        /// </summary>
        /// <param name="virtualUserIndex">The virtual user index.</param>
        /// <returns>The account id.</returns>
        /// <remarks>
        /// Under <see cref="TokenStrategy.Shared"/> every user signs in as the first account,
        /// which measures one account being hammered. Under
        /// <see cref="TokenStrategy.PerUser"/> the index wraps around the seeded pool, so a run
        /// with more virtual users than accounts still works — it just shares accounts.
        /// </remarks>
        internal string ResolveUserId(int virtualUserIndex)
        {
            var index = _auth.TokenStrategy == TokenStrategy.Shared
                ? 0
                : virtualUserIndex % Math.Max(_auth.UserPoolSize, 1);
            return _auth.UserIdPrefix + index.ToString(CultureInfo.InvariantCulture);
        }
    }
}
