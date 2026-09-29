using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// A <see cref="FieldDbType.Time"/> field must reach the UI as a time editor, otherwise the
    /// semantic marker buys nothing at the layer it exists to serve (ADR-033).
    /// </summary>
    public class TimeControlTypeTests
    {
        [Fact]
        [DisplayName("The Auto control type of a Time field resolves to TimeEdit")]
        public void ResolveControlType_TimeField_ResolvesToTimeEdit()
        {
            Assert.Equal(ControlType.TimeEdit,
                LayoutColumnFactory.ResolveControlType(ControlType.Auto, FieldDbType.Time));
        }

        [Fact]
        [DisplayName("An explicitly specified control type takes precedence over the Time default")]
        public void ResolveControlType_ExplicitType_Wins()
        {
            Assert.Equal(ControlType.TextEdit,
                LayoutColumnFactory.ResolveControlType(ControlType.TextEdit, FieldDbType.Time));
        }
    }
}
