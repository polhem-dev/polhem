using System.ComponentModel;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Tests for <see cref="FrameworkClock"/>: time zone conversion, the UTC default for a blank zone, and the failure behavior for an unresolvable zone.
    /// </summary>
    /// <remarks>
    /// Expected values are always derived from <see cref="TimeZoneInfo"/> rather than hard-coded offsets, because the
    /// tests must hold both on a developer machine (Asia/Taipei) and in CI (UTC).
    /// </remarks>
    public class FrameworkClockTests
    {
        private const string Taipei = "Asia/Taipei";

        [Fact]
        [DisplayName("Today returns the local calendar date of the given zone")]
        public void Today_UsesGivenZone()
        {
            var expected = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Taipei)));

            Assert.Equal(expected, FrameworkClock.Today(Taipei));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("A blank zone means UTC, not the machine zone")]
        public void Today_BlankZone_MeansUtc(string timeZoneId)
        {
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), FrameworkClock.Today(timeZoneId));
        }

        [Fact]
        [DisplayName("Now always has Kind Unspecified, never Local")]
        public void Now_KindIsAlwaysUnspecified()
        {
            // `Local` would mislabel a wall-clock time from a zone other than the machine's, and it shifts the reading on both wires (ADR-032 D6).
            Assert.Equal(DateTimeKind.Unspecified, FrameworkClock.Now(Taipei).Kind);
            Assert.Equal(DateTimeKind.Unspecified, FrameworkClock.Now(string.Empty).Kind);
        }

        [Fact]
        [DisplayName("Now and Today agree for the same zone")]
        public void Now_AndToday_AgreeOnTheSameZone()
        {
            Assert.Equal(DateOnly.FromDateTime(FrameworkClock.Now(Taipei)), FrameworkClock.Today(Taipei));
        }

        [Fact]
        [DisplayName("The zone offset applied by Now matches TimeZoneInfo")]
        public void Now_AppliesTheZoneOffset()
        {
            var offset = TimeZoneInfo.FindSystemTimeZoneById(Taipei).GetUtcOffset(DateTime.UtcNow);

            var delta = FrameworkClock.Now(Taipei) - FrameworkClock.Now(string.Empty);

            Assert.True(Math.Abs((delta - offset).TotalSeconds) < 5,
                $"Expected offset {offset}, got {delta}.");
        }

        [Fact]
        [DisplayName("An unresolvable zone throws instead of silently falling back to UTC")]
        public void Today_UnresolvableZone_Throws()
        {
            // A silent fallback to UTC would make every date wrong without a trace, which is exactly how missing tz data on mobile or WASM shows up.
            var exception = Assert.Throws<InvalidOperationException>(
                () => FrameworkClock.Today("Not/AZone"));

            Assert.Contains("Not/AZone", exception.Message, StringComparison.Ordinal);
        }
    }
}
