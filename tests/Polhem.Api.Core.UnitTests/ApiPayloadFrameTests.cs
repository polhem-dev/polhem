using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Encoding and decoding tests for ApiPayloadFrame (pure logic that does not touch process-wide state).
    /// </summary>
    public class ApiPayloadFrameTests
    {
        [Fact]
        [DisplayName("Extract after Prepend restores every field and the body")]
        public void Extract_AfterPrepend_RoundTripsAllFields()
        {
            var body = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var frame = new ApiPayloadFrame(timestampMs: 1_700_000_000_123, sequence: 42);

            var restored = ApiPayloadFrame.Extract(frame.Prepend(body), out var restoredBody);

            Assert.Equal(ApiPayloadFrame.CurrentVersion, restored.Version);
            Assert.Equal(1_700_000_000_123, restored.TimestampMs);
            Assert.Equal(42, restored.Sequence);
            Assert.Equal(body, restoredBody);
        }

        [Fact]
        [DisplayName("Prepend writes big-endian, and the body follows immediately after 17 bytes")]
        public void Prepend_WritesBigEndianAtFixedOffsets()
        {
            // The byte order is fixed to big-endian regardless of platform, because the two ends may run on different architectures.
            var frame = new ApiPayloadFrame(timestampMs: 0x0102030405060708, sequence: 0x1112131415161718);

            var framed = frame.Prepend(new byte[] { 0xAA });

            Assert.Equal(ApiPayloadFrame.Version1Size + 1, framed.Length);
            Assert.Equal(ApiPayloadFrame.CurrentVersion, framed[0]);
            Assert.Equal(0x01, framed[1]);
            Assert.Equal(0x08, framed[8]);
            Assert.Equal(0x11, framed[9]);
            Assert.Equal(0x18, framed[16]);
            Assert.Equal(0xAA, framed[17]);
        }

        [Fact]
        [DisplayName("Extract on exactly 17 bytes returns an empty body instead of throwing")]
        public void Extract_ExactlyFrameSized_ReturnsEmptyBody()
        {
            var framed = new ApiPayloadFrame(1, 2).Prepend(Array.Empty<byte>());

            ApiPayloadFrame.Extract(framed, out var body);

            Assert.Empty(body);
        }

        [Fact]
        [DisplayName("Extract on a buffer that is too short throws ReplayRejectedException rather than an index out of range")]
        public void Extract_BufferShorterThanFrame_ThrowsReplayRejected()
        {
            // An old client sends no frame, so the start of its body is read as a frame. When it is too short, the
            // result must be an explicit rejection, not an `IndexOutOfRangeException`.
            var tooShort = new byte[ApiPayloadFrame.Version1Size - 1];

            Assert.Throws<ReplayRejectedException>(() => ApiPayloadFrame.Extract(tooShort, out _));
        }

        [Fact]
        [DisplayName("Extract throws ReplayRejectedException for an unknown version")]
        public void Extract_UnknownVersion_ThrowsReplayRejected()
        {
            // The frame carries no length, so the reader learns from the version how many bytes to consume.
            // An unrecognized version can therefore only be rejected.
            var framed = new ApiPayloadFrame(1, 2).Prepend(new byte[] { 0x01 });
            framed[0] = 0xFE;

            var ex = Assert.Throws<ReplayRejectedException>(() => ApiPayloadFrame.Extract(framed, out _));

            Assert.Contains("254", ex.Message, StringComparison.Ordinal);
        }
    }
}
