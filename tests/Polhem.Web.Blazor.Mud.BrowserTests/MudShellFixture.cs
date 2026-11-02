using Polhem.Web.Blazor.BehaviorTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// The test site with the shell's services and backend.
    /// </summary>
    public sealed class MudShellFixture : MudSiteFixture
    {
        /// <summary>Gets the shell's backend.</summary>
        public ShellTestBackend ShellBackend { get; } = new();

        /// <inheritdoc />
        protected override void RegisterBackend(IServiceCollection services) => ShellBackend.Register(services);
    }
}
