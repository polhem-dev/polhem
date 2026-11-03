using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared column tests on the Fluent UI UI package.
    /// </summary>
    public class FluentGridColumnTests : GridColumnTests, IClassFixture<FluentSiteFixture>
    {
        private readonly FluentSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridColumnTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentGridColumnTests(FluentSiteFixture fixture) : base(fixture) => _fixture = fixture;

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
    
        [BrowserFact]
        [DisplayName("Dragging the edge back narrows the column by the distance dragged")]
        public async Task DragEdgeBack_NarrowsByTheDistance()
        {
            var page = await OpenAsync(_fixture.Address);
            float before = await WidthAsync(page);
            await DragResizerAsync(page, 80, 300);
            await WaitForWidthAsync(page, width => width >= before + 60);

            await DragResizerAsync(page, -40, 300);

            await WaitForWidthAsync(page, width => width < before + 60);
            await page.WaitForTimeoutAsync(300);
            // The grid's table filled its container and spread the space its columns left over across them, so a column
            // dragged narrower came out wider (polhem-fluent.css).
            Assert.InRange(await WidthAsync(page), before + 25, before + 55);
        }
    }
}
