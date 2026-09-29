using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Business.System;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// JSON round-trip of the <c>Ping</c> business arguments and result.
    /// </summary>
    public class PingSerializationTests
    {
        // Re-serializing after the round trip must equal the original string; only that proves every field came back.
        private static void AssertJsonRoundTrips<T>(T value)
        {
            string json = JsonCodec.Serialize(value!);
            var restored = JsonCodec.Deserialize<T>(json);
            Assert.NotNull(restored);
            Assert.Equal(json, JsonCodec.Serialize(restored!));
        }

        [Fact]
        [DisplayName("PingArgs and PingResult round-trip through JSON serialization")]
        public void SerializePing_Json_RoundTripsCorrectly()
        {
            AssertJsonRoundTrips(new PingArgs
            {
                ClientName = "TestClient",
                TraceId = Guid.NewGuid().ToString()
            });

            AssertJsonRoundTrips(new PingResult
            {
                Status = "pong",
                ServerTime = new DateTime(2025, 5, 16, 8, 30, 0, DateTimeKind.Utc),
                Version = "1.2.3",
                TraceId = Guid.NewGuid().ToString()
            });
        }
    }
}
