using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.Server.Shell;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared shell behaviors on the HTML shell.
    /// </summary>
    public class HtmlShellBehaviorTests : ShellBehaviorTests
    {
        /// <inheritdoc />
        protected override Type LayoutType => typeof(PolhemShellLayout);

        /// <inheritdoc />
        protected override string LinkSelector => "a.polhem-shell-nav__link";

        /// <inheritdoc />
        protected override bool IsMenuToggled(IRenderedComponent<ContainerFragment> cut)
            => cut.Find("nav.polhem-shell__nav").ClassList.Contains("polhem-shell__nav--toggled");
    }
}
