using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// NumberFormatResolver with multiple currencies: decimals and rounding resolved from the currency in the CUKY column,
    /// two-stage rounding (round-then-sum on the lines plus final cash rounding), home currency amounts, and preserve.
    /// </summary>
    public class NumberFormatResolverCurrencyTests
    {
        private static CurrencySettings Currencies() =>
        [
            new CurrencyItem("USD", 0.01m, "$", "US Dollar"),
            new CurrencyItem("JPY", 1m, "¥", "Japanese Yen"),
            new CurrencyItem("BHD", 0.001m, "BD", "Bahraini Dinar"),
            new CurrencyItem("CHF", 0.01m, "CHF", "Swiss Franc"),
        ];

        private static RoundingContext Ctx(CompanyInfo? company = null) =>
            new() { Company = company, CurrencySettings = Currencies() };

        [Theory]
        [InlineData("USD", 2)]
        [InlineData("JPY", 0)]
        [InlineData("BHD", 3)]
        [DisplayName("ResolveDecimals gives an amount column different decimals by the currency in the CUKY column")]
        public void ResolveDecimals_Amount_ByCurrency(string code, int expected)
        {
            Assert.Equal(expected, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, Ctx(), code));
        }

        [Theory]
        [InlineData("USD", "N2")]
        [InlineData("JPY", "N0")]
        [InlineData("BHD", "N3")]
        [DisplayName("ResolveFormat gives an amount column a different format string by currency")]
        public void ResolveFormat_Amount_ByCurrency(string code, string expected)
        {
            Assert.Equal(expected, NumberFormatResolver.ResolveFormat(NumberKind.Amount, Ctx(), code));
        }

        [Fact]
        [DisplayName("ResolveDecimals with an empty refCode falls back to the company's default currency, then to the framework's 2")]
        public void ResolveDecimals_EmptyRefCode_FallsBackToDefaultCurrencyThenFramework()
        {
            var company = new CompanyInfo { CompanyId = "C001", DefaultCurrency = "JPY" };

            // An empty refCode falls back to the company's default currency, JPY (0 decimals).
            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, Ctx(company), null));

            // Without a company default currency or a refCode, the framework's 2 applies.
            Assert.Equal(2, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, Ctx(), null));
        }

        [Fact]
        [DisplayName("ResolveDecimals falls back to the framework's 2 for amounts without a currency master")]
        public void ResolveDecimals_NoCurrencyMaster_FrameworkDefault()
        {
            var ctx = new RoundingContext { Company = null, CurrencySettings = null };

            Assert.Equal(2, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, ctx, "USD"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("ResolveDecimals throws InvalidOperationException for a company with a blank default currency and an empty refCode")]
        public void ResolveDecimals_CompanyDefaultCurrencyBlank_Throws(string defaultCurrency)
        {
            var ctx = Ctx(new CompanyInfo { CompanyId = "C001", DefaultCurrency = defaultCurrency });

            Assert.Throws<InvalidOperationException>(
                () => NumberFormatResolver.ResolveDecimals(NumberKind.Amount, ctx, null));
        }

        [Fact]
        [DisplayName("ResolveDecimals throws for a company with a blank default currency even without a currency master")]
        public void ResolveDecimals_CompanyDefaultCurrencyBlank_NoCurrencyMaster_Throws()
        {
            var ctx = new RoundingContext { Company = new CompanyInfo { CompanyId = "C001" }, CurrencySettings = null };

            Assert.Throws<InvalidOperationException>(
                () => NumberFormatResolver.ResolveDecimals(NumberKind.Amount, ctx, null));
        }

        [Fact]
        [DisplayName("ResolveDecimals resolves by refCode without throwing when the company's default currency is blank but a refCode is given")]
        public void ResolveDecimals_CompanyDefaultCurrencyBlank_WithRefCode_ResolvesByRefCode()
        {
            var ctx = Ctx(new CompanyInfo { CompanyId = "C001" });

            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, ctx, "JPY"));
        }

        [Fact]
        [DisplayName("ResolveDecimals for non-amount kinds is unaffected by a blank company default currency")]
        public void ResolveDecimals_CompanyDefaultCurrencyBlank_NonAmountKind_UsesCompanyDecimals()
        {
            var ctx = Ctx(new CompanyInfo { CompanyId = "C001" });

            Assert.Equal(2, NumberFormatResolver.ResolveDecimals(NumberKind.Percent, ctx, null));
        }

        [Fact]
        [DisplayName("RoundByKind company overload throws InvalidOperationException for an amount when the company's default currency is blank")]
        public void RoundByKind_CompanyOverload_CompanyDefaultCurrencyBlank_Throws()
        {
            var company = new CompanyInfo { CompanyId = "C001" };

            Assert.Throws<InvalidOperationException>(
                () => NumberFormatResolver.RoundByKind(12.345m, NumberKind.Amount, company));
        }

        [Fact]
        [DisplayName("RoundByKind rounds amounts by currency (USD 2 decimals, JPY 0)")]
        public void RoundByKind_Amount_ByCurrency()
        {
            Assert.Equal(12.35m, NumberFormatResolver.RoundByKind(12.345m, NumberKind.Amount, Ctx(), "USD"));
            Assert.Equal(12m, NumberFormatResolver.RoundByKind(12.4m, NumberKind.Amount, Ctx(), "JPY"));
            Assert.Equal(13m, NumberFormatResolver.RoundByKind(12.5m, NumberKind.Amount, Ctx(), "JPY"));
        }

        [Fact]
        [DisplayName("Round-then-sum invariant: the rounded lines sum to the header total, which differs from rounding at full precision (USD)")]
        public void RoundThenSum_Usd_InvariantHolds()
        {
            decimal[] details = [10.333m, 10.333m, 10.333m];
            var ctx = Ctx();

            decimal total = 0m;
            foreach (var d in details)
                total += NumberFormatResolver.RoundByKind(d, NumberKind.Amount, ctx, "USD");

            decimal sumThenRound = NumberFormatResolver.RoundByKind(
                details[0] + details[1] + details[2], NumberKind.Amount, ctx, "USD");

            Assert.Equal(30.99m, total);        // 10.33 × 3
            Assert.Equal(31.00m, sumThenRound); // 30.999 → 31.00
            Assert.NotEqual(total, sumThenRound);
        }

        [Fact]
        [DisplayName("Round-then-sum with JPY lines at 0 decimals, a different scale from USD")]
        public void RoundThenSum_Jpy_ZeroDecimals()
        {
            decimal[] details = [100.4m, 100.4m, 100.4m];
            var ctx = Ctx();

            decimal total = 0m;
            foreach (var d in details)
                total += NumberFormatResolver.RoundByKind(d, NumberKind.Amount, ctx, "JPY");

            Assert.Equal(300m, total); // Each line rounds to 100, so the total is 300.
        }

        [Fact]
        [DisplayName("RoundCash with a company setting of 0.05 for CHF rounds to multiples of 5 cents, and diff = payable - total")]
        public void RoundCash_CompanyOverride_RoundsToUnit_WithDiff()
        {
            var company = new CompanyInfo
            {
                CompanyId = "C001",
                CashRounding = [new CashRoundingItem("CHF", 0.05m)],
            };
            var ctx = Ctx(company);

            decimal total = 12.34m;
            decimal payable = NumberFormatResolver.RoundCash(total, "CHF", ctx);
            Assert.Equal(12.35m, payable);
            Assert.Equal(0.01m, payable - total);

            decimal total2 = 12.32m;
            decimal payable2 = NumberFormatResolver.RoundCash(total2, "CHF", ctx);
            Assert.Equal(12.30m, payable2);
            Assert.Equal(-0.02m, payable2 - total2);
        }

        [Fact]
        [DisplayName("RoundCash without a company override falls back to the currency's natural unit (USD 0.01, no extra rounding)")]
        public void RoundCash_NoOverride_NoExtraRounding()
        {
            var ctx = Ctx(new CompanyInfo { CompanyId = "C001" });

            // The natural unit of USD is 0.01 and the total already has 2 decimals, so payable equals total and the diff is 0.
            Assert.Equal(12.34m, NumberFormatResolver.RoundCash(12.34m, "USD", ctx));
        }

        [Fact]
        [DisplayName("Home currency amount: home_amount = round(amount x rate, Amount, home currency), with home currency JPY at 0 decimals")]
        public void HomeAmount_ConvertedAndRoundedToHomeCurrency()
        {
            var company = new CompanyInfo { CompanyId = "C001", DefaultCurrency = "JPY" };
            var ctx = Ctx(company);

            // A USD amount of 100.00 at a rate of 150.5 (preserved at full precision) converts to JPY at 0 decimals.
            decimal amount = 100.00m;
            decimal rate = 150.5m;
            decimal home = NumberFormatResolver.RoundByKind(amount * rate, NumberKind.Amount, ctx, "JPY");

            Assert.Equal(15050m, home); // 100 x 150.5 = 15050, and JPY has 0 decimals.
        }

        [Fact]
        [DisplayName("Original and home amounts in different currencies on one row each resolve from their own CUKY column")]
        public void SameRow_OriginalAndHome_DifferentCurrencies()
        {
            var ctx = Ctx();

            // The original currency USD has 2 decimals and the home currency JPY has 0.
            Assert.Equal(2, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, ctx, "USD"));
            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Amount, ctx, "JPY"));
        }

        [Fact]
        [DisplayName("Preserve: unit prices and exchange rates are not rounded by currency and keep full precision")]
        public void Preserve_UnitPriceAndRate_NotRounded()
        {
            var ctx = Ctx();
            var value = 12.3456789m;

            Assert.Equal(value, NumberFormatResolver.RoundByKind(value, NumberKind.UnitPrice, ctx, "JPY"));
            Assert.Equal(value, NumberFormatResolver.RoundByKind(value, NumberKind.ExchangeRate, ctx, "JPY"));
        }
    }
}
