using Polhem.Api.Contracts.Form;
using Polhem.UI.Core.Definitions;
using Polhem.Web.Blazor.BehaviorTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// The test site with a lookup target and no form backend.
    /// </summary>
    public sealed class MudLookupFixture : MudSiteFixture
    {
        /// <summary>Gets the lookup target.</summary>
        public FakeLookupSource Source { get; } = new();

        /// <inheritdoc />
        protected override void RegisterBackend(IServiceCollection services)
        {
            services.AddSingleton<IFormDefinitionSource>(Source);
            services.AddSingleton<IFormApiConnectorFactory>(Source);
        }
    }
}
