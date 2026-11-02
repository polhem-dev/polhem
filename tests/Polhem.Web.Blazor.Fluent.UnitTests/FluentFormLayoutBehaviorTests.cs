using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Controls;
using Polhem.Web.Blazor.Fluent.Controls.Editors;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared record layout and edit form behaviors on the Fluent UI controls.
    /// </summary>
    public class FluentFormLayoutBehaviorTests : FormLayoutBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentFormLayoutBehaviorTests"/>.
        /// </summary>
        public FluentFormLayoutBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type PresenterType => typeof(FormLayoutPresenter);

        /// <inheritdoc />
        protected override Type RowEditPanelType => typeof(RowEditPanel);
    }
}
