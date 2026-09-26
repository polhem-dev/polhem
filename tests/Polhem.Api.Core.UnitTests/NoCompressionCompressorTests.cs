using System.ComponentModel;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for NoCompressionCompressor.
    /// </summary>
    public class NoCompressionCompressorTests
    {
        [Fact]
        [DisplayName("CompressionMethod is \"none\"")]
        public void CompressionMethod_IsNone()
        {
            var compressor = new NoCompressionCompressor();

            Assert.Equal("none", compressor.CompressionMethod);
        }

        [Fact]
        [DisplayName("Compress returns the original byte array")]
        public void Compress_ReturnsSameBytes()
        {
            var compressor = new NoCompressionCompressor();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            var result = compressor.Compress(data);

            Assert.Same(data, result);
        }

        [Fact]
        [DisplayName("Decompress returns the original byte array")]
        public void Decompress_ReturnsSameBytes()
        {
            var compressor = new NoCompressionCompressor();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            var result = compressor.Decompress(data);

            Assert.Same(data, result);
        }
    }
}
