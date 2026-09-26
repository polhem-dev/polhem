using System.ComponentModel;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// AmountColumnSummary: a transaction-currency column shows a total only when every row has the same currency and shows none for mixed currencies (returns null); a home-currency column (one currency throughout) can always be totaled.
    /// </summary>
    public class AmountColumnSummaryTests
    {
        [Fact]
        [DisplayName("TryComputeTotal returns the total when every row has the same currency")]
        public void TryComputeTotal_SameCurrency_ReturnsSum()
        {
            var total = AmountColumnSummary.TryComputeTotal(
            [
                (10.00m, "USD"), (20.50m, "USD"), (5.25m, "USD"),
            ]);

            Assert.Equal(35.75m, total);
        }

        [Fact]
        [DisplayName("TryComputeTotal returns null (no total shown) for mixed currencies (USD+JPY)")]
        public void TryComputeTotal_MixedCurrency_ReturnsNull()
        {
            var total = AmountColumnSummary.TryComputeTotal(
            [
                (10.00m, "USD"), (1000m, "JPY"),
            ]);

            Assert.Null(total);
        }

        [Fact]
        [DisplayName("TryComputeTotal always returns the total for a home-currency column (one currency throughout)")]
        public void TryComputeTotal_HomeCurrencyColumn_AlwaysTotals()
        {
            var total = AmountColumnSummary.TryComputeTotal(
            [
                (300m, "TWD"), (31000m, "TWD"), (1650m, "TWD"),
            ]);

            Assert.Equal(32950m, total);
        }

        [Fact]
        [DisplayName("TryComputeTotal compares currency codes case-insensitively")]
        public void TryComputeTotal_CaseInsensitiveCurrency()
        {
            var total = AmountColumnSummary.TryComputeTotal(
            [
                (10m, "usd"), (20m, "USD"),
            ]);

            Assert.Equal(30m, total);
        }

        [Fact]
        [DisplayName("TryComputeTotal returns a total of 0 for an empty collection")]
        public void TryComputeTotal_Empty_ReturnsZero()
        {
            var total = AmountColumnSummary.TryComputeTotal([]);

            Assert.Equal(0m, total);
        }
    }
}
