using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Identity
{
    /// <summary>
    /// CompanyInfo decimal place resolution (company override vs framework default) and CompanyNumberFormats XML/JSON round-trips.
    /// </summary>
    public class CompanyInfoTests
    {
        [Fact]
        [DisplayName("GetDecimals returns the override when the company has one")]
        public void GetDecimals_Override_ReturnsOverride()
        {
            var company = new CompanyInfo
            {
                CompanyId = "C001",
                NumberFormats = [new NumberFormatItem(NumberKind.Percent, 4)],
            };

            Assert.Equal(4, company.GetDecimals(NumberKind.Percent));
        }

        [Fact]
        [DisplayName("GetDecimals falls back to the framework default decimal places when the company has no override")]
        public void GetDecimals_NoOverride_ReturnsFrameworkDefault()
        {
            var company = new CompanyInfo { CompanyId = "C001" };

            Assert.Equal(2, company.GetDecimals(NumberKind.Amount));
            Assert.Equal(4, company.GetDecimals(NumberKind.UnitPrice));
            Assert.Equal(3, company.GetDecimals(NumberKind.Weight));
        }

        [Fact]
        [DisplayName("GetDecimals with a partial override overrides only the specified kinds and falls back to the framework default for the rest")]
        public void GetDecimals_PartialOverride_OthersFallBack()
        {
            var company = new CompanyInfo
            {
                CompanyId = "C001",
                NumberFormats = [new NumberFormatItem(NumberKind.UnitPrice, 6)],
            };

            Assert.Equal(6, company.GetDecimals(NumberKind.UnitPrice));   // Overridden.
            Assert.Equal(2, company.GetDecimals(NumberKind.Percent));     // Framework default.
        }

        [Fact]
        [DisplayName("FindDecimals returns the decimal places on a hit and null on a miss")]
        public void FindDecimals_HitAndMiss()
        {
            CompanyNumberFormats formats = [new NumberFormatItem(NumberKind.Cost, 5)];

            Assert.Equal(5, formats.FindDecimals(NumberKind.Cost));
            Assert.Null(formats.FindDecimals(NumberKind.Percent));
        }

        [Fact]
        [DisplayName("CompanyNumberFormats XML serialization restores the override items")]
        public void CompanyNumberFormats_XmlRoundtrip_PreservesItems()
        {
            CompanyNumberFormats original =
            [
                new NumberFormatItem(NumberKind.Percent, 3),
                new NumberFormatItem(NumberKind.UnitPrice, 6),
            ];

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<CompanyNumberFormats>(xml);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Count);
            Assert.Equal(3, restored.FindDecimals(NumberKind.Percent));
            Assert.Equal(6, restored.FindDecimals(NumberKind.UnitPrice));
        }

        [Fact]
        [DisplayName("CompanyNumberFormats JSON serialization restores the override items")]
        public void CompanyNumberFormats_JsonRoundtrip_PreservesItems()
        {
            CompanyNumberFormats original = [new NumberFormatItem(NumberKind.Amount, 0)];

            var json = JsonCodec.Serialize(original);
            var restored = JsonCodec.Deserialize<CompanyNumberFormats>(json);

            Assert.NotNull(restored);
            Assert.Single(restored!);
            Assert.Equal(0, restored.FindDecimals(NumberKind.Amount));
        }

        // --- Multi-currency: home currency / cash rounding / allowed currencies ---

        private static CurrencySettings BuildCurrencies() =>
        [
            new CurrencyItem("USD", 0.01m, "$", "US Dollar"),
            new CurrencyItem("JPY", 1m, "¥", "Japanese Yen"),
            new CurrencyItem("CHF", 0.01m, "CHF", "Swiss Franc"),
        ];

        [Fact]
        [DisplayName("GetCashRounding returns the override unit when the company has one (CHF → 0.05)")]
        public void GetCashRounding_Override_ReturnsOverride()
        {
            var currencies = BuildCurrencies();
            var company = new CompanyInfo
            {
                CompanyId = "C001",
                CashRounding = [new CashRoundingItem("CHF", 0.05m)],
            };

            Assert.Equal(0.05m, company.GetCashRounding("CHF", currencies));
        }

        [Fact]
        [DisplayName("GetCashRounding falls back to the currency's natural smallest unit when the company has no override")]
        public void GetCashRounding_NoOverride_ReturnsCurrencyNaturalUnit()
        {
            var currencies = BuildCurrencies();
            var company = new CompanyInfo { CompanyId = "C001" };

            Assert.Equal(0.01m, company.GetCashRounding("USD", currencies));
            Assert.Equal(1m, company.GetCashRounding("JPY", currencies));
        }

        [Fact]
        [DisplayName("GetAllowedCurrencies returns the subset when the allowlist is not empty")]
        public void GetAllowedCurrencies_NonEmpty_ReturnsSubset()
        {
            var currencies = BuildCurrencies();
            var company = new CompanyInfo
            {
                CompanyId = "C001",
                AllowedCurrencies = [new AllowedCurrencyItem("USD"), new AllowedCurrencyItem("JPY")],
            };

            Assert.Equal(["USD", "JPY"], company.GetAllowedCurrencies(currencies));
        }

        [Fact]
        [DisplayName("GetAllowedCurrencies returns every system currency code when the allowlist is empty")]
        public void GetAllowedCurrencies_Empty_ReturnsAllSystemCodes()
        {
            var currencies = BuildCurrencies();
            var company = new CompanyInfo { CompanyId = "C001" };

            Assert.Equal(["USD", "JPY", "CHF"], company.GetAllowedCurrencies(currencies));
        }

        [Fact]
        [DisplayName("CompanyInfo multi-currency fields keep home currency, cash rounding and allowlist through an XML round-trip")]
        public void CompanyInfo_MultiCurrencyFields_XmlRoundtrip()
        {
            var original = new CompanyInfo
            {
                CompanyId = "C001",
                DefaultCurrency = "USD",
                CashRounding = [new CashRoundingItem("CHF", 0.05m)],
                AllowedCurrencies = [new AllowedCurrencyItem("USD"), new AllowedCurrencyItem("JPY")],
            };

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<CompanyInfo>(xml);

            Assert.NotNull(restored);
            Assert.Equal("USD", restored!.DefaultCurrency);
            Assert.Equal(0.05m, restored.CashRounding.FindUnit("CHF"));
            Assert.Equal(2, restored.AllowedCurrencies.Count);
        }

        [Fact]
        [DisplayName("CompanyCashRounding.FindUnit returns the unit on a hit and null on a miss")]
        public void CompanyCashRounding_FindUnit_HitAndMiss()
        {
            CompanyCashRounding rounding = [new CashRoundingItem("CHF", 0.05m)];

            Assert.Equal(0.05m, rounding.FindUnit("CHF"));
            Assert.Null(rounding.FindUnit("USD"));
        }
    }
}
