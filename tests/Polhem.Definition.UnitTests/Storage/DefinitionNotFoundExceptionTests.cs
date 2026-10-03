using System.ComponentModel;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    public class DefinitionNotFoundExceptionTests
    {
        [Fact]
        [DisplayName("The message names the definition type and key, and the path stays on FileName only")]
        public void Message_WithKey_NamesTypeAndKeyButNotPath()
        {
            string filePath = Path.Combine("srv", "secret", "Define", "FormSchema", "Employee.FormSchema.xml");

            var ex = new DefinitionNotFoundException(DefineType.FormSchema, "Employee", filePath);

            Assert.Equal("FormSchema 'Employee' not found.", ex.Message);
            Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
            Assert.Equal(filePath, ex.FileName);
        }

        [Fact]
        [DisplayName("The message of a definition held only once names the type alone")]
        public void Message_EmptyKey_NamesTypeOnly()
        {
            var ex = new DefinitionNotFoundException(DefineType.ProgramSettings, string.Empty);

            Assert.Equal("ProgramSettings not found.", ex.Message);
            Assert.Null(ex.FileName);
        }
    }
}
