using System.ComponentModel;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    public class FieldMappingTests
    {
        [Fact]
        [DisplayName("ToString returns the \"{SourceField} -> {DestinationField}\" format")]
        public void ToString_ReturnsFormattedString()
        {
            var mapping = new FieldMapping("dept_name", "name");

            Assert.Equal("dept_name -> name", mapping.ToString());
        }
    }
}
