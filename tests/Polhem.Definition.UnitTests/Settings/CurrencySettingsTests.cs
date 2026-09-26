using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// CurrencySettings (the system-level currency master): GetRounding, GetDecimals (derived from the rounding factor),
    /// fallback, and the XML / JSON round-trips.
    /// </summary>
    public class CurrencySettingsTests
    {
        private static CurrencySettings BuildSettings() =>
        [
            new CurrencyItem("USD", 0.01m, "$", "US Dollar", "840"),
            new CurrencyItem("JPY", 1m, "¥", "Japanese Yen", "392"),
            new CurrencyItem("BHD", 0.001m, "BD", "Bahraini Dinar", "048"),
        ];

        [Fact]
        [DisplayName("GetRounding returns the currency's natural smallest unit on a hit")]
        public void GetRounding_Hit_ReturnsRounding()
        {
            var settings = BuildSettings();

            Assert.Equal(0.01m, settings.GetRounding("USD"));
            Assert.Equal(1m, settings.GetRounding("JPY"));
            Assert.Equal(0.001m, settings.GetRounding("BHD"));
        }

        [Fact]
        [DisplayName("GetRounding falls back to 0.01 on a miss")]
        public void GetRounding_Miss_ReturnsFallback()
        {
            var settings = BuildSettings();

            Assert.Equal(0.01m, settings.GetRounding("XXX"));
            Assert.Equal(0.01m, settings.GetRounding(""));
        }

        [Fact]
        [DisplayName("GetRounding matches currency codes case-insensitively")]
        public void GetRounding_CaseInsensitive()
        {
            var settings = BuildSettings();

            Assert.Equal(1m, settings.GetRounding("jpy"));
        }

        [Theory]
        [InlineData("USD", 2)]
        [InlineData("JPY", 0)]
        [InlineData("BHD", 3)]
        [InlineData("XXX", 2)] // fallback 0.01 → 2
        [DisplayName("GetDecimals derives the display decimals from the rounding factor")]
        public void GetDecimals_DerivesFromRounding(string code, int expected)
        {
            var settings = BuildSettings();

            Assert.Equal(expected, settings.GetDecimals(code));
        }

        [Theory]
        [InlineData("0.01", 2)]
        [InlineData("0.001", 3)]
        [InlineData("1", 0)]
        [InlineData("10", 0)]
        [InlineData("0.05", 2)]
        [InlineData("0", 0)]
        [DisplayName("DecimalsFromRounding computes the decimals from the rounding factor (decimal-safe)")]
        public void DecimalsFromRounding_ComputesDecimals(string roundingText, int expected)
        {
            var rounding = decimal.Parse(roundingText, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(expected, CurrencySettings.DecimalsFromRounding(rounding));
        }

        [Fact]
        [DisplayName("Find returns the item on a hit and null on a miss")]
        public void Find_HitAndMiss()
        {
            var settings = BuildSettings();

            Assert.Equal("US Dollar", settings.Find("USD")?.Name);
            Assert.Null(settings.Find("XXX"));
        }

        [Fact]
        [DisplayName("CurrencySettings round-trips every field through XML serialization")]
        public void CurrencySettings_XmlRoundtrip_PreservesItems()
        {
            var original = BuildSettings();

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<CurrencySettings>(xml);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.Count);
            var usd = restored.Find("USD");
            Assert.NotNull(usd);
            Assert.Equal("840", usd!.Numeric);
            Assert.Equal(0.01m, usd.Rounding);
            Assert.Equal("$", usd.Symbol);
            Assert.Equal("US Dollar", usd.Name);
            Assert.Equal(0, restored.GetDecimals("JPY"));
            Assert.Equal(3, restored.GetDecimals("BHD"));
        }

        [Fact]
        [DisplayName("CurrencySettings round-trips every field through JSON serialization")]
        public void CurrencySettings_JsonRoundtrip_PreservesItems()
        {
            var original = BuildSettings();

            var json = JsonCodec.Serialize(original);
            var restored = JsonCodec.Deserialize<CurrencySettings>(json);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.Count);
            Assert.Equal(2, restored.GetDecimals("USD"));
            Assert.Equal(0, restored.GetDecimals("JPY"));
        }
    }
}
