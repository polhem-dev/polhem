using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// The change events and row-scoped access of <see cref="FormDataObject"/>, which the field editors rely on.
    /// </summary>
    public class FormDataObjectEventTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            schema.Tables!.Add("Employee", "Employee").Fields!.Add("emp_name", "Name", FieldDbType.String);
            schema.Tables.Add("Skill", "Skill").Fields!.Add("skill_name", "Skill", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("SetField raises FieldValueChanged with the table, field and new value, and marks the object dirty")]
        public void SetField_RaisesFieldValueChanged()
        {
            var dataObject = BuildDataObject();
            FieldValueChangedEventArgs? raised = null;
            var dirtyChanges = 0;
            dataObject.FieldValueChanged += (_, e) => raised = e;
            dataObject.IsDirtyChanged += (_, _) => dirtyChanges++;

            dataObject.SetField("emp_name", "Ada");

            Assert.NotNull(raised);
            Assert.Equal("Employee", raised!.TableName);
            Assert.Equal("emp_name", raised.FieldName, ignoreCase: true);
            Assert.Equal("Ada", raised.Value);
            Assert.True(dataObject.IsDirty);
            Assert.Equal(1, dirtyChanges);
        }

        [Fact]
        [DisplayName("Writing the value already stored raises nothing and leaves the object clean")]
        public void SetField_SameValue_RaisesNothing()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField("emp_name", "Ada");
            dataObject.InitializeNewMaster();
            dataObject.MasterRow!["emp_name"] = "Ada";
            dataObject.MasterRow.AcceptChanges();
            var raised = 0;
            dataObject.FieldValueChanged += (_, _) => raised++;

            dataObject.SetField("emp_name", "Ada");

            Assert.Equal(0, raised);
        }

        [Fact]
        [DisplayName("InitializeNewMaster raises DataSetReplaced")]
        public void InitializeNewMaster_RaisesDataSetReplaced()
        {
            var dataObject = BuildDataObject();
            var raised = 0;
            dataObject.DataSetReplaced += (_, _) => raised++;

            dataObject.InitializeNewMaster();

            Assert.Equal(1, raised);
        }

        [Fact]
        [DisplayName("GetField and SetField with a row target that row")]
        public void RowOverloads_TargetTheRow()
        {
            var dataObject = BuildDataObject();
            var detail = dataObject.DataSet.Tables["Skill"]!;
            var row = detail.NewRow();
            detail.Rows.Add(row);

            dataObject.SetField(row, "skill_name", "Chess");

            Assert.Equal("Chess", dataObject.GetField(row, "skill_name"));
        }

        [Fact]
        [DisplayName("A row from another DataSet is rejected")]
        public void RowOverloads_ForeignRow_Throws()
        {
            var dataObject = BuildDataObject();
            var foreign = new DataTable("Skill");
            foreign.Columns.Add("skill_name", typeof(string));
            var row = foreign.NewRow();
            foreign.Rows.Add(row);

            Assert.Throws<ArgumentException>(() => dataObject.GetField(row, "skill_name"));
            Assert.Throws<ArgumentException>(() => dataObject.SetField(row, "skill_name", "x"));
        }

        [Fact]
        [DisplayName("GetFormField with a table name finds a detail table's field")]
        public void GetFormField_DetailTable_ReturnsField()
        {
            var dataObject = BuildDataObject();

            Assert.NotNull(dataObject.GetFormField("Skill", "skill_name"));
            Assert.Null(dataObject.GetFormField("Skill", "missing"));
            Assert.Null(dataObject.GetFormField("Missing", "skill_name"));
        }
    }
}
