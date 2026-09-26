using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// MessagePack round-trip tests for <see cref="DeleteRequest"/> / <see cref="DeleteResponse"/>.
    /// Minimal types, so only RowId and RowsAffected need to come back.
    /// </summary>
    public class DeleteMessagePackTests
    {
        [Fact]
        [DisplayName("DeleteRequest round-trip restores RowId")]
        public void DeleteRequest_RoundTrip_PreservesRowId()
        {
            var rowId = Guid.NewGuid();
            var request = new DeleteRequest { RowId = rowId };

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<DeleteRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(rowId, restored!.RowId);
        }

        [Fact]
        [DisplayName("DeleteResponse round-trip restores RowsAffected")]
        public void DeleteResponse_RoundTrip_PreservesRowsAffected()
        {
            var response = new DeleteResponse { RowsAffected = 5 };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<DeleteResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(5, restored!.RowsAffected);
        }

        [Fact]
        [DisplayName("DeleteResponse with RowsAffected = 0 round-trips as 0")]
        public void DeleteResponse_ZeroRowsAffected_RoundTrip()
        {
            var response = new DeleteResponse();

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<DeleteResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(0, restored!.RowsAffected);
        }
    }
}
