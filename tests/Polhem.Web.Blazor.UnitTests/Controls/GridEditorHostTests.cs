using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Controls;
using Polhem.UI.Core.DataObjects;

namespace Polhem.Web.Blazor.UnitTests.Controls
{
    /// <summary>
    /// A grid keeps one editor open, and closes it when it stops editing in cells.
    /// </summary>
    public class GridEditorHostTests : BunitContext
    {
        [Fact]
        [DisplayName("Activating a cell closes the editor of the previous cell and renders both")]
        public void Activate_AnotherSlot_RefreshesBoth()
        {
            var host = new GridEditorHost();
            var first = new Slot();
            var second = new Slot();
            host.Activate(first);

            host.Activate(second);

            Assert.Same(second, host.Active);
            Assert.Equal(2, first.Refreshes);
            Assert.Equal(1, second.Refreshes);
        }

        [Fact]
        [DisplayName("Activating the cell that already has the editor renders nothing again, so its input keeps the focus")]
        public void Activate_SameSlot_DoesNotRefresh()
        {
            var host = new GridEditorHost();
            var slot = new Slot();
            host.Activate(slot);

            host.Activate(slot);

            Assert.Equal(1, slot.Refreshes);
        }

        [Fact]
        [DisplayName("Closing returns the active cell to text")]
        public void Close_ActiveSlot_ClearsAndRefreshes()
        {
            var host = new GridEditorHost();
            var slot = new Slot();
            host.Activate(slot);

            host.Close();

            Assert.Null(host.Active);
            Assert.Equal(2, slot.Refreshes);
        }

        [Fact]
        [DisplayName("Releasing a disposed cell forgets it without rendering it, and another cell's release changes nothing")]
        public void Release_OnlyTheActiveSlot_ClearsWithoutRefresh()
        {
            var host = new GridEditorHost();
            var slot = new Slot();
            host.Activate(slot);

            host.Release(new Slot());
            Assert.Same(slot, host.Active);

            host.Release(slot);
            Assert.Null(host.Active);
            Assert.Equal(1, slot.Refreshes);
        }

        [Fact]
        [DisplayName("A grid closes its open editor when the form enters View mode")]
        public void SetControlState_View_ClosesTheEditor()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order");
            schema.Tables.Add("Line", "Line").Fields!.Add("item", "Item", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var layout = new LayoutGrid { TableName = "Line", AllowActions = GridControlAllowActions.Edit };
            var grid = Render<GridProbe>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.AllowEdit, null)).Instance;
            var slot = new Slot();
            grid.Host.Activate(slot);
            Assert.Same(slot, grid.Host.Active);

            grid.SetControlState(SingleFormMode.View);

            Assert.Null(grid.Host.Active);
        }

        private sealed class Slot : IGridEditorSlot
        {
            public int Refreshes { get; private set; }
            public void RefreshEditorState() => Refreshes++;
        }

        private sealed class GridProbe : GridControlBase
        {
            public GridEditorHost Host => EditorHost;
        }
    }
}
