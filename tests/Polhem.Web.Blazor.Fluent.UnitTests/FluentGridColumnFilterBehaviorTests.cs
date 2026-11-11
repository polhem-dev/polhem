using AngleSharp.Dom;
using Bunit;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Controls;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared column filter behaviors on the Fluent UI filter, whose boxes are native inputs that write back on
    /// change.
    /// </summary>
    public class FluentGridColumnFilterBehaviorTests : GridColumnFilterBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridColumnFilterBehaviorTests"/>.
        /// </summary>
        public FluentGridColumnFilterBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type FilterType => typeof(GridColumnFilter);

        /// <inheritdoc />
        protected override void EnterFirst(IElement input, string text) => input.Change(text);
    }
}
