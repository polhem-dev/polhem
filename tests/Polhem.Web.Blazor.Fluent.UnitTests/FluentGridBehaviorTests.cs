using System.ComponentModel;
using Bunit;
using Bunit.Rendering;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Controls;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared grid behaviors on the Fluent UI grid. Its edit form is an inline dialog, and a cell opens its editor
    /// only when it takes the focus.
    /// </summary>
    public class FluentGridBehaviorTests : GridBehaviorTests
    {
        private const string RowSelector = "tr.polhem-grid__row";
        private const string HeaderSelector = "thead th";

        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridBehaviorTests"/>.
        /// </summary>
        public FluentGridBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type GridType => typeof(GridControl);

        /// <inheritdoc />
        protected override int RowCount(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(RowSelector).Count;

        /// <inheritdoc />
        protected override int EditorCount(IRenderedComponent<ContainerFragment> cut, int row)
            => cut.FindAll(RowSelector)[row].QuerySelectorAll(".polhem-grid-cell__text, .polhem-grid-cell__editor").Length;

        /// <inheritdoc />
        protected override void EnterInFirstEditor(IRenderedComponent<ContainerFragment> cut, int row, string text)
        {
            cut.FindAll(RowSelector)[row].QuerySelector(".polhem-grid-cell__text")!.Focus();
            cut.FindAll(RowSelector)[row].QuerySelector(".polhem-grid-cell__editor fluent-text-input")!.Change(text);
        }

        /// <inheritdoc />
        protected override bool ShowsToolbar(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(".polhem-grid__toolbar").Count == 1;

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
        protected override bool IsEditFormOpen(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(".polhem-row-edit-panel").Count > 0;

        /// <inheritdoc />
        protected override void ConfirmEditForm(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-row-edit-panel__ok").Click();

        /// <inheritdoc />
        protected override void EnterInEditForm(IRenderedComponent<ContainerFragment> cut, string fieldName, string text)
            => cut.Find($"#polhem-row-edit-{fieldName}").Change(text);

        /// <inheritdoc />
        protected override void CancelEditForm(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-row-edit-panel__cancel").Click();

        /// <inheritdoc />
        protected override string HeaderText(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll(HeaderSelector)[column].QuerySelector(".polhem-grid__header")!.TextContent.Replace("▾", string.Empty, StringComparison.Ordinal).Trim();

        /// <inheritdoc />
        protected override bool OffersSort(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll(HeaderSelector)[column].QuerySelector("button.polhem-grid__sort") is not null;

        /// <inheritdoc />
        protected override void ClickSort(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll(HeaderSelector)[column].QuerySelector("button.polhem-grid__sort")!.Click();

        /// <inheritdoc />
        protected override bool ShowsRequiredCue(IRenderedComponent<ContainerFragment> cut, int column)
            => cut.FindAll(HeaderSelector)[column].QuerySelector(".polhem-grid__header--required") is not null;

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

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [DisplayName("Only a grid that is not compact offers to resize and move its columns")]
        public void Columns_OfferResizeAndMove_UnlessCompact(bool compact, bool expected)
        {
            var layout = new LayoutGrid { TableName = "Line" };
            layout.Columns!.Add(new LayoutColumn("item", "Item", ControlType.TextEdit));

            var cut = Render<GridControl>(p => p.Add(g => g.Layout, layout).Add(g => g.Compact, compact));

            var grid = cut.FindComponent<Microsoft.FluentUI.AspNetCore.Components.FluentDataGrid<System.Data.DataRow>>().Instance;
            Assert.Equal(expected, grid.ResizableColumns);
            Assert.Equal(expected, grid.ReorderableColumns);
        }
    }
}
