using System.ComponentModel;
using System.Text;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for GzipPayloadCompressor.
    /// </summary>
    public class GzipPayloadCompressorTests
    {
        [Fact]
        [DisplayName("CompressionMethod is \"gzip\"")]
        public void CompressionMethod_IsGzip()
        {
            var compressor = new GzipPayloadCompressor();

            Assert.Equal("gzip", compressor.CompressionMethod);
        }

        [Fact]
        [DisplayName("Decompress after Compress restores the original content")]
        public void CompressDecompress_RoundTrip_RestoresOriginalBytes()
        {
            var compressor = new GzipPayloadCompressor();
            var original = Encoding.UTF8.GetBytes(
                "The quick brown fox jumps over the lazy dog. " +
                "The quick brown fox jumps over the lazy dog. " +
                "The quick brown fox jumps over the lazy dog.");

            var compressed = compressor.Compress(original);
            var decompressed = compressor.Decompress(compressed);

            Assert.Equal(original, decompressed);
        }

        [Fact]
        [DisplayName("Compress makes repetitive content shorter than the original")]
        public void Compress_ProducesDifferentBytes()
        {
            var compressor = new GzipPayloadCompressor();
            var original = Encoding.UTF8.GetBytes(new string('A', 1000));

            var compressed = compressor.Compress(original);

            Assert.NotEqual(original, compressed);
            Assert.True(compressed.Length < original.Length);
        }
    }
}
