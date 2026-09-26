using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// **Populated** round trips of <c>List&lt;T&gt;</c> wire members.
    /// </summary>
    /// <remarks>
    /// These members used to be exercised only with empty collections. An empty round trip proves neither that the
    /// item formatter is registered nor that every field really makes it onto the wire: when the collection is written
    /// as an empty array, the item type is never resolved. So each test puts **at least two** items in (pinning their
    /// order too) and compares them field by field.
    /// </remarks>
    public class CollectionMemberRoundTripTests
    {
        [Fact]
        [DisplayName("ListApiKeysResponse.ApiKeys round-trips populated, covering both a set and a null nullable DateTime")]
        public void ListApiKeysResponse_ApiKeys_RoundTrips()
        {
            var issued = new DateTime(2026, 8, 1, 9, 30, 0, DateTimeKind.Utc);
            var expired = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var original = new ListApiKeysResponse
            {
                ApiKeys =
                [
                    new ApiKeySummary
                    {
                        SysId = "key-1", SysName = "整合用", KeyType = ApiKeyType.ThirdParty,
                        Contact = "ops@example.invalid", Enabled = true,
                        IssuedAt = issued, ExpiredAt = expired,
                    },
                    // Both nullable `DateTime` values are null here; null and a set value take different wire branches.
                    new ApiKeySummary { SysId = "key-2", Enabled = false },
                ],
            };

            var restored = MessagePackCodec.Deserialize<ListApiKeysResponse>(
                MessagePackCodec.Serialize(original));

            Assert.NotNull(restored);
            Assert.Equal(2, restored.ApiKeys.Count);

            var first = restored.ApiKeys[0];
            Assert.Equal("key-1", first.SysId);
            Assert.Equal("整合用", first.SysName);
            Assert.Equal(ApiKeyType.ThirdParty, first.KeyType);
            Assert.Equal("ops@example.invalid", first.Contact);
            Assert.True(first.Enabled);
            Assert.Equal(issued, first.IssuedAt);
            Assert.Equal(expired, first.ExpiredAt);

            var second = restored.ApiKeys[1];
            Assert.Equal("key-2", second.SysId);
            Assert.False(second.Enabled);
            Assert.Null(second.IssuedAt);
            Assert.Null(second.ExpiredAt);
        }
    }
}
