using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the default value of <see cref="BackendConfiguration.DefaultTimeZone"/>.
    /// </summary>
    /// <remarks>
    /// This default is a deliberate compatibility choice, not an arbitrary constant: st_user.time_zone was a new column,
    /// so every existing row is empty, and an empty default (meaning UTC) would shift all times of every existing deployment
    /// after the upgrade. Changing this value changes the displayed times of existing deployments and is a breaking change.
    /// </remarks>
    public class BackendConfigurationTimeZoneTests
    {
        [Fact]
        [DisplayName("DefaultTimeZone defaults to Asia/Taipei (upgrade compatibility)")]
        public void DefaultTimeZone_Default_IsAsiaTaipei()
        {
            var config = new BackendConfiguration();

            Assert.Equal("Asia/Taipei", config.DefaultTimeZone);
        }

        [Fact]
        [DisplayName("DefaultTimeZone can be set to an empty string to use UTC")]
        public void DefaultTimeZone_CanBeClearedForUtc()
        {
            var config = new BackendConfiguration { DefaultTimeZone = string.Empty };

            // An empty string is a valid setting: the conversion layer (FrameworkClock / DateTimeZoneConverter /
            // PayloadZoneConverter) treats an empty time zone as UTC and does not convert.
            Assert.Empty(config.DefaultTimeZone);
        }
    }
}
