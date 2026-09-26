using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Settings;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Verifies the MessagePack wire round-trip of UnitSettings (the system-level unit of measure master). UnitSettings
    /// is shipped to the client so the UI runtime can resolve quantity and weight decimals, and its collection must be
    /// handled by CollectionBaseFormatter&lt;UnitSettings, UnitItem&gt; in the custom FormatterResolver.
    /// </summary>
    public sealed class UnitSettingsMessagePackTests
    {
        [Fact]
        [DisplayName("UnitSettings MessagePack round-trip preserves every unit and its decimals")]
        public void UnitSettings_RoundTrip_PreservesItems()
        {
            UnitSettings original =
            [
                new UnitItem("KG", 3, "weight", "Kilogram"),
                new UnitItem("PCS", 0, "count", "Pieces"),
            ];

            var bytes = MessagePackCodec.Serialize(original);
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);

            var restored = MessagePackCodec.Deserialize<UnitSettings>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Count);
            Assert.Equal(3, restored.GetDecimals("KG"));
            Assert.Equal(0, restored.GetDecimals("PCS"));
            Assert.Equal("weight", restored.Find("KG")!.Dimension);
        }

        [Fact]
        [DisplayName("An empty UnitSettings MessagePack round-trip returns an empty collection, not null")]
        public void UnitSettings_Empty_RoundTrip_Succeeds()
        {
            UnitSettings original = [];

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<UnitSettings>(bytes);

            Assert.NotNull(restored);
            Assert.Empty(restored!);
        }
    }
}
