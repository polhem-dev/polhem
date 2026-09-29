using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Additional tests for FormLayoutGenerator:
    /// the ControlType inference of detail grids, the system field allowlist (sys_rowid + sys_master_rowid),
    /// and the empty-field edge cases of master and detail tables.
    /// </summary>
    public class FormLayoutGeneratorExtraTests
    {
        private static FormSchema BuildMasterDetailSchema()
        {
            var schema = new FormSchema("Order", "訂單");
            var master = schema.Tables!.Add("Order", "訂單");
            master.Fields!.Add("sys_id", "編號", FieldDbType.String);
            return schema;
        }

        [Theory]
        [InlineData(ControlType.TextEdit, ControlType.TextEdit)]
        [InlineData(ControlType.ButtonEdit, ControlType.ButtonEdit)]
        [InlineData(ControlType.DateEdit, ControlType.DateEdit)]
        [InlineData(ControlType.YearMonthEdit, ControlType.YearMonthEdit)]
        [InlineData(ControlType.DropDownEdit, ControlType.DropDownEdit)]
        [InlineData(ControlType.CheckEdit, ControlType.CheckEdit)]
        [InlineData(ControlType.MemoEdit, ControlType.MemoEdit)]
        [InlineData(ControlType.DateTimeEdit, ControlType.DateTimeEdit)]
        [DisplayName("FormLayoutGenerator.Generate keeps a non-Auto ControlType of a detail table as is")]
        public void Generate_DetailTable_NonAutoControlType_PreservesValue(
            ControlType controlType, ControlType expected)
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField("col", "欄", FieldDbType.String) { ControlType = controlType });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            Assert.Equal(expected, grid.Columns![0].ControlType);
        }

        [Theory]
        [InlineData(FieldDbType.Boolean, ControlType.CheckEdit)]
        [InlineData(FieldDbType.DateTime, ControlType.DateTimeEdit)]
        [InlineData(FieldDbType.Text, ControlType.MemoEdit)]
        [InlineData(FieldDbType.String, ControlType.TextEdit)]
        [InlineData(FieldDbType.Integer, ControlType.NumericEdit)]
        [DisplayName("FormLayoutGenerator.Generate infers the ControlType from the DbType for ControlType=Auto in a detail table")]
        public void Generate_DetailTable_AutoControlType_MapsDbTypeToControlType(
            FieldDbType dbType, ControlType expected)
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField("col", "欄", dbType) { ControlType = ControlType.Auto });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            Assert.Equal(expected, grid.Columns![0].ControlType);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate passes the Width of a detail table through to LayoutColumn")]
        public void Generate_DetailTable_PassesWidth()
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField("col", "欄", FieldDbType.String) { Width = 250 });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            Assert.Equal(250, grid.Columns![0].Width);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate keeps a detail table Width=0 as 0 (auto/unset)")]
        public void Generate_DetailTable_WidthZero_StaysZero()
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add("col", "欄", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            Assert.Equal(0, grid.Columns![0].Width);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate skips non-system fields with Visible=false in a detail table")]
        public void Generate_DetailTable_SkipsInvisibleNonSystemFields()
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField("hidden", "隱藏", FieldDbType.String) { Visible = false });
            detail.Fields!.Add("visible", "顯示", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            Assert.Single(grid.Columns!);
            Assert.Equal("visible", grid.Columns![0].FieldName);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate adds no grid for a detail table whose fields are all invisible")]
        public void Generate_DetailTable_AllFieldsInvisible_DoesNotAddGrid()
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField("hidden", "隱藏", FieldDbType.String) { Visible = false });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Empty(layout.Details!);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate adds an existing sys_rowid of a detail table to the grid with Visible=false")]
        public void Generate_DetailTable_AddsHiddenRowIdColumn()
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField(SysFields.RowId, "Row ID", FieldDbType.Guid) { Visible = false });
            detail.Fields!.Add("col", "欄", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            var rowId = grid.Columns!.FirstOrDefault(c => c.FieldName == SysFields.RowId);
            Assert.NotNull(rowId);
            Assert.False(rowId!.Visible);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate adds an existing sys_master_rowid of a detail table to the grid with Visible=false")]
        public void Generate_DetailTable_AddsHiddenMasterRowIdColumn()
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid) { Visible = false });
            detail.Fields!.Add("col", "欄", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            var masterRowId = grid.Columns!.FirstOrDefault(c => c.FieldName == SysFields.MasterRowId);
            Assert.NotNull(masterRowId);
            Assert.False(masterRowId!.Visible);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate does not add system fields to the master automatically (the allowlist applies only to grids)")]
        public void Generate_MasterSection_DoesNotAutoAddSystemFields()
        {
            var schema = new FormSchema("Demo", "示範");
            var master = schema.Tables!.Add("Demo", "示範");
            master.Fields!.Add(new FormField(SysFields.RowId, "Row ID", FieldDbType.Guid) { Visible = false });
            master.Fields!.Add("name", "名稱", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Single(layout.Sections!);
            Assert.DoesNotContain(layout.Sections![0].Fields!, f => f.FieldName == SysFields.RowId);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate produces no master Section without a master table and keeps only the detail grid")]
        public void Generate_NoMasterTable_OnlyDetailGrid()
        {
            // The ProgId does not match any table, so `MasterTable` is null.
            var schema = new FormSchema("NotExist", "不存在");
            var other = schema.Tables!.Add("Other", "其他");
            other.Fields!.Add("col", "欄", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Empty(layout.Sections!);
            Assert.Single(layout.Details!);
            Assert.Equal("Other", layout.Details![0].TableName);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("FormLayoutGenerator.Generate passes FormField.ReadOnly of a master field to LayoutField.ReadOnly")]
        public void Generate_MasterField_PropagatesReadOnly(bool readOnly)
        {
            var schema = new FormSchema("Demo", "示範");
            var master = schema.Tables!.Add("Demo", "示範");
            master.Fields!.Add(new FormField("amount", "金額", FieldDbType.Currency) { ReadOnly = readOnly });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var field = layout.Sections![0].Fields!.First(f => f.FieldName == "amount");
            Assert.Equal(readOnly, field.ReadOnly);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("FormLayoutGenerator.Generate passes FormField.ReadOnly of a detail field to LayoutColumn.ReadOnly")]
        public void Generate_DetailColumn_PropagatesReadOnly(bool readOnly)
        {
            var schema = BuildMasterDetailSchema();
            var detail = schema.Tables!.Add("OrderItem", "訂單明細");
            detail.Fields!.Add(new FormField("amount", "金額", FieldDbType.Currency) { ReadOnly = readOnly });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var grid = layout.Details!.First(g => g.TableName == "OrderItem");
            var column = grid.Columns!.First(c => c.FieldName == "amount");
            Assert.Equal(readOnly, column.ReadOnly);
        }
    }
}
