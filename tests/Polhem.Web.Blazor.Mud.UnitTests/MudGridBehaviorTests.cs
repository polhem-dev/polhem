using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Controls;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared grid behaviors on the MudBlazor grid. Its edit form is a dialog, which renders in the dialog
    /// provider, and a cell opens its editor only when it takes the focus.
    /// </summary>
    public class MudGridBehaviorTests : GridBehaviorTests
    {
        private const string RowSelector = "tr.polhem-grid__row";
        private readonly IRenderedComponent<MudDialogProvider> _dialogs;

        /// <summary>
        /// Initializes a new instance of <see cref="MudGridBehaviorTests"/>.
        /// </summary>
        public MudGridBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
            Render<MudPopoverProvider>();
            _dialogs = Render<MudDialogProvider>();
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
            cut.FindAll(RowSelector)[row].QuerySelector(".polhem-grid-cell__editor input")!.Change(text);
        }

        /// <inheritdoc />
        protected override bool ShowsToolbar(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(".polhem-grid__toolbar").Count == 1;

        /// <inheritdoc />
        protected override void ClickAdd(IRenderedComponent<ContainerFragment> cut) => cut.Find("button.polhem-grid__button--add").Click();

        /// <inheritdoc />
        protected override void ClickDelete(IRenderedComponent<ContainerFragment> cut) => cut.Find("button.polhem-grid__button--delete").Click();

        /// <inheritdoc />
        protected override void ClickRow(IRenderedComponent<ContainerFragment> cut, int row) => cut.FindAll(RowSelector)[row].Click();

        /// <inheritdoc />
        protected override bool IsSelected(IRenderedComponent<ContainerFragment> cut, int row)
            => cut.FindAll(RowSelector)[row].ClassList.Contains("polhem-grid__row--selected");

        /// <inheritdoc />
        protected override bool IsEditFormOpen(IRenderedComponent<ContainerFragment> cut) => _dialogs.FindAll(".polhem-row-edit-panel").Count > 0;

        /// <inheritdoc />
        protected override void ConfirmEditForm(IRenderedComponent<ContainerFragment> cut) => _dialogs.Find(".polhem-row-edit-panel__ok").Click();

        /// <inheritdoc />
        protected override void EnterInEditForm(IRenderedComponent<ContainerFragment> cut, string fieldName, string text)
            => _dialogs.Find($"#polhem-row-edit-{fieldName}").Change(text);

        /// <inheritdoc />
        protected override void CancelEditForm(IRenderedComponent<ContainerFragment> cut) => _dialogs.Find("button.polhem-row-edit-panel__cancel").Click();

        /// <inheritdoc />
        protected override string HeaderText(IRenderedComponent<ContainerFragment> cut, int column) => cut.FindAll("th.polhem-grid__header")[column].TextContent.Trim();

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
        protected override void DoubleClickRow(IRenderedComponent<ContainerFragment> cut, int row) => cut.FindAll(RowSelector)[row].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Detail = 2 });
    }
}
