using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    public class RelationFieldReferenceTests
    {
        [Fact]
        [DisplayName("Default constructor creates an instance with empty FieldName, SourceProgId and SourceField")]
        public void DefaultConstructor_CreatesInstance_WithEmptyFields()
        {
            var reference = new RelationFieldReference();

            Assert.Equal(string.Empty, reference.FieldName);
            Assert.Equal(string.Empty, reference.SourceProgId);
            Assert.Equal(string.Empty, reference.SourceField);
        }

        [Fact]
        [DisplayName("ToString returns the \"{SourceProgId}.{SourceField} -> {FieldName}\" format")]
        public void ToString_ReturnsFormattedString()
        {
            var field = new FormField("dept_id", "部門 ID", FieldDbType.String);
            var reference = new RelationFieldReference("dept_id", field, "Department", "dept_name");

            Assert.Equal("Department.dept_name -> dept_id", reference.ToString());
        }
    }
}
