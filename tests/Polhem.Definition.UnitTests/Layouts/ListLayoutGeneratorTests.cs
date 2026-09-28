using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for FormSchema.GetListLayout (through ListLayoutGenerator).
    /// </summary>
    public class ListLayoutGeneratorTests
    {
        [Fact]
        [DisplayName("GetListLayout produces a LayoutGrid with TableName=ProgId and Caption=the master DisplayName")]
        public void GetListLayout_ProducesGridWithProgIdAndCaption()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "sys_id,sys_name" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add("sys_id", "編號", FieldDbType.String);
            table.Fields!.Add("sys_name", "名稱", FieldDbType.String);

            var grid = schema.GetListLayout();

            Assert.Equal("Demo", grid.TableName);
            Assert.Equal("示範", grid.Caption);
            Assert.Equal(GridControlAllowActions.All, grid.AllowActions);
        }

        [Fact]
        [DisplayName("GetListLayout adds Columns in ListFields order")]
        public void GetListLayout_PreservesListFieldsOrder()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "sys_name,sys_id" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add("sys_id", "編號", FieldDbType.String);
            table.Fields!.Add("sys_name", "名稱", FieldDbType.String);

            var grid = schema.GetListLayout();

            Assert.Equal("sys_name", grid.Columns![0].FieldName);
            Assert.Equal("sys_id", grid.Columns![1].FieldName);
        }

        [Fact]
        [DisplayName("GetListLayout adds a hidden sys_rowid column with Visible=false when the master has sys_rowid")]
        public void GetListLayout_AddsHiddenRowIdColumn()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "sys_id" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row ID", FieldDbType.Guid) { Visible = false });
            table.Fields!.Add("sys_id", "編號", FieldDbType.String);

            var grid = schema.GetListLayout();

            var rowId = grid.Columns!.FirstOrDefault(c => c.FieldName == SysFields.RowId);
            Assert.NotNull(rowId);
            Assert.False(rowId!.Visible);
        }

        [Fact]
        [DisplayName("GetListLayout does not force in sys_rowid when the master does not have it")]
        public void GetListLayout_NoRowIdField_DoesNotAddIt()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "sys_id" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add("sys_id", "編號", FieldDbType.String);

            var grid = schema.GetListLayout();

            Assert.DoesNotContain(grid.Columns!, c => c.FieldName == SysFields.RowId);
        }

        [Fact]
        [DisplayName("GetListLayout does not add sys_master_rowid automatically (only FormLayout does)")]
        public void GetListLayout_DoesNotAddMasterRowId()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "sys_id" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid) { Visible = false });
            table.Fields!.Add("sys_id", "編號", FieldDbType.String);

            var grid = schema.GetListLayout();

            Assert.DoesNotContain(grid.Columns!, c => c.FieldName == SysFields.MasterRowId);
        }

        [Theory]
        [InlineData(ControlType.TextEdit)]
        [InlineData(ControlType.ButtonEdit)]
        [InlineData(ControlType.DateEdit)]
        [InlineData(ControlType.YearMonthEdit)]
        [InlineData(ControlType.DropDownEdit)]
        [InlineData(ControlType.CheckEdit)]
        [InlineData(ControlType.MemoEdit)]
        [InlineData(ControlType.DateTimeEdit)]
        [DisplayName("GetListLayout keeps a non-Auto ControlType as is")]
        public void GetListLayout_NonAutoControlType_PreservesValue(ControlType controlType)
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "col" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField("col", "欄", FieldDbType.String) { ControlType = controlType });

            var grid = schema.GetListLayout();

            var column = grid.Columns!.First(c => c.FieldName == "col");
            Assert.Equal(controlType, column.ControlType);
        }

        [Theory]
        [InlineData(FieldDbType.Boolean, ControlType.CheckEdit)]
        [InlineData(FieldDbType.DateTime, ControlType.DateTimeEdit)]
        [InlineData(FieldDbType.Text, ControlType.MemoEdit)]
        [InlineData(FieldDbType.String, ControlType.TextEdit)]
        [DisplayName("GetListLayout infers the control type from the DbType for ControlType=Auto")]
        public void GetListLayout_AutoControlType_MapsDbType(FieldDbType dbType, ControlType expected)
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "col" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField("col", "欄", dbType) { ControlType = ControlType.Auto });

            var grid = schema.GetListLayout();

            var column = grid.Columns!.First(c => c.FieldName == "col");
            Assert.Equal(expected, column.ControlType);
        }

        [Fact]
        [DisplayName("GetListLayout passes Width through to LayoutColumn")]
        public void GetListLayout_PassesWidth()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "col" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField("col", "欄", FieldDbType.String) { Width = 200 });

            var grid = schema.GetListLayout();

            Assert.Equal(200, grid.Columns!.First(c => c.FieldName == "col").Width);
        }

        [Fact]
        [DisplayName("GetListLayout passes DisplayFormat and NumberFormat through")]
        public void GetListLayout_PropagatesDisplayAndNumberFormats()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "amount" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField("amount", "金額", FieldDbType.Decimal)
            {
                DisplayFormat = "{0:C}",
                NumberFormat = "Amount"
            });

            var grid = schema.GetListLayout();

            var column = grid.Columns!.First(c => c.FieldName == "amount");
            Assert.Equal("{0:C}", column.DisplayFormat);
            Assert.Equal("Amount", column.NumberFormat);
        }

        [Fact]
        [DisplayName("GetListLayout silently skips fields in ListFields that do not exist (allowlist mode)")]
        public void GetListLayout_UnknownFieldInListFields_Skipped()
        {
            var schema = new FormSchema("Demo", "示範") { ListFields = "known,missing" };
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField("known", "已知", FieldDbType.String));

            var grid = schema.GetListLayout();

            Assert.Single(grid.Columns!);
            Assert.Equal("known", grid.Columns![0].FieldName);
        }

        [Fact]
        [DisplayName("GetListLayout returns a LayoutGrid with no columns when there is no MasterTable")]
        public void GetListLayout_NoMasterTable_ReturnsEmptyGrid()
        {
            // The ProgId does not match any table, so `MasterTable` is null.
            var schema = new FormSchema("NotExist", "不存在") { ListFields = "sys_id" };

            var grid = schema.GetListLayout();

            Assert.Equal("NotExist", grid.TableName);
            Assert.Empty(grid.Columns!);
        }
    }
}
