using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.Server.Controls;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared grid behaviors on the HTML grid.
    /// </summary>
    public class HtmlGridBehaviorTests : GridBehaviorTests
    {
        private const string RowSelector = "tr.polhem-grid__row";

        /// <inheritdoc />
        protected override Type GridType => typeof(GridControl);

        /// <inheritdoc />
        protected override int RowCount(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(RowSelector).Count;

        /// <inheritdoc />
        protected override int EditorCount(IRenderedComponent<ContainerFragment> cut, int row)
            => cut.FindAll(RowSelector)[row].QuerySelectorAll(".polhem-field-editor").Length;

        /// <inheritdoc />
        protected override void EnterInFirstEditor(IRenderedComponent<ContainerFragment> cut, int row, string text)
            => cut.FindAll(RowSelector)[row].QuerySelector(".polhem-field-editor")!.Change(text);

        /// <inheritdoc />
        protected override bool ShowsToolbar(IRenderedComponent<ContainerFragment> cut)
            => cut.FindAll(".polhem-grid__toolbar").Count == 1;

        /// <inheritdoc />
        protected override void ClickAdd(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-grid__button--add").Click();

        /// <inheritdoc />
        protected override void ClickDelete(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-grid__button--delete").Click();

        /// <inheritdoc />
        protected override void ClickRow(IRenderedComponent<ContainerFragment> cut, int row) => cut.FindAll(RowSelector)[row].Click();

        /// <inheritdoc />
        protected override bool IsSelected(IRenderedComponent<ContainerFragment> cut, int row)
            => cut.FindAll(RowSelector)[row].ClassList.Contains("polhem-grid__row--selected");

        /// <inheritdoc />
        protected override bool IsEditFormOpen(IRenderedComponent<ContainerFragment> cut)
            => cut.FindAll(".polhem-row-edit-panel").Count > 0;

        /// <inheritdoc />
        protected override void CancelEditForm(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(".polhem-row-edit-panel__cancel").Click();

        /// <inheritdoc />
        protected override string HeaderText(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll("th.polhem-grid__header")[column].TextContent;

        /// <inheritdoc />
        protected override bool OffersSort(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll("th.polhem-grid__header")[column].QuerySelector("button.polhem-grid__sort") is not null;

        /// <inheritdoc />
        protected override void ClickSort(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll("th.polhem-grid__header")[column].QuerySelector("button.polhem-grid__sort")!.Click();

        /// <inheritdoc />
        protected override bool ShowsRequiredCue(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll("th.polhem-grid__header")[column].ClassList.Contains("polhem-grid__header--required");

        /// <inheritdoc />
        protected override bool TryDoubleClickRecordCell(IRenderedComponent<ContainerFragment> cut)
        {
            var cells = cut.FindAll(".polhem-grid__cell--opens-record");
            if (cells.Count == 0) return false;
            cells[0].DoubleClick();
            return true;
        }

        /// <inheritdoc />
        protected override void DoubleClickRow(IRenderedComponent<ContainerFragment> cut, int row) => cut.FindAll(RowSelector)[row].DoubleClick();
    }
}
