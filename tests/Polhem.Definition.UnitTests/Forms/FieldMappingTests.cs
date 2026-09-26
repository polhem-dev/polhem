using System.ComponentModel;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    public class FieldMappingTests
    {
        [Fact]
        [DisplayName("ToString 應回傳 \"{SourceField} -> {DestinationField}\" 格式")]
        public void ToString_ReturnsFormattedString()
        {
            var mapping = new FieldMapping("dept_name", "name");

            Assert.Equal("dept_name -> name", mapping.ToString());
        }
    }
}
