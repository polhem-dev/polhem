using System.ComponentModel;
using Polhem.Web.Blazor.Server.UnitTests.Controls.Editors;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// The lookup write-back of <see cref="Polhem.Web.Blazor.Server.DataObjects.FormDataObject"/>.
    /// </summary>
    public class FormDataObjectLookupTests
    {
        [Fact]
        [DisplayName("ApplyLookupSelection writes the row id and the mapped fields, and ClearLookupSelection empties them")]
        public void ApplyAndClear_RoundTrip()
        {
            var dataObject = LookupTestData.BuildEmployee();
            var field = dataObject.GetFormField(LookupTestData.LookupField)!;
            var selected = LookupTestData.BuildTargetRows().Rows[0];

            dataObject.ApplyLookupSelection(field, selected);
            Assert.Equal(LookupTestData.EngineeringId, (Guid)dataObject.MasterRow![LookupTestData.LookupField]);
            Assert.Equal("D01", dataObject.GetField("ref_dept_id"));
            Assert.Equal("Engineering", dataObject.GetField("ref_dept_name"));

            dataObject.ClearLookupSelection(field);
            Assert.Equal(string.Empty, dataObject.GetField("ref_dept_id"));
            Assert.Equal(string.Empty, dataObject.GetField("ref_dept_name"));
        }
    }
}
