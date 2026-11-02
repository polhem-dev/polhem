using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared column tests on the Fluent UI UI package.
    /// </summary>
    public class FluentGridColumnTests : GridColumnTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridColumnTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentGridColumnTests(FluentSiteFixture fixture) : base(fixture)
        {
        }

        /// <inheritdoc />
        protected override string ColumnDragItem => ".polhem-grid thead th[draggable=true]";

        /// <inheritdoc />
        protected override string ColumnDropZone => ".polhem-grid thead th";

        /// <inheritdoc />
        protected override string ColumnResizer => ".polhem-grid thead th [resize-handle]";

        /// <inheritdoc />
        protected override string DraggableColumn => ".polhem-grid thead th[draggable=true]";

        // The grid marks a header draggable when it renders, and its script starts listening some round trips later.
        /// <inheritdoc />
        protected override Task WaitForColumnsReadyAsync(IPage page) => page.WaitForTimeoutAsync(2000);
    }
}
