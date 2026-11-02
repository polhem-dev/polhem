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
    }
}
