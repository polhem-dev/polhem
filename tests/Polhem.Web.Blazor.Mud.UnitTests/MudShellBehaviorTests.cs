using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Mud.Shell;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared shell behaviors on the MudBlazor shell.
    /// </summary>
    public class MudShellBehaviorTests : ShellBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudShellBehaviorTests"/>.
        /// </summary>
        public MudShellBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type LayoutType => typeof(PolhemShellLayout);

        /// <inheritdoc />
        protected override string LinkSelector => ".polhem-shell-nav__link a";

        /// <inheritdoc />
        protected override bool IsMenuToggled(IRenderedComponent<ContainerFragment> cut)
            => !cut.Find(".polhem-shell__nav").ClassList.Contains("mud-drawer--open");
    }
}
