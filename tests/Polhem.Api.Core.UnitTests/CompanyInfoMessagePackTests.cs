using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition;
using Polhem.Definition.Identity;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Verifies the MessagePack wire round-trip of CompanyInfo (including the CompanyNumberFormats collection).
    /// CompanyInfo travels over MessagePack through IEnterCompanyResponse.Company, and CompanyNumberFormats has to be
    /// handled by the CollectionBaseFormatter of the custom FormatterResolver.
    /// </summary>
    public sealed class CompanyInfoMessagePackTests
    {
        [Fact]
        [DisplayName("CompanyInfo with number format overrides keeps the overrides and basic fields through a MessagePack round-trip")]
        public void CompanyInfo_WithNumberFormats_RoundTrip_Succeeds()
        {
            var original = new CompanyInfo
            {
                CompanyId = "C001",
                CompanyName = "測試公司",
                CompanyDatabaseId = "common",
                CustomizeId = "",
                NumberFormats =
                [
                    new NumberFormatItem(NumberKind.Percent, 3),
                    new NumberFormatItem(NumberKind.UnitPrice, 6),
                ],
            };

            var bytes = MessagePackCodec.Serialize(original);
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);

            var restored = MessagePackCodec.Deserialize<CompanyInfo>(bytes);

            Assert.NotNull(restored);
            Assert.Equal("C001", restored!.CompanyId);
            Assert.Equal("測試公司", restored.CompanyName);
            Assert.Equal(2, restored.NumberFormats.Count);
            Assert.Equal(3, restored.GetDecimals(NumberKind.Percent));
            Assert.Equal(6, restored.GetDecimals(NumberKind.UnitPrice));
            // A kind that is not overridden falls back to the framework default.
            Assert.Equal(2, restored.GetDecimals(NumberKind.Amount));
        }

        [Fact]
        [DisplayName("CompanyInfo with an empty number format table round-trips through MessagePack to an empty table that falls back to the framework defaults")]
        public void CompanyInfo_EmptyNumberFormats_RoundTrip_Succeeds()
        {
            var original = new CompanyInfo { CompanyId = "C001", CompanyName = "測試公司" };

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<CompanyInfo>(bytes);

            Assert.NotNull(restored);
            Assert.Empty(restored!.NumberFormats);
            Assert.Equal(4, restored.GetDecimals(NumberKind.UnitPrice));
        }

        [Fact]
        [DisplayName("CompanyInfo keeps the default currency, cash rounding and currency allowlist through a MessagePack round-trip")]
        public void CompanyInfo_WithMultiCurrencyFields_RoundTrip_Succeeds()
        {
            var original = new CompanyInfo
            {
                CompanyId = "C001",
                CompanyName = "測試公司",
                DefaultCurrency = "USD",
                CashRounding = [new CashRoundingItem("CHF", 0.05m)],
                AllowedCurrencies = [new AllowedCurrencyItem("USD"), new AllowedCurrencyItem("JPY")],
            };

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<CompanyInfo>(bytes);

            Assert.NotNull(restored);
            Assert.Equal("USD", restored!.DefaultCurrency);
            Assert.Equal(0.05m, restored.CashRounding.FindUnit("CHF"));
            Assert.Equal(2, restored.AllowedCurrencies.Count);
        }

        [Fact]
        [DisplayName("CompanyInfo with empty multi-currency fields round-trips through MessagePack to empty tables and an empty default currency")]
        public void CompanyInfo_EmptyMultiCurrencyFields_RoundTrip_Succeeds()
        {
            var original = new CompanyInfo { CompanyId = "C001", CompanyName = "測試公司" };

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<CompanyInfo>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(string.Empty, restored!.DefaultCurrency);
            Assert.Empty(restored.CashRounding);
            Assert.Empty(restored.AllowedCurrencies);
        }
    }
}
