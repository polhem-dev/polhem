using Bunit;
using MudBlazor;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Controls;
using Polhem.Web.Blazor.Mud.Controls.Editors;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared record layout and edit form behaviors on the MudBlazor controls.
    /// </summary>
    public class MudFormLayoutBehaviorTests : FormLayoutBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudFormLayoutBehaviorTests"/>.
        /// </summary>
        public MudFormLayoutBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
            Render<MudPopoverProvider>();
        }

        /// <inheritdoc />
        protected override Type PresenterType => typeof(FormLayoutPresenter);

        /// <inheritdoc />
        protected override Type RowEditPanelType => typeof(RowEditPanel);
    }
}
