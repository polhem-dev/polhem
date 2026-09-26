using System.ComponentModel;
using Polhem.LoadTests.Statistics;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="LatencyStatistics"/>.
    /// </summary>
    public class LatencyStatisticsTests
    {
        [Fact]
        [DisplayName("Percentile uses nearest-rank, so the p50 of ten samples is the 5th sample")]
        public void Percentile_TenSamples_UsesNearestRank()
        {
            var stats = LatencyStatistics.FromMilliseconds(
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

            // ceil(50 / 100 * 10) = 5, so the p50 is the 5th value in sorted order.
            Assert.Equal(5, stats.Percentile(50));
        }

        [Fact]
        [DisplayName("Percentile returns an observed value without interpolation")]
        public void Percentile_P99OfHundred_ReturnsObservedValue()
        {
            var samples = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();
            var stats = LatencyStatistics.FromMilliseconds(samples);

            Assert.Equal(99, stats.Percentile(99));
            Assert.Equal(100, stats.Percentile(100));
        }

        [Fact]
        [DisplayName("FromMilliseconds computes correct percentiles from unsorted input")]
        public void FromMilliseconds_UnsortedInput_SortsBeforeComputing()
        {
            var stats = LatencyStatistics.FromMilliseconds([9, 1, 7, 3, 5]);

            Assert.Equal(1, stats.Min);
            Assert.Equal(9, stats.Max);
            Assert.Equal(5, stats.Percentile(50));
        }

        [Fact]
        [DisplayName("FromMilliseconds does not modify the caller's array")]
        public void FromMilliseconds_DoesNotMutateCallerArray()
        {
            double[] source = [3, 1, 2];

            LatencyStatistics.FromMilliseconds(source);

            Assert.Equal([3, 1, 2], source);
        }

        [Fact]
        [DisplayName("LatencyStatistics with no samples returns zero instead of throwing")]
        public void Percentile_NoSamples_ReturnsZero()
        {
            var stats = LatencyStatistics.FromMilliseconds([]);

            Assert.Equal(0, stats.Count);
            Assert.Equal(0, stats.Percentile(95));
            Assert.Equal(0, stats.Min);
            Assert.Equal(0, stats.Max);
            Assert.Equal(0, stats.Mean);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(101)]
        [DisplayName("Percentile throws ArgumentOutOfRangeException for 0 or less and for more than 100")]
        public void Percentile_OutOfRange_Throws(double percentile)
        {
            var stats = LatencyStatistics.FromMilliseconds([1, 2, 3]);

            Assert.Throws<ArgumentOutOfRangeException>(() => stats.Percentile(percentile));
        }

        [Fact]
        [DisplayName("Mean returns the arithmetic mean of all samples")]
        public void Mean_ReturnsArithmeticMean()
        {
            var stats = LatencyStatistics.FromMilliseconds([2, 4, 6, 8]);

            Assert.Equal(5, stats.Mean);
        }
    }
}
