using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Expressions;

namespace Polhem.Core.UnitTests.Expressions
{
    /// <summary>
    /// Tests for how <see cref="ExpressionPolicy.CoerceValue"/> moves calendar days and times of day between the
    /// expression engine's types and the storage types of a <see cref="System.Data.DataSet"/>.
    /// </summary>
    public class ExpressionPolicyTemporalTests
    {
        [Fact]
        [DisplayName("CoerceValue narrows a TimeOnly for a Time column to its fixed-width HH:mm text")]
        public void CoerceValue_TimeOnlyForTimeColumn_ReturnsHourMinuteText()
        {
            Assert.Equal("09:05", ExpressionPolicy.CoerceValue(new TimeOnly(9, 5, 30), FieldDbType.Time));
        }

        [Fact]
        [DisplayName("CoerceValue widens a DateOnly for a Date column to midnight of that day")]
        public void CoerceValue_DateOnlyForDateColumn_ReturnsMidnightDateTime()
        {
            Assert.Equal(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Unspecified),
                ExpressionPolicy.CoerceValue(new DateOnly(2026, 7, 28), FieldDbType.Date));
        }
    }
}
