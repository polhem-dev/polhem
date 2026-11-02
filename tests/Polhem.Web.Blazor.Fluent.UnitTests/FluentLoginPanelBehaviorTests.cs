using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Components;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared login panel behaviors on the Fluent UI panel.
    /// </summary>
    public class FluentLoginPanelBehaviorTests : LoginPanelBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentLoginPanelBehaviorTests"/>.
        /// </summary>
        public FluentLoginPanelBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type PanelType => typeof(PolhemLoginPanel);
    }
}
