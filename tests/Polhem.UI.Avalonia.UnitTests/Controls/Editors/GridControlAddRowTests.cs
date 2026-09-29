using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.Definition.Layouts;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// How GridControl.AddRow initializes a new detail row: each row gets its own sys_rowid (primary key) and is linked to
    /// the master's sys_master_rowid, so saving a new detail neither violates NOT NULL or the unique key nor loses its master.
    /// </summary>
    public class GridControlAddRowTests
    {
        private static FormSchema BuildOrderSchema()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "訂單");
            master.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            var detail = schema.Tables!.Add("OrderLine", "訂單明細");
            detail.Fields!.Add(new FormField(SysFields.RowId, "唯一識別", FieldDbType.Guid));
            detail.Fields!.Add(new FormField(SysFields.MasterRowId, "主檔識別", FieldDbType.Guid));
            detail.Fields!.Add(new FormField("qty", "數量", FieldDbType.Integer));
            return schema;
        }

        [Fact]
        [DisplayName("AddRow assigns a new sys_rowid and links the master's sys_master_rowid")]
        public void AddRow_AssignsRowIdAndMasterLink()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var masterRowId = Guid.NewGuid();
            dataObject.MasterRow![SysFields.RowId] = masterRowId;

            var layout = FormLayoutGenerator.Generate(schema, "default").Details![0];
            var grid = new GridControl { AllowEdit = true, EditMode = GridEditMode.InCell };
            grid.Bind(dataObject, layout);

            grid.AddRow();

            var lineTable = dataObject.DataSet.Tables["OrderLine"]!;
            var added = lineTable.Rows[^1];
            Assert.NotEqual(Guid.Empty, (Guid)added[SysFields.RowId]);
            Assert.Equal(masterRowId, (Guid)added[SysFields.MasterRowId]);
        }

        [Fact]
        [DisplayName("AddRow gives two consecutive rows different sys_rowid values")]
        public void AddRow_TwoRows_GetDistinctRowIds()
        {
            var schema = BuildOrderSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.MasterRow![SysFields.RowId] = Guid.NewGuid();

            var layout = FormLayoutGenerator.Generate(schema, "default").Details![0];
            var grid = new GridControl { AllowEdit = true, EditMode = GridEditMode.InCell };
            grid.Bind(dataObject, layout);

            grid.AddRow();
            grid.AddRow();

            var lineTable = dataObject.DataSet.Tables["OrderLine"]!;
            var first = (Guid)lineTable.Rows[0][SysFields.RowId];
            var second = (Guid)lineTable.Rows[1][SysFields.RowId];
            Assert.NotEqual(Guid.Empty, first);
            Assert.NotEqual(Guid.Empty, second);
            Assert.NotEqual(first, second);
        }
    }
}
