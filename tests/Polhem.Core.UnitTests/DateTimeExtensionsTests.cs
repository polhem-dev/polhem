using System.ComponentModel;

namespace Polhem.Core.UnitTests
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        [DisplayName("GetYearMonth returns the first day of the month with a time of 00:00:00")]
        public void GetYearMonth_ReturnsFirstOfMonth()
        {
            var input = new DateTime(2026, 4, 17, 9, 30, 15, DateTimeKind.Unspecified);
            var result = input.GetYearMonth();

            Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), result);
        }
    }
}
