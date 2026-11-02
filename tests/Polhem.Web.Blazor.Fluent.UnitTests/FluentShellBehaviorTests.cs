using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.DependencyInjection;
using Polhem.Web.Blazor.Fluent.Shell;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared shell behaviors on the Fluent UI shell.
    /// </summary>
    public class FluentShellBehaviorTests : ShellBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentShellBehaviorTests"/>.
        /// </summary>
        public FluentShellBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type LayoutType => typeof(PolhemShellLayout);

        /// <inheritdoc />
        protected override Type NavMenuType => typeof(PolhemShellNavMenu);

        /// <inheritdoc />
        protected override string LinkSelector => "a.polhem-shell-nav__link";

        /// <inheritdoc />
        protected override string Language => "fluent-button.polhem-shell-signin__language";

        /// <inheritdoc />
        protected override bool IsMenuToggled(IRenderedComponent<ContainerFragment> cut)
            => !cut.Find(".polhem-shell").ClassList.Contains("polhem-shell--menu-open");
    }
}
