using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared login panel behaviors on the HTML panel.
    /// </summary>
    public class HtmlLoginPanelBehaviorTests : LoginPanelBehaviorTests
    {
        /// <inheritdoc />
        protected override Type PanelType => typeof(PolhemLoginPanel);
    }
}
