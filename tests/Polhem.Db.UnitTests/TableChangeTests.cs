using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableChangeTests : IClassFixture<SharedDbFixture>
    {
        public TableChangeTests(SharedDbFixture _) { }

        [Fact]
        [DisplayName("AddFieldChange keeps the Field reference and is a TableChange subclass")]
        public void AddFieldChange_PreservesField()
        {
            var field = new DbField("age", "Age", FieldDbType.Integer);
            var change = new AddFieldChange(field);

            Assert.Same(field, change.Field);
            Assert.IsType<ITableChange>(change, exactMatch: false);
        }

        [Fact]
        [DisplayName("AlterFieldChange keeps the OldField and NewField references")]
        public void AlterFieldChange_PreservesOldAndNewFields()
        {
            var oldField = new DbField("name", "Name", FieldDbType.String) { Length = 30 };
            var newField = new DbField("name", "Name", FieldDbType.String) { Length = 50 };
            var change = new AlterFieldChange(oldField, newField);

            Assert.Same(oldField, change.OldField);
            Assert.Same(newField, change.NewField);
            Assert.IsType<ITableChange>(change, exactMatch: false);
        }

        [Fact]
        [DisplayName("AddIndexChange keeps the Index reference")]
        public void AddIndexChange_PreservesIndex()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");
            var change = new AddIndexChange(index);

            Assert.Same(index, change.Index);
            Assert.IsType<ITableChange>(change, exactMatch: false);
        }

        [Fact]
        [DisplayName("DropIndexChange keeps the Index reference")]
        public void DropIndexChange_PreservesIndex()
        {
            var index = new DbTableIndex { Name = "ix_demo_name" };
            index.IndexFields!.Add("name");
            var change = new DropIndexChange(index);

            Assert.Same(index, change.Index);
            Assert.IsType<ITableChange>(change, exactMatch: false);
        }

        [Fact]
        [DisplayName("DropIndexChange.Describe returns a description containing the index name")]
        public void DropIndexChange_Describe_ReturnsIndexName()
        {
            var index = new DbTableIndex { Name = "ix_user_email" };
            var change = new DropIndexChange(index);

            Assert.Equal("DropIndexChange on 'ix_user_email'", change.Describe());
        }

        [Fact]
        [DisplayName("RenameFieldChange keeps OldFieldName and NewField")]
        public void RenameFieldChange_PreservesOldNameAndNewField()
        {
            var newField = new DbField("employee_name", "Employee Name", FieldDbType.String) { Length = 50 };
            var change = new RenameFieldChange("emp_name", newField);

            Assert.Equal("emp_name", change.OldFieldName);
            Assert.Same(newField, change.NewField);
            Assert.IsType<ITableChange>(change, exactMatch: false);
        }

        [Fact]
        [DisplayName("AddFieldChange.Describe returns a description containing the field name")]
        public void AddFieldChange_Describe_ReturnsCorrectString()
        {
            var field = new DbField("email", "Email", FieldDbType.String);
            var change = new AddFieldChange(field);

            Assert.Equal("AddFieldChange on 'email'", change.Describe());
        }

        [Fact]
        [DisplayName("AddIndexChange.Describe returns a description containing the index name")]
        public void AddIndexChange_Describe_ReturnsCorrectString()
        {
            var index = new DbTableIndex { Name = "ix_test_email" };
            var change = new AddIndexChange(index);

            Assert.Equal("AddIndexChange on 'ix_test_email'", change.Describe());
        }

        [Fact]
        [DisplayName("RenameFieldChange.Describe returns a description containing the old and the new field names")]
        public void RenameFieldChange_Describe_ReturnsCorrectString()
        {
            var newField = new DbField("employee_name", "Employee Name", FieldDbType.String);
            var change = new RenameFieldChange("emp_name", newField);

            Assert.Equal("RenameFieldChange 'emp_name' -> 'employee_name'", change.Describe());
        }
    }
}
