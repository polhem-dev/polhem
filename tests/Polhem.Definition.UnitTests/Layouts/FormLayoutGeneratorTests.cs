using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Tests for FormLayoutGenerator.Generate converting a FormSchema into a FormLayout.
    /// </summary>
    public class FormLayoutGeneratorTests
    {
        [Fact]
        [DisplayName("FormLayoutGenerator.Generate produces a FormLayout with LayoutId / ProgId / Caption")]
        public void Generate_RequiresLayoutId_ProducesLayoutWithProgIdAndCaption()
        {
            var schema = BuildSchema();

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Equal("default", layout.LayoutId);
            Assert.Equal("Employee", layout.ProgId);
            Assert.Equal("員工", layout.Caption);
            Assert.Equal(2, layout.ColumnCount);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate names the master Section Main, with the master DisplayName as a caption that is not shown")]
        public void Generate_CreatesMainSection()
        {
            var schema = BuildSchema();

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Single(layout.Sections!);
            var section = layout.Sections![0];
            Assert.Equal("Main", section.Name);
            Assert.Equal("員工", section.Caption);
            // The master section caption repeats the form name, so it is not rendered
            // (the host frames the form via tab / title). See FormLayoutGenerator.AddSections.
            Assert.False(section.ShowCaption);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate ignores fields with Visible=false")]
        public void Generate_SkipsInvisibleFields()
        {
            var schema = BuildSchema();
            schema.MasterTable!.Fields!["sys_id"].Visible = false;

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var section = layout.Sections![0];
            Assert.DoesNotContain(section.Fields!, f => f.FieldName == "sys_id");
        }

        [Theory]
        [InlineData(FieldDbType.Boolean, ControlType.CheckEdit)]
        [InlineData(FieldDbType.DateTime, ControlType.DateTimeEdit)]
        [InlineData(FieldDbType.Text, ControlType.MemoEdit)]
        [InlineData(FieldDbType.String, ControlType.TextEdit)]
        [DisplayName("FormLayoutGenerator.Generate infers the control type from the DbType for ControlType=Auto")]
        public void Generate_AutoControlType_MapsDbTypeToControlType(FieldDbType dbType, ControlType expected)
        {
            var schema = new FormSchema("Demo", "示範");
            var table = schema.Tables!.Add("Demo", "示範");
            table.Fields!.Add(new FormField("field", "欄位", dbType) { ControlType = ControlType.Auto });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            var field = layout.Sections!.First().Fields!.First();
            Assert.Equal(expected, field.ControlType);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate creates a detail grid for every table other than the master")]
        public void Generate_MultipleTables_CreatesDetailGrid()
        {
            var schema = BuildSchema();
            var detail = schema.Tables!.Add("EmployeeSkill", "員工技能");
            detail.Fields!.Add("skill_name", "技能", FieldDbType.String);

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Single(layout.Details!);
            var grid = layout.Details![0];
            Assert.Equal("EmployeeSkill", grid.TableName);
            Assert.Equal("員工技能", grid.Caption);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate adds no Section when every master field is invisible")]
        public void Generate_MasterAllInvisible_DoesNotAddSection()
        {
            var schema = new FormSchema("Demo", "示範");
            var master = schema.Tables!.Add("Demo", "示範");
            master.Fields!.Add(new FormField("hidden", "隱藏", FieldDbType.String) { Visible = false });

            var layout = FormLayoutGenerator.Generate(schema, "default");

            Assert.Empty(layout.Sections!);
        }

        [Fact]
        [DisplayName("FormLayoutGenerator.Generate accepts a custom layoutId and writes it as is")]
        public void Generate_AcceptsCustomLayoutId()
        {
            var schema = BuildSchema();

            var layout = FormLayoutGenerator.Generate(schema, "manager_view");

            Assert.Equal("manager_view", layout.LayoutId);
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "員工");
            var master = schema.Tables!.Add("Employee", "員工");
            master.Fields!.Add("sys_id", "編號", FieldDbType.String);
            master.Fields!.Add("sys_name", "姓名", FieldDbType.String);
            return schema;
        }
    }
}
