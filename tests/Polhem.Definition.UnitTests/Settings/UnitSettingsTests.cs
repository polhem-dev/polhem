using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// UnitSettings (the system-level unit of measure master): GetDecimals hits and fallback, and the XML / JSON round-trips.
    /// </summary>
    public class UnitSettingsTests
    {
        private static UnitSettings BuildSettings() =>
        [
            new UnitItem("KG", 3, "weight", "Kilogram"),
            new UnitItem("PCS", 0, "count", "Pieces"),
            new UnitItem("M", 2, "length", "Metre"),
        ];

        [Theory]
        [InlineData("KG", 3)]
        [InlineData("PCS", 0)]
        [InlineData("M", 2)]
        [DisplayName("GetDecimals returns the unit's decimals on a hit")]
        public void GetDecimals_Hit_ReturnsUnitDecimals(string code, int expected)
        {
            var settings = BuildSettings();

            Assert.Equal(expected, settings.GetDecimals(code));
        }

        [Fact]
        [DisplayName("GetDecimals returns the fallback 0 on a miss")]
        public void GetDecimals_Miss_ReturnsFallback()
        {
            var settings = BuildSettings();

            Assert.Equal(0, settings.GetDecimals("XXX"));
            Assert.Equal(0, settings.GetDecimals(""));
        }

        [Fact]
        [DisplayName("GetDecimals matches unit codes case-insensitively")]
        public void GetDecimals_CaseInsensitive()
        {
            var settings = BuildSettings();

            Assert.Equal(3, settings.GetDecimals("kg"));
        }

        [Fact]
        [DisplayName("Find returns the item on a hit and null on a miss")]
        public void Find_HitAndMiss()
        {
            var settings = BuildSettings();

            Assert.Equal("Kilogram", settings.Find("KG")?.Name);
            Assert.Null(settings.Find("XXX"));
        }

        [Fact]
        [DisplayName("UnitSettings round-trips every field through XML serialization")]
        public void UnitSettings_XmlRoundtrip_PreservesItems()
        {
            var original = BuildSettings();

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<UnitSettings>(xml);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.Count);
            var kg = restored.Find("KG");
            Assert.NotNull(kg);
            Assert.Equal(3, kg!.Decimals);
            Assert.Equal("weight", kg.Dimension);
            Assert.Equal("Kilogram", kg.Name);
            Assert.Equal(0, restored.GetDecimals("PCS"));
        }

        [Fact]
        [DisplayName("UnitSettings round-trips every field through JSON serialization")]
        public void UnitSettings_JsonRoundtrip_PreservesItems()
        {
            var original = BuildSettings();

            var json = JsonCodec.Serialize(original);
            var restored = JsonCodec.Deserialize<UnitSettings>(json);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.Count);
            Assert.Equal(3, restored.GetDecimals("KG"));
            Assert.Equal(0, restored.GetDecimals("PCS"));
        }
    }
}
