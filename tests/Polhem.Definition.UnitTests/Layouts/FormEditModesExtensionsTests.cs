using System.ComponentModel;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for FormEditModesExtensions.
    /// </summary>
    public class FormEditModesExtensionsTests
    {
        [Theory]
        [InlineData(FormEditModes.All, SingleFormMode.Add, true)]
        [InlineData(FormEditModes.All, SingleFormMode.Edit, true)]
        [InlineData(FormEditModes.All, SingleFormMode.View, false)]
        [InlineData(FormEditModes.Add, SingleFormMode.Add, true)]
        [InlineData(FormEditModes.Add, SingleFormMode.Edit, false)]
        [InlineData(FormEditModes.Add, SingleFormMode.View, false)]
        [InlineData(FormEditModes.Edit, SingleFormMode.Add, false)]
        [InlineData(FormEditModes.Edit, SingleFormMode.Edit, true)]
        [InlineData(FormEditModes.Edit, SingleFormMode.View, false)]
        [InlineData(FormEditModes.None, SingleFormMode.Add, false)]
        [InlineData(FormEditModes.None, SingleFormMode.Edit, false)]
        [InlineData(FormEditModes.None, SingleFormMode.View, false)]
        [DisplayName("Allows returns whether editing is allowed from the flags and the form mode, and View is always false")]
        public void Allows_FlagAndMode_ReturnsExpected(FormEditModes modes, SingleFormMode formMode, bool expected)
        {
            Assert.Equal(expected, modes.Allows(formMode));
        }
    }
}
