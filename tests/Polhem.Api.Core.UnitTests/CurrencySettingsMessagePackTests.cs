using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Settings;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Verifies the MessagePack wire round-trip of CurrencySettings (the system-level currency master). CurrencySettings
    /// ships to the client so the UI can resolve amount decimals at run time, and its collection must be handled by
    /// the custom FormatterResolver's CollectionBaseFormatter&lt;CurrencySettings, CurrencyItem&gt;.
    /// </summary>
    public sealed class CurrencySettingsMessagePackTests
    {
        [Fact]
        [DisplayName("A CurrencySettings MessagePack round-trip keeps every currency and its decimals")]
        public void CurrencySettings_RoundTrip_PreservesItems()
        {
            CurrencySettings original =
            [
                new CurrencyItem("USD", 0.01m, "$", "US Dollar", "840"),
                new CurrencyItem("JPY", 1m, "¥", "Japanese Yen", "392"),
                new CurrencyItem("BHD", 0.001m, "BD", "Bahraini Dinar", "048"),
            ];

            var bytes = MessagePackCodec.Serialize(original);
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);

            var restored = MessagePackCodec.Deserialize<CurrencySettings>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.Count);
            Assert.Equal(2, restored.GetDecimals("USD"));
            Assert.Equal(0, restored.GetDecimals("JPY"));
            Assert.Equal(3, restored.GetDecimals("BHD"));
            Assert.Equal("$", restored.Find("USD")!.Symbol);
        }

        [Fact]
        [DisplayName("An empty CurrencySettings MessagePack round-trip returns an empty collection (not null)")]
        public void CurrencySettings_Empty_RoundTrip_Succeeds()
        {
            CurrencySettings original = [];

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<CurrencySettings>(bytes);

            Assert.NotNull(restored);
            Assert.Empty(restored!);
        }
    }
}
