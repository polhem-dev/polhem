using System.ComponentModel;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// A <see cref="ControlType"/> travels as its underlying integer, so a member that moves changes the
    /// meaning of every payload already written. New members are appended; the existing values never change.
    /// </summary>
    public class ControlTypeValueTests
    {
        [Theory]
        [InlineData(ControlType.Auto, 0)]
        [InlineData(ControlType.TextEdit, 1)]
        [InlineData(ControlType.ButtonEdit, 2)]
        [InlineData(ControlType.DateEdit, 3)]
        [InlineData(ControlType.YearMonthEdit, 4)]
        [InlineData(ControlType.DropDownEdit, 5)]
        [InlineData(ControlType.MemoEdit, 6)]
        [InlineData(ControlType.CheckEdit, 7)]
        [InlineData(ControlType.NumericEdit, 8)]
        [InlineData(ControlType.TimeEdit, 9)]
        [InlineData(ControlType.DateTimeEdit, 10)]
        [DisplayName("Each ControlType member keeps the underlying value existing payloads were written with")]
        public void UnderlyingValue_IsPinned(ControlType controlType, int expected)
        {
            Assert.Equal(expected, (int)controlType);
        }

        [Fact]
        [DisplayName("Every ControlType member has a pinned underlying value, so a new member cannot slip in unpinned")]
        public void AllMembers_ArePinned()
        {
            Assert.Equal(11, Enum.GetValues<ControlType>().Length);
        }
    }
}
