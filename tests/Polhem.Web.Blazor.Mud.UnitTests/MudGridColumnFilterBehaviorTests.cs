using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Controls;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared column filter behaviors on the MudBlazor filter, whose text fields take each keystroke.
    /// </summary>
    public class MudGridColumnFilterBehaviorTests : GridColumnFilterBehaviorTests
    {
        private IRenderedComponent<MudPopoverProvider> _popovers = default!;

        /// <summary>
        /// Initializes a new instance of <see cref="MudGridColumnFilterBehaviorTests"/>.
        /// </summary>
        public MudGridColumnFilterBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type FilterType => typeof(GridColumnFilter);

        /// <inheritdoc />
        protected override void OnBeforeRender() => _popovers = Render<MudPopoverProvider>();

        /// <inheritdoc />
        protected override IReadOnlyList<IElement> FindInPanel(IRenderedComponent<ContainerFragment> cut, string selector)
            => _popovers.FindAll(selector);

        /// <inheritdoc />
        protected override void EnterFirst(IElement input, string text) => input.Input(text);
    }
}
