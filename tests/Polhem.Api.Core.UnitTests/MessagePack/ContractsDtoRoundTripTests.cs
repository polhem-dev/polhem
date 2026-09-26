using System.ComponentModel;
using Polhem.Api.Contracts.AuditLog;
using Polhem.Api.Core.MessagePack;
using Polhem.Base.Serialization;
using Polhem.Definition.Logging;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Wire round-trip tests for the concrete DTOs in <c>Polhem.Api.Contracts</c> (MessagePack and JSON).
    /// </summary>
    /// <remarks>
    /// The contracts assembly is mostly interfaces but includes a few concrete DTOs, all marked
    /// <c>[MessagePackObject(keyAsPropertyName: true)]</c>. That means they really travel over the wire, yet they
    /// were previously only reached indirectly by higher-level tests and had no direct verification. They are pure
    /// wire DTOs (never written to disk), so under the serialization rules they need only MessagePack and JSON, not
    /// XML.
    /// </remarks>
    public class ContractsDtoRoundTripTests
    {
        [Fact]
        [DisplayName("RecordFieldChange round-trips through MessagePack and JSON, including a null-valued field")]
        public void RecordFieldChange_RoundTrips_PreservesValues()
        {
            var original = new RecordFieldChange
            {
                TableName = "ft_order_detail",
                RowKey = "8f14e45f-ea8f-4b0c-9f2b-1a2b3c4d5e6f",
                RowState = ChangeKind.Update,
                FieldName = "qty",
                OldValue = "10",
                NewValue = null
            };

            AssertRecordFieldChange(MessagePackCodec.Deserialize<RecordFieldChange>(MessagePackCodec.Serialize(original)));
            AssertRecordFieldChange(JsonCodec.Deserialize<RecordFieldChange>(JsonCodec.Serialize(original)));
        }

        private static void AssertRecordFieldChange(RecordFieldChange? restored)
        {
            Assert.NotNull(restored);
            Assert.Equal("ft_order_detail", restored.TableName);
            Assert.Equal("8f14e45f-ea8f-4b0c-9f2b-1a2b3c4d5e6f", restored.RowKey);
            Assert.Equal(ChangeKind.Update, restored.RowState);
            Assert.Equal("qty", restored.FieldName);
            Assert.Equal("10", restored.OldValue);
            Assert.Null(restored.NewValue);
        }
    }
}
