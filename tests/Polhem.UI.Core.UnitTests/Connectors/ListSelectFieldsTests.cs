using System.ComponentModel;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Core.Connectors;

namespace Polhem.UI.Core.UnitTests.Connectors
{
    /// <summary>
    /// <see cref="ListSelectFields"/> puts the row id ahead of the form's list fields.
    /// </summary>
    public class ListSelectFieldsTests
    {
        [Theory]
        [InlineData("sys_id,sys_name", "sys_rowid,sys_id,sys_name")]
        [InlineData(" sys_id , SYS_ROWID ,sys_id", "sys_rowid,sys_id")]
        [InlineData("", "sys_rowid")]
        [DisplayName("For puts sys_rowid first and drops blanks and duplicates from ListFields")]
        public void For_ListFields_PutsRowIdFirst(string listFields, string expected)
        {
            var schema = new FormSchema("P", "P") { ListFields = listFields };

            Assert.Equal(expected, ListSelectFields.For(schema));
            Assert.StartsWith(SysFields.RowId, expected, StringComparison.Ordinal);
        }
    }
}
