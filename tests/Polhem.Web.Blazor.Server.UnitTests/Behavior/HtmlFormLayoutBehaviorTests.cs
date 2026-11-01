using Polhem.Web.Blazor.Server.Controls;
using Polhem.Web.Blazor.Server.Controls.Editors;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared record layout and edit form behaviors on the HTML controls.
    /// </summary>
    public class HtmlFormLayoutBehaviorTests : FormLayoutBehaviorTests
    {
        /// <inheritdoc />
        protected override Type PresenterType => typeof(FormLayoutPresenter);

        /// <inheritdoc />
        protected override Type RowEditPanelType => typeof(RowEditPanel);
    }
}
