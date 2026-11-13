using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// <see cref="RowEditDialog"/> on a host without native windows: the row is edited in an overlay card, OK keeps the
    /// edit and Cancel rolls it back.
    /// </summary>
    public class RowEditDialogTests
    {
        private static (FormDataObject DataObject, LayoutGrid Layout, DataRow Row) CreateRow()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            var detail = schema.Tables.Add("OrderLine", "Lines");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            detail.Fields.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid));
            detail.Fields.Add(new FormField("note", "Note", FieldDbType.String));

            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            var row = lines.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["note"] = "before";
            lines.Rows.Add(row);
            return (dataObject, FormLayoutGenerator.Generate(schema, "default").Details![0], row);
        }

        [Fact]
        [DisplayName("OK returns true, keeps the edited value and removes the overlay card")]
        public void ShowAsync_Committed_ReturnsTrueAndKeepsEdit()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var (dataObject, layout, row) = CreateRow();
            var task = RowEditDialog.ShowAsync(host.Host, dataObject, layout, row);

            row["note"] = "after";
            host.Panel<RowEditPanel>().Commit();

            Assert.True(InlineContinuationScope.Completed(task));
            Assert.Equal("after", row["note"]);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Cancel returns false and rolls the edit back")]
        public void ShowAsync_Cancelled_ReturnsFalseAndRollsBack()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var (dataObject, layout, row) = CreateRow();
            var task = RowEditDialog.ShowAsync(host.Host, dataObject, layout, row);

            row["note"] = "after";
            host.Panel<RowEditPanel>().Cancel();

            Assert.False(InlineContinuationScope.Completed(task));
            Assert.Equal("before", row["note"]);
        }

        [Fact]
        [DisplayName("The card is titled with the layout's caption, or its table name when the caption is empty")]
        public void ShowAsync_Title_FallsBackToTableName()
        {
            using var scope = new InlineContinuationScope();
            var (dataObject, layout, row) = CreateRow();
            var captioned = new OverlayTestHost();
            var task = RowEditDialog.ShowAsync(captioned.Host, dataObject, layout, row);
            Assert.Equal(layout.Caption, captioned.Title);
            captioned.Panel<RowEditPanel>().Cancel();
            InlineContinuationScope.Completed(task);

            layout.Caption = string.Empty;
            var uncaptioned = new OverlayTestHost();
            task = RowEditDialog.ShowAsync(uncaptioned.Host, dataObject, layout, row);

            Assert.Equal("OrderLine", uncaptioned.Title);
            uncaptioned.Panel<RowEditPanel>().Cancel();
            InlineContinuationScope.Completed(task);
        }

        [Fact]
        [DisplayName("A token cancelled before the call throws without opening an edit")]
        public void ShowAsync_AlreadyCancelled_Throws()
        {
            var host = new OverlayTestHost();
            var (dataObject, layout, row) = CreateRow();

            var task = RowEditDialog.ShowAsync(host.Host, dataObject, layout, row, new CancellationToken(canceled: true));

            Assert.True(task.IsCanceled);
            Assert.Empty(host.Overlay.Children);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [DisplayName("Null arguments are rejected")]
        public void ShowAsync_NullArgument_Throws(int nullArgument)
        {
            var (dataObject, layout, row) = CreateRow();

            var task = RowEditDialog.ShowAsync(
                nullArgument == 0 ? null! : new OverlayTestHost().Host,
                nullArgument == 1 ? null! : dataObject,
                nullArgument == 2 ? null! : layout,
                nullArgument == 3 ? null! : row);

            Assert.IsType<ArgumentNullException>(task.Exception?.InnerException);
        }
    }
}
