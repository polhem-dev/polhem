using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared column tests on the MudBlazor UI package.
    /// </summary>
    public class MudGridColumnTests : GridColumnTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudGridColumnTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudGridColumnTests(MudSiteFixture fixture) : base(fixture)
        {
        }

        /// <inheritdoc />
        protected override string ColumnDragItem => ".polhem-grid thead .mud-drop-item";

        /// <inheritdoc />
        protected override string ColumnDropZone => ".polhem-grid thead .mud-drop-zone";

        /// <inheritdoc />
        protected override string ColumnResizer => ".polhem-grid thead .mud-resizer";

        /// <inheritdoc />
        protected override string DraggableColumn => ".polhem-grid thead .mud-drop-item-draggable";

        // MudBlazor handles the resize on the server. The render that carries a new width also marks the resizer
        // `mud-resizing`, and the release handler removes that class, then releases the pointer capture and clears the
        // grid's `cursor-col-resize` as its last step. Until then a new drag on the same column is ended by it.
        /// <inheritdoc />
        protected override async Task WaitForResizeEndAsync(IPage page)
        {
            await Assertions.Expect(page.Locator(".polhem-grid .mud-resizer.mud-resizing")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator(".polhem-grid .cursor-col-resize")).ToHaveCountAsync(0);
        }
    }
}
