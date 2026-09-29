using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Tests for <see cref="FormLayoutCaptionApplier"/>: a layout definition file only defines structure, and display text always comes from
    /// the localized <see cref="FormSchema"/>. Fields the schema does not have keep the value from the layout file.
    /// </summary>
    public class FormLayoutCaptionApplierTests
    {
        [Fact]
        [DisplayName("Field captions come from the schema (overriding the static text of the layout file)")]
        public void Apply_FieldCaption_TakenFromSchema()
        {
            var layout = BuildLayout();
            var schema = BuildSchema();

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("員工編號", Field(layout, "sys_id").Caption);
            Assert.Equal("姓名", Field(layout, "sys_name").Caption);
        }

        [Fact]
        [DisplayName("Form, section and detail captions come from the schema")]
        public void Apply_ContainerCaptions_TakenFromSchema()
        {
            var layout = BuildLayout();
            var schema = BuildSchema();

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("員工", layout.Caption);
            Assert.Equal("員工主檔", layout.Sections![0].Caption);
            Assert.Equal("工作紀錄", layout.Details![0].Caption);
        }

        [Fact]
        [DisplayName("Detail column captions come from the fields of the matching FormTable")]
        public void Apply_DetailColumnCaption_TakenFromDetailTable()
        {
            var layout = BuildLayout();
            var schema = BuildSchema();

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("工作日期", Column(layout.Details![0], "work_date").Caption);
        }

        [Fact]
        [DisplayName("A field the schema does not have keeps the layout file value (the layout may lag behind the schema)")]
        public void Apply_FieldMissingFromSchema_KeepsLayoutText()
        {
            var layout = BuildLayout();
            layout.Sections![0].Fields!.Add(new LayoutField { FieldName = "retired_field", Caption = "已移除欄位" });
            var schema = BuildSchema();

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("已移除欄位", Field(layout, "retired_field").Caption);
        }

        [Fact]
        [DisplayName("A detail table the schema does not have keeps all of its original values")]
        public void Apply_DetailTableMissingFromSchema_KeepsLayoutText()
        {
            var layout = BuildLayout();
            var orphan = new LayoutGrid("Removed", "已移除明細");
            orphan.Columns!.Add(new LayoutColumn("x", "欄 X", ControlType.TextEdit));
            layout.Details!.Add(orphan);
            var schema = BuildSchema();

            FormLayoutCaptionApplier.Apply(layout, schema);

            Assert.Equal("已移除明細", orphan.Caption);
            Assert.Equal("欄 X", Column(orphan, "x").Caption);
        }

        [Fact]
        [DisplayName("A null layout or a null schema throws ArgumentNullException")]
        public void Apply_NullArguments_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FormLayoutCaptionApplier.Apply(null!, BuildSchema()));
            Assert.Throws<ArgumentNullException>(() => FormLayoutCaptionApplier.Apply(BuildLayout(), null!));
        }

        [Fact]
        [DisplayName("A layout with empty Sections / Details does not throw")]
        public void Apply_EmptyLayout_DoesNotThrow()
        {
            var layout = new FormLayout { LayoutId = "Employee", ProgId = "Employee" };

            var exception = Record.Exception(() => FormLayoutCaptionApplier.Apply(layout, BuildSchema()));

            Assert.Null(exception);
            Assert.Equal("員工", layout.Caption);
        }

        [Fact]
        [DisplayName("A layout with several sections keeps its authored section captions and still takes the field captions")]
        public void Apply_SeveralSections_KeepsSectionCaptions()
        {
            var layout = BuildLayout();
            var address = new LayoutSection { Name = "Address", Caption = "Address (layout file)" };
            address.Fields!.Add(new LayoutField { FieldName = "sys_name", Caption = "Name (layout file)" });
            layout.Sections!.Add(address);

            FormLayoutCaptionApplier.Apply(layout, BuildSchema());

            Assert.Equal("Main (layout file)", layout.Sections![0].Caption);
            Assert.Equal("Address (layout file)", layout.Sections![1].Caption);
            Assert.Equal("姓名", layout.Sections![1].Fields!.First(f => f.FieldName == "sys_name").Caption);
        }

        private static LayoutField Field(FormLayout layout, string fieldName)
            => layout.Sections![0].Fields!.First(f => f.FieldName == fieldName);

        private static LayoutColumn Column(LayoutGrid grid, string fieldName)
            => grid.Columns!.First(c => c.FieldName == fieldName);

        // The layout file carries the text its author hard-coded, and all of it must be overridden by the schema.
        private static FormLayout BuildLayout()
        {
            var layout = new FormLayout
            {
                LayoutId = "Employee",
                ProgId = "Employee",
                Caption = "Employee (layout file)",
            };
            var section = new LayoutSection { Name = "Main", Caption = "Main (layout file)" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "ID (layout file)" });
            section.Fields!.Add(new LayoutField { FieldName = "sys_name", Caption = "Name (layout file)" });
            layout.Sections!.Add(section);

            var grid = new LayoutGrid("WorkLog", "WorkLog (layout file)");
            grid.Columns!.Add(new LayoutColumn("work_date", "Date (layout file)", ControlType.DateEdit));
            layout.Details!.Add(grid);
            return layout;
        }

        // Simulates an already localized schema, whose text is the final Chinese to display.
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "員工") { CategoryId = "common" };
            var master = schema.Tables!.Add("Employee", "員工主檔");
            master.DbTableName = "st_employee";
            master.Fields!.Add("sys_id", "員工編號", FieldDbType.String);
            master.Fields!.Add("sys_name", "姓名", FieldDbType.String);

            var detail = schema.Tables!.Add("WorkLog", "工作紀錄");
            detail.DbTableName = "ft_work_log";
            detail.Fields!.Add("work_date", "工作日期", FieldDbType.Date);
            return schema;
        }
    }
}
