using Bunit;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Components;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared login panel behaviors on the MudBlazor panel.
    /// </summary>
    public class MudLoginPanelBehaviorTests : LoginPanelBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudLoginPanelBehaviorTests"/>.
        /// </summary>
        public MudLoginPanelBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type PanelType => typeof(PolhemLoginPanel);
    }
}
