using System.ComponentModel;

namespace Polhem.ObjectCaching.UnitTests
{
    /// <summary>
    /// Tests the capped miss-marker set behind <see cref="KeyObjectCache{T}.MaxNegativeEntries"/>.
    /// </summary>
    public class BoundedMissMarkersTests
    {
        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => _now;
            public void Advance(TimeSpan by) => _now += by;
        }

        [Fact]
        [DisplayName("A marker is live until its expiry and gone after it")]
        public void Contains_AfterExpiry_ReturnsFalse()
        {
            var clock = new ManualClock();
            var markers = new BoundedMissMarkers(10, clock);
            markers.Add("k", clock.GetUtcNow().AddMinutes(1), notifyKey: null);

            Assert.True(markers.Contains("k"));
            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.False(markers.Contains("k"));
        }

        [Fact]
        [DisplayName("When full of live markers, a new miss is not recorded")]
        public void Add_WhenFull_DoesNotGrow()
        {
            var clock = new ManualClock();
            var markers = new BoundedMissMarkers(3, clock);
            for (int i = 0; i < 3; i++)
                markers.Add($"k{i}", clock.GetUtcNow().AddMinutes(1), notifyKey: null);

            markers.Add("extra", clock.GetUtcNow().AddMinutes(1), notifyKey: null);

            Assert.Equal(3, markers.Count);
            Assert.False(markers.Contains("extra"));
        }

        [Fact]
        [DisplayName("When full, expired markers are swept to make room for a new one")]
        public void Add_WhenFullOfExpired_SweepsAndRecords()
        {
            var clock = new ManualClock();
            var markers = new BoundedMissMarkers(3, clock);
            for (int i = 0; i < 3; i++)
                markers.Add($"k{i}", clock.GetUtcNow().AddMinutes(1), notifyKey: null);
            clock.Advance(TimeSpan.FromMinutes(2));

            markers.Add("fresh", clock.GetUtcNow().AddMinutes(1), notifyKey: null);

            Assert.True(markers.Contains("fresh"));
            Assert.Equal(1, markers.Count);
        }

        [Fact]
        [DisplayName("A marker lapses when the cache-notify version of its key moves")]
        public void Contains_AfterNotifyVersionBump_ReturnsFalse()
        {
            var clock = new ManualClock();
            var markers = new BoundedMissMarkers(10, clock);
            string notifyKey = "Test:" + Guid.NewGuid().ToString("N");
            markers.Add("k", clock.GetUtcNow().AddMinutes(1), notifyKey);
            Assert.True(markers.Contains("k"));

            // The key is unique to this test, so bumping it in the process-wide store affects nothing else.
            CacheInfo.NotifyVersions.SetVersion(notifyKey, CacheInfo.NotifyVersions.GetVersion(notifyKey) + 1);

            Assert.False(markers.Contains("k"));
        }
    }
}
