using System.ComponentModel;
using Polhem.Definition.Identity;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// NumberFormatResolver: company-aware resolution of decimals and formats, and RoundByKind (including the round-then-sum rule).
    /// </summary>
    public class NumberFormatResolverTests
    {
        private static CompanyInfo Company(params NumberFormatItem[] overrides)
        {
            var company = new CompanyInfo { CompanyId = "C001" };
            foreach (var item in overrides)
                company.NumberFormats.Add(item);
            return company;
        }

        [Fact]
        [DisplayName("ResolveDecimals prefers the company override over the framework default")]
        public void ResolveDecimals_CompanyOverride_Wins()
        {
            var company = Company(new NumberFormatItem(NumberKind.Percent, 4));

            Assert.Equal(4, NumberFormatResolver.ResolveDecimals(NumberKind.Percent, company));
        }

        [Fact]
        [DisplayName("ResolveDecimals falls back to the framework default when company is null")]
        public void ResolveDecimals_NullCompany_FrameworkDefault()
        {
            Assert.Equal(2, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, null));
            Assert.Equal(4, NumberFormatResolver.ResolveDecimals(NumberKind.UnitPrice, null));
        }

        [Fact]
        [DisplayName("ResolveDecimals for SystemFixed (exchange rate) ignores the company override and uses the framework default")]
        public void ResolveDecimals_SystemFixed_IgnoresCompanyOverride()
        {
            // Even when the company overrides ExchangeRate, SystemFixed does not use it.
            var company = Company(new NumberFormatItem(NumberKind.ExchangeRate, 9));

            Assert.Equal(5, NumberFormatResolver.ResolveDecimals(NumberKind.ExchangeRate, company));
        }

        [Theory]
        [InlineData(NumberKind.Amount, "N2")]
        [InlineData(NumberKind.Percent, "P2")]
        [InlineData(NumberKind.ExchangeRate, "N5")]
        [DisplayName("ResolveFormat returns the framework default format string when company is null")]
        public void ResolveFormat_NullCompany_FrameworkFormat(NumberKind kind, string expected)
        {
            Assert.Equal(expected, NumberFormatResolver.ResolveFormat(kind, null));
        }

        [Fact]
        [DisplayName("ResolveFormat reflects the company override in the format string")]
        public void ResolveFormat_CompanyOverride_Reflected()
        {
            var company = Company(new NumberFormatItem(NumberKind.UnitPrice, 6));

            Assert.Equal("N6", NumberFormatResolver.ResolveFormat(NumberKind.UnitPrice, company));
        }

        [Theory]
        [InlineData(NumberKind.UnitPrice)]
        [InlineData(NumberKind.Cost)]
        [InlineData(NumberKind.ExchangeRate)]
        [DisplayName("RoundByKind returns the value unchanged for Preserve kinds (no rounding)")]
        public void RoundByKind_Preserve_ReturnsOriginal(NumberKind kind)
        {
            var value = 12.3456789m;

            Assert.Equal(value, NumberFormatResolver.RoundByKind(value, kind, null));
        }

        [Fact]
        [DisplayName("RoundByKind rounds Round kinds AwayFromZero to the resolved decimals")]
        public void RoundByKind_Round_AwayFromZero()
        {
            // Amount defaults to 2 decimals, so 12.345 becomes 12.35 (away from zero, not banker's rounding).
            Assert.Equal(12.35m, NumberFormatResolver.RoundByKind(12.345m, NumberKind.Amount, null));
            // Negative values also round away from zero.
            Assert.Equal(-12.35m, NumberFormatResolver.RoundByKind(-12.345m, NumberKind.Amount, null));
        }

        [Fact]
        [DisplayName("RoundByKind rounds to the company's decimals (the company sets Percent to 0)")]
        public void RoundByKind_UsesCompanyDecimals()
        {
            var company = Company(new NumberFormatItem(NumberKind.Percent, 0));

            Assert.Equal(13m, NumberFormatResolver.RoundByKind(12.5m, NumberKind.Percent, company));
        }

        [Fact]
        [DisplayName("Round-then-sum rule: rounding each line and then summing differs from rounding the full-precision sum")]
        public void RoundThenSum_DiffersFromSumThenRound()
        {
            decimal[] details = [10.333m, 10.333m, 10.333m];

            // Round-then-sum: each line is first rounded to the Amount scale (2), then summed.
            decimal roundedSum = 0m;
            foreach (var d in details)
                roundedSum += NumberFormatResolver.RoundByKind(d, NumberKind.Amount, null);

            // Sum-then-round: summing at full precision and rounding afterwards (the forbidden approach).
            decimal sumThenRound = NumberFormatResolver.RoundByKind(
                details[0] + details[1] + details[2], NumberKind.Amount, null);

            Assert.Equal(30.99m, roundedSum);      // 10.33 × 3
            Assert.Equal(31.00m, sumThenRound);    // 30.999 → 31.00
            Assert.NotEqual(roundedSum, sumThenRound);
        }
    }
}
